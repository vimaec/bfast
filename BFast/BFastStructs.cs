using System;
using System.Runtime.InteropServices;

namespace Ara3D.BFast
{
    public static class Constants
    {
        public const ulong Magic = 0xBFA5ul;
        public const ulong SameEndian = Magic; 
        public const ulong SwappedEndian = 0x5AFB;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
    public struct Range
    {
        [FieldOffset(0)] public ulong Begin;
        [FieldOffset(8)] public ulong End;

        public ulong Count { get { return End - Begin; } }
        public static ulong Size = 16;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 32)]
    public struct FileHeader
    {
        [FieldOffset(0)]    public ulong Magic;         // Either Constants.SameEndian or Constants.SwappedEndian depending on endianess of writer compared to reader. 
        [FieldOffset(8)]    public ulong DataStart;     // <= file size
        [FieldOffset(16)]   public ulong DataEnd;       // >= DataStart and <= file size
        [FieldOffset(24)]   public ulong NumArrays;     // number of arrays 

        public ulong ArrayOffsetsEnd { get { return ArrayOffsetsStart + NumArrays * 16; } }

        public static ulong Size = 32;
        public static ulong ArrayOffsetsStart = Size;

        public ulong GetOffsetOfRange(ulong n) {
            if (n < 0 || n >= NumArrays)
                throw new IndexOutOfRangeException();
            return ArrayOffsetsStart + Range.Size * n;
        }
    };
}
