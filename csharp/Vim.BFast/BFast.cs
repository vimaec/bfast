/*
    BFAST - Binary Format for Array Streaming and Transmission
    Copyright 2019, VIMaec LLC
    Copyright 2018, Ara 3D, Inc.
    Usage licensed under terms of MIT License

    The BFAST format is a simple, generic, and efficient representation of 
    buffers (arrays of binary data) with optional names.  
    
    It can be used in place of a zip when compression is not required, or when a simple protocol
    is required for transmitting data to/from disk, between processes, or over a network. 

    In C# a BFast is an `IList<INamedBuffer>` where an INamedBuffer is a simple interface
    that provide access to a `Bytes` property of type `Span<byte>` and a `Name` property
    of type `string`.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Vim
{
    /// <summary>
    /// Wraps an array of byte buffers encoding a BFast structure and provides validation and safe access to the memory. 
    /// The BFAST file/data format is structured as follows:
    ///   * File header   - Fixed size file descriptor
    ///   * Ranges        - An array of pairs of offsets that point to the begin and end of each data arrays
    ///   * Array data    - All of the array data is contained in this section.
    /// </summary>
    public static class BFast
    {
        public static class Constants
        {
            public const ulong Magic = 0xBFA5;
            
            // https://en.wikipedia.org/wiki/Endianness
            public const ulong SameEndian = Magic;
            public const ulong SwappedEndian = 0xA5BFul << 48;
        }

        /// <summary>
        /// This tells us where a particular array begins and ends in relation to the beginning of a file.
        /// * Begin must be less than or equal to End.
        /// * Begin must be greater than or equal to DataStart
        /// * End must be less than or equal to DataEnd
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
        public struct Range
        {
            [FieldOffset(0)] public ulong Begin;
            [FieldOffset(8)] public ulong End;

            public ulong Count => End - Begin;
            public static ulong Size = 16;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 8, Size = 32)]
        public struct Header
        {
            [FieldOffset(0)] public ulong Magic;         // Either Constants.SameEndian or Constants.SwappedEndian depending on endianess of writer compared to reader. 
            [FieldOffset(8)] public ulong DataStart;     // <= file size and >= ArrayRangesEnd and >= FileHeader.ByteCount
            [FieldOffset(16)] public ulong DataEnd;      // >= DataStart and <= file size
            [FieldOffset(24)] public ulong NumArrays;    // number of arrays 

            /// <summary>
            /// This is where the array ranges are finished. 
            /// Must be less than or equal to DataStart.
            /// Must be greater than or equal to FileHeader.ByteCount
            /// </summary>
            public ulong RangesEnd => Size + NumArrays * 16;

            /// <summary>
            /// The size of the FileHeader structure 
            /// </summary>
            public static ulong Size = 32;

            /// <summary>
            /// Returns true if the producer of the BFast file has the same endianness as the current library
            /// </summary>
            public bool SameEndian => Magic == Constants.SameEndian;
        };

        /// <summary>
        /// This is an internal data structure prepared before writing out all of the BFast 
        /// </summary>
        public class BFastData
        {
            public Header Header = new Header();
            public Range[] Ranges;
            public IList<IBuffer> Buffers;
        }

        public const ulong ALIGNMENT = 32;

        /// <summary>
        /// Given a position in the stream, tells us where the the next aligned position will be, if it the current position is not aligned.
        /// </summary>
        public static ulong ComputeNextAlignment(ulong n)
            => IsAligned(n) ? n : n + ALIGNMENT - (n % ALIGNMENT);

        /// <summary>
        /// Given a position in the stream, computes how much padding is required to bring the value to an algitned point. 
        /// </summary>
        public static ulong ComputePadding(ulong n)
            => ComputeNextAlignment(n) - n;

        /// <summary>
        /// Given a position in the stream, tells us whether the position is aligned.
        /// </summary>
        public static bool IsAligned(ulong n)
            => n % ALIGNMENT == 0;

        /// <summary>
        /// Write enough padding bytes to bring the current stream position to an aligned position
        /// </summary>
        public static void WritePadding(BinaryWriter bw)
        {
            var padding = ComputePadding((ulong)bw.BaseStream.Position);
            for (var i = 0ul; i < padding; ++i)
                bw.Write((byte)0);
            Debug.Assert(IsAligned((ulong)bw.BaseStream.Position));
        }

        /// <summary>
        /// Extracts the BFast header from the first bytes of a span.
        /// </summary>
        public static Header GetHeader(Span<byte> bytes)
        {
            // Assure that the data is of sufficient size to get the header 
            if (Header.Size > (ulong)bytes.Length)
                throw new Exception($"Data length ({bytes.Length}) is smaller than size of FileHeader ({Header.Size})");

            // Get the values that make up the header
            var values = MemoryMarshal.Cast<byte, ulong>(bytes).Slice(0, 4).ToArray();
            var header = new Header
            {
                Magic = values[0],
                DataStart = values[1],
                DataEnd = values[2],
                NumArrays = values[3],
            };
            header.Validate();
            return header;
        }

        /// <summary>
        /// Extracts the data range structs from a byte span given the file header. Also performs basic validation.
        /// </summary>
        public static Range[] GetRanges(Header header, Span<byte> bytes)
        {
            // NOTE: this will fail when dealing with larger than 2^32 sized BFAST objects
            // This is due to a limitation of the Span implementation in .NET
            var rangeByteSpan = bytes.Slice((int)Header.Size, (int)(Range.Size * header.NumArrays));
            var rangeSpan = MemoryMarshal.Cast<byte, Range>(rangeByteSpan);
            var ranges = rangeSpan.ToArray();
            ValidateRanges(header, ranges);
            return ranges;
        }

        /// <summary>
        /// Given an array of bytes representing a BFast file, returns the array of data buffers. 
        /// </summary>
        public static IEnumerable<IBuffer> AsBFastRawBuffers(this byte[] bytes)
            => new Memory<byte>(bytes).AsBFastRawBuffers();

        /// <summary>
        /// Given a memory block of bytes representing a BFast file, returns the array of data buffers,
        /// </summary>
        public static IEnumerable<IBuffer> AsBFastRawBuffers(this Memory<byte> bytes)
        {
            var header = GetHeader(bytes.Span);
            var ranges = GetRanges(header, bytes.Span);
            return ranges.Select(r => bytes.Slice((int)r.Begin, (int)r.Count).ToBuffer());
        }

        /// <summary>
        /// BFast encode buffer names in first buffer as a concatenated array of null-terminated strings
        /// </summary>
        public static IEnumerable<INamedBuffer> UnpackBFastRawBuffers(this IEnumerable<IBuffer> buffers)
            => buffers.Skip(1).Zip(buffers.First().GetStrings(), BufferExtensions.ToNamedBuffer);

        /// <summary>
        /// Converts a BFast encoded memory block of an array of named buffers
        /// </summary>
        public static INamedBuffer[] Unpack(this Memory<byte> bytes)
            => bytes.AsBFastRawBuffers().UnpackBFastRawBuffers().ToArray();

        /// <summary>
        /// Converts a memory block into a BFast (array of named buffers)
        /// </summary>
        public static INamedBuffer[] Unpack(this byte[] bytes)
            => new Memory<byte>(bytes).Unpack();

        /// <summary>
        /// Loads BFast raw buffers from a filePath
        /// </summary>
        public static IEnumerable<IBuffer> ReadBFastRawBuffers(string filePath)
            => File.ReadAllBytes(filePath).AsBFastRawBuffers();

        /// <summary>
        /// Loads a BFast file from the given path 
        /// </summary>
        public static INamedBuffer[] Read(string filePath)
            => ReadBFastRawBuffers(filePath).UnpackBFastRawBuffers().ToArray();

        /// <summary>
        /// Writes a BFast to stream using the provided BinaryWriter
        /// </summary>
        public static BinaryWriter Write(this BFastData data, BinaryWriter bw)
        {
            bw.Write(data.Header.Magic);
            bw.Write(data.Header.DataStart);
            bw.Write(data.Header.DataEnd);
            bw.Write(data.Header.NumArrays);
            foreach (var r in data.Ranges)
                bw.Write(r.ToBytes());
            WritePadding(bw);
            foreach (var b in data.Buffers)
            {
                WritePadding(bw);
                bw.Write(b.Bytes.ToArray());
            }
            return bw;
        }

        /// <summary>
        /// Copies prepared BFast encoding into the given byte array in parallel.
        /// </summary>
        public static byte[] ToBytes(this BFastData data)
            => data.CopyTo(new byte[data.Header.DataEnd]);

        /// <summary>
        /// A ForLoop that may or may not be parallized 
        /// </summary>
        public static void ForLoop(int from, int to, Action<int> action, bool parallelize)
        {
            if (parallelize)
                Parallel.For(from, to, action);
            else
                for (var i = from; i < to; ++i)
                    action(i);
        }

        /// <summary>
        /// Copies prepared BFast data into the given byte array, optionally in parallel.
        /// </summary>
        public static byte[] CopyTo(this BFastData data, byte[] dest, int offset = 0, bool parallelize = false)
        {
            if (dest.Length < (int)data.Header.DataEnd + offset)
                throw new Exception("Byte array is not sufficiently large");

            data.Header.ToBytes().CopyTo(dest, offset);
            data.Ranges.ToBytes().CopyTo(dest, (int)Header.Size);

            ForLoop(0, data.Ranges.Length,
                i =>
                {
                    var range = data.Ranges[i];
                    var buffer = data.Buffers[i];
                    var target = dest.AsSpan().Slice((int)range.Begin + offset, (int)range.Count);
                    buffer.Bytes.CopyTo(target);
                }, parallelize);

            return dest;
        }

        /// <summary>
        /// Copies prepared BFast data into the given byte array in parallel.
        /// </summary>
        public static byte[] ParallelCopyTo(this BFastData data, byte[] dest, int offset = 0)
            => CopyTo(data, dest, offset, true);

        /// <summary>
        /// Converts an array of byte arrays to a BFAST file format in memory. 
        /// </summary>
        public static byte[] Pack(this IEnumerable<byte[]> buffers, IEnumerable<string> names = null)
            => buffers.Select(BufferExtensions.ToBuffer).Pack(names);

        /// <summary>
        /// Converts an array of byte arrays to a BFAST file format in memory. 
        /// </summary>
        public static byte[] Pack(this IEnumerable<IBuffer> buffers, IEnumerable<string> names = null)
            => buffers.ToNamedBuffers(names).Pack();

        /// <summary>
        /// Converts an array of data buffers to a BFAST file format in memory. 
        /// </summary>
        public static byte[] Pack(this IEnumerable<INamedBuffer> buffers)
            => buffers.ToRawBFastBuffers().ToBFastData().ToBytes();

        /// <summary>
        /// Converts a collection of named buffers into Raw BFAST buffers, 
        /// where the first buffer contains all of the names 
        /// </summary>
        public static IList<IBuffer> ToRawBFastBuffers(this IEnumerable<INamedBuffer> buffers)
        {
            var nameBuffer = buffers.Select(b => b.Name).ToBuffer();
            var tmp = new List<IBuffer> { nameBuffer };
            tmp.AddRange(buffers);
            return tmp;
        }

        /// <summary>
        /// Writes an array of data buffers to the given file. 
        /// </summary>
        public static void ToBFastFile(this IEnumerable<INamedBuffer> buffers, string filePath)
            => WriteBFast(buffers, File.OpenWrite(filePath));

        /// <summary>
        /// Writes an array of data buffers to the given data stream 
        /// </summary>
        public static T WriteBFast<T>(this IEnumerable<INamedBuffer> buffers, T stream) where T : Stream
            => buffers.ToBFastData().Write(stream);

        /// <summary>
        /// Prepares a BFastData structure from named buffers
        /// </summary>
        public static BFastData ToBFastData(this IEnumerable<INamedBuffer> buffers)
            => ToBFastData(buffers.ToRawBFastBuffers());

        /// <summary>
        /// Prepares a BFastData structure from raw BFast arrays (first one encodes names)
        /// </summary>
        public static BFastData ToBFastData(this IList<IBuffer> buffers)
        {
            var data = new BFastData();
            data.Buffers = buffers;
            data.Header.Magic = Constants.Magic;
            data.Header.NumArrays = (ulong)buffers.Count;
            data.Header.DataStart = ComputeNextAlignment(data.Header.RangesEnd);

            // Allocate the data for the ranges
            data.Ranges = new Range[data.Header.NumArrays];

            // Compute the offsets for the data buffers
            var curIndex = data.Header.DataStart;
            for (var i = 0; i < buffers.Count; ++i)
            {
                Debug.Assert(IsAligned(curIndex));

                data.Ranges[i].Begin = curIndex;
                curIndex += (ulong)buffers[i].Bytes.Length;
                data.Ranges[i].End = curIndex;
                curIndex = ComputeNextAlignment(curIndex);

                if (i > 0)
                    Debug.Assert(data.Ranges[i].Begin >= data.Ranges[i - 1].End);
                Debug.Assert(data.Ranges[i].Begin <= data.Ranges[i].End);
                Debug.Assert(data.Ranges[i].Count == (ulong)buffers[i].Bytes.Length);
            }

            // Finish with the header
            data.Header.DataEnd = curIndex;

            // Check that everything adds up 
            data.Header.Validate();
            data.Header.ValidateRanges(data.Ranges);

            return data;
        }

        /// <summary>
        /// Writes the BFast data to the given data stream 
        /// </summary>
        public static T Write<T>(this BFastData data, T stream) where T: Stream
        {
            using (var bw = new BinaryWriter(stream))
                data.Write(bw);
            return stream;
        }

        /// <summary>
        /// Checks that the header values are sensible, and throws an exception otherwise.
        /// </summary>
        public static void Validate(this Header header)
        {
            if (header.Magic != Constants.SameEndian && header.Magic != Constants.SwappedEndian)
                throw new Exception($"Invalid magic number {header.Magic}");

            if (header.DataStart < Header.Size)
                throw new Exception($"Data start {header.DataStart} cannot be before the file header size {Header.Size}");

            if (header.DataStart > header.DataEnd)
                throw new Exception($"Data start {header.DataStart} cannot be after the data end {header.DataEnd}");

            if (header.NumArrays < 0)
                throw new Exception($"Number of arrays {header.NumArrays} is not a positive number");

            if (header.RangesEnd > header.DataStart)
                throw new Exception($"Computed arrays ranges end must be less than the start of data {header.DataStart}");
        }

        /// <summary>
        /// Checks that the range values are sensible, and throws an exception otherwise.
        /// </summary>
        public static void ValidateRanges(this Header header, Range[] ranges)
        {
            if (ranges == null)
                throw new Exception("Ranges must not be null");

            var min = header.DataStart;
            var max = header.DataEnd;

            for (var i = 0; i < ranges.Length; ++i)
            {
                var begin = ranges[i].Begin;
                var end = ranges[i].End;
                if (begin < min || begin > max)
                    throw new Exception($"Array offset begin {begin} is not in valid span of {min} to {max}");
                if (i > 0)
                    if (begin < ranges[i - 1].End)
                        throw new Exception($"Array offset begin {begin} is overlapping with previous array {ranges[i - 1].End}");
                if (end < begin || end > max)
                    throw new Exception($"Array offset end {end} is not in valid span of {begin} to {max}");
            }
        }

        /// <summary>
        /// Given a list of raw buffers, we generate named buffers. 
        /// </summary>
        public static IList<INamedBuffer> RawBFastBuffersToNamedBuffers(this IList<IBuffer> buffers)
            => buffers.Skip(1).ToNamedBuffers(buffers[0].GetStrings()).ToList();
    }
}
