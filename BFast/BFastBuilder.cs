using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Ara3D.BFast
{
    /// <summary>
    /// Constructs a BFast data structure in memory incrementally. 
    /// </summary>
    public class BFastBuilder
    {
        readonly List<byte[]> buffers = new List<byte[]>();

        public BFastBuilder Add(byte[] bytes)
        {
            buffers.Add(bytes);
            return this;
        }

        public BFastBuilder Add(string s)
        {
            return Add(Encoding.UTF8.GetBytes(s));
        }

        public const int Alignment = 64;

        public static long ComputeNextAlignment(long n)
        {
            if (IsAligned(n))
                return n;
            return n + Alignment - (n % Alignment);
        }

        public static bool IsAligned(long n)
        {
            return n % Alignment == 0;
        }

        public BFast ToBFast()
        {
            // Construct the header
            var header = new FileHeader();
            var offsets = new Range[buffers.Count];
            header.Magic = Constants.Magic;
            header.NumArrays = buffers.Count;
            header.DataStart = ComputeNextAlignment(header.ArrayOffsetsEnd);
            // DataEnd is computed after iterating over all buffers

            // Compute the offsets for the data buffers
            var curIndex = header.DataStart;
            for (var i=0; i < buffers.Count; ++i)
            {
                Debug.Assert(IsAligned(curIndex));

                offsets[i].Begin = curIndex;
                curIndex += buffers[i].LongLength;
                offsets[i].End = curIndex;
                curIndex = ComputeNextAlignment(curIndex);

                Debug.Assert(offsets[i].Count == buffers[i].LongLength);
            }

            // Finish with the header
            header.DataEnd = curIndex;

            // Allocate a data-buffer
            var data = new byte[header.DataEnd];

            // Copy the FileHeader to the bytes
            var ptr = Marshal.AllocHGlobal((int)FileHeader.Size);
            try
            {
                Marshal.StructureToPtr(header, ptr, true);
                Marshal.Copy(ptr, data, 0, (int)FileHeader.Size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            // Copy the ArrayOffsets to the bytes
            ptr = Marshal.AllocHGlobal((int)Range.Size);
            try
            {
                for (var i = 0; i < offsets.Length; ++i)
                {
                    Marshal.StructureToPtr(offsets[i], ptr, true);
                    Marshal.Copy(ptr, data, (int)header.GetOffsetOfRange(i), (int)Range.Size);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            // Copy the data-buffers
            for (var i=0; i < offsets.Length; ++i)
            {
                Array.Copy(buffers[i], 0, data, offsets[i].Begin, offsets[i].Count);
            }

            return new BFast(data);
        }
    }
}
