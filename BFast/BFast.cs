using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ara3D.BFast
{
    /// <summary>
    /// Wraps an array of bytes encoding a BFast structure and provides validation and safe access to the memory. 
    /// The BFAST file/data format is structured as follows:\n
    ///   * File header   - Fixed size file descriptor\n
    ///   * Ranges        - An array of pairs of offsets that point to the begin and end of each data arrays\n
    ///   * Array data    - All of the array data is contained in this section.\n
    /// </summary>
    public class BFast
    {
        public long RawDataLength { get { return RawData.LongLength; } }
        public FileHeader Header { get; }
        public byte[] RawData { get; }
        public bool SameEndianness { get { return Header.Magic == Constants.SameEndian; } }
        public Range[] Ranges { get; }
       
        /// <summary>
        /// Creates a BFast structure from the data in memory. 
        /// </summary>
        /// <param name="data"></param>
        public BFast(byte[] data)
        {
            RawData = data;

            // Assure that the data is of sufficient size to get the header 
            if (FileHeader.Size > RawDataLength)
                throw new Exception($"Data length ({data.Length}) is smaller than size of FileHeader ({FileHeader.Size})");

            // Get the header
            Header = MarshalHeader();

            // Check each value tin the header
            if (Header.Magic != Constants.SameEndian && Header.Magic != Constants.SwappedEndian)
                throw new Exception($"Invalid magic number {Header.Magic}");

            if (Header.DataStart < FileHeader.Size || Header.DataStart > RawDataLength)
                throw new Exception($"Data start {Header.DataStart} is not in the valid range of {FileHeader.Size} to {RawDataLength}");

            if (Header.DataEnd < Header.DataStart || Header.DataEnd > RawDataLength)
                throw new Exception($"Data start {Header.DataStart} is not in the valid range of {Header.DataEnd} to {RawDataLength}");
            
            if (Header.NumArrays < 0 || Header.NumArrays > RawDataLength)
                throw new Exception($"Number of arrays {Header.NumArrays} is not in the valid range of {Header.NumArrays} to {RawDataLength}");

            if (FileHeader.ArrayOffsetsStart < FileHeader.Size || FileHeader.ArrayOffsetsStart > Header.DataStart)
                throw new Exception($"Array offset start {FileHeader.ArrayOffsetsStart} is not in valid range of {FileHeader.Size} to {Header.DataStart}");

            if (Header.ArrayOffsetsEnd < FileHeader.ArrayOffsetsStart|| Header.ArrayOffsetsEnd > Header.DataStart)
                throw new Exception($"Array offset end {Header.ArrayOffsetsEnd} is not in valid range of {FileHeader.ArrayOffsetsStart} to {Header.DataStart}");

            Ranges = MarshalRanges((int)Header.NumArrays);

            for (var i=0; i < Ranges.Length; ++i)
            {
                var min = Header.DataStart;
                var max = Header.DataEnd;
                var begin = Ranges[i].Begin;
                var end = Ranges[i].End;
                if (begin < min || begin > max)
                    throw new Exception($"Array offset begin {begin} is not in valid range of {min} to {max}");
                if (i > 0)
                    if (begin < Ranges[i - 1].End)
                        throw new Exception($"Array offset begin {begin} is overlapping with previous array {Ranges[i - 1].End}");
                if (end < begin || end > max)
                    throw new Exception($"Array offset end {end} is not in valid range of {begin} to {max}");
            }
        }

        public static BFast ReadFile(string file)
        {
            return new BFast(File.ReadAllBytes(file));
        }

        public long Count { get { return Header.NumArrays; } }

        public byte[] this[long n] {
            get {
                var r = new byte[Ranges[n].Count];
                Array.Copy(RawData, Ranges[n].Begin, r, 0, r.Length);
                return r;
            }
        }

        FileHeader MarshalHeader()
        {
            var intPtr = Marshal.AllocHGlobal((int)FileHeader.Size);
            try
            {
                Marshal.Copy(RawData, 0, intPtr, (int)FileHeader.Size);
                return Marshal.PtrToStructure<FileHeader>(intPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(intPtr);
            }
        }

        Range[] MarshalRanges(int n)
        {
            var ranges = new Range[n];
            var intPtr = Marshal.AllocHGlobal((int)Range.Size);
            try
            {
                for (var i = 0; i < n; ++i)
                {
                    var offset = Header.GetOffsetOfRange(i);
                    Marshal.Copy(RawData, (int)Header.GetOffsetOfRange(i), intPtr, (int)Range.Size);
                    ranges[i] = Marshal.PtrToStructure<Range>(intPtr);
                }
                return ranges;
            }
            finally
            {
                Marshal.FreeHGlobal(intPtr);
            }
        }

        public void WriteToFile(string path)
        {
            File.WriteAllBytes(path, RawData);
        }
    }
}
