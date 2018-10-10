using System;
using System.Runtime.InteropServices;

namespace Ara3D.BFast
{
    public static class Constants
    {
        public const ushort Magic = 0xBFA5;
        public const ushort SameEndian = Magic; 
        public const ushort SwappedEndian = 0x5AFB;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
    public struct Range
    {
        [FieldOffset(0)] public long Begin;
        [FieldOffset(8)] public long End;

        public long Count { get { return End - Begin; } }
        public static long Size = Marshal.SizeOf<FileHeader>(); // Should be 16
    };

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 32)]
    public struct FileHeader
    {
        [FieldOffset(0)]    public long Magic;        // Either Constants.SameEndian or Constants.SwappedEndian depending on endianess of writer compared to reader. 
        [FieldOffset(8)]    public long DataStart;     // <= fileSize
        [FieldOffset(16)]   public long DataEnd;       // >= data_start and <= file_size
        [FieldOffset(24)]   public long NumArrays;     // number of arrays 

        public long ArrayOffsetsEnd { get { return ArrayOffsetsStart + NumArrays * 16; } }

        public static long Size = Marshal.SizeOf<FileHeader>(); // Should be 32
        public static long ArrayOffsetsStart = Size;

        public long GetOffsetOfRange(int n) {
            if (n < 0 || n >= NumArrays)
                throw new IndexOutOfRangeException();
            return ArrayOffsetsStart + Range.Size * n;
        }
    };
}
