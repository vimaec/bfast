using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Vim.BFast;

namespace Vim
{
    public static class Tests
    {
        static int[] bigArray = Enumerable.Range(0, 1000 * 1000).ToArray();
        static IList<int[]> severalArrays = Enumerable.Repeat(bigArray, 100).ToList();

        [Test]
        public static void TestToStrings()
        {
            foreach (var s in new[] { "", "ababab", "a" + "\0" + "b" })
            {
                Assert.AreEqual(s, s.ToBuffer().GetString());
                Assert.AreEqual(s, s.ToBuffer().GetString());
            }
            var s1 = "Hello";
            var s2 = "world";
            var buffer1 = s1.ToBuffer();
            var buffer2 = s2.ToBuffer();
            Assert.AreEqual(s1, buffer1.GetString());
            Assert.AreEqual(s2, buffer2.GetString());
            var strings = new[] { s1, s2 };
            var bufferStrings = strings.ToBuffer();
            var tmp = bufferStrings.GetStrings();
            Assert.AreEqual(strings, tmp);
        }

        [Test]
        public static void TestByteCasts()
        {
            var ranges = new Range[3];
            var bytes = ranges.ToBytes();
            Assert.AreEqual(16, Range.Size);
            Assert.AreEqual(Range.Size * (ulong)ranges.Length, bytes.Length);
            var span = MemoryMarshal.Cast<byte, Range>(bytes);
            var newRanges = span.ToArray();
            Assert.AreEqual(ranges, newRanges);
        }

        [Test]
        public static void TestEmptyBFast()
        {
            var buffers = new INamedBuffer[0];
            var bfastBytes = buffers.Pack();
            var bfast = bfastBytes.Unpack();
            Assert.AreEqual(0, bfast.Length);           
        }

        public static byte[] BFastToMemoryStream(IList<INamedBuffer> buffers)
            => buffers.ToBFastData().Write(new System.IO.MemoryStream()).ToArray();        

        [Test]
        public static void TestPackAndUnpack()
        {
            var xs = new[] { 1, 2, 3 };
            var ys = new[] { 3.0, 4.0, 5.0 };
            var xbuff = xs.ToNamedBuffer("xs");
            var ybuff = ys.ToNamedBuffer("ys");
            var bytes = new[] { xbuff, ybuff }.Pack();
            var bfast = bytes.Unpack();
            Assert.AreEqual(2, bfast.Length);
            Assert.AreEqual("xs", bfast[0].Name);
            Assert.AreEqual("ys", bfast[1].Name);
        }

        const int PerformanceIterations = 100;

        public static byte[] PackWithBFastUsingMemoryStream<T>(IList<T[]> arrays) where T: struct
        {
            var buffers = new List<INamedBuffer>();
            for (var i = 0; i < arrays.Count; ++i)
            {
                var buffer = arrays[i].ToNamedBuffer(i.ToString());
                buffers.Add(buffer);
            }
            return BFastToMemoryStream(buffers);
        }

        public static byte[] PackWithoutBuffer(int[] data)
        {
            var bufferSize = data.Length * 4;
            var bytes = new byte[bufferSize * PerformanceIterations];
            for (var i=0; i < PerformanceIterations; ++i)
            {
                // This is slower, because we are converting to bytes, rather than using a span directly
                var buffer = data.ToBytes();
                buffer.CopyTo(bytes, bufferSize * i);
            }
            return bytes;
        }

        public static byte[] PackWithBFast(int[] data)
        {
            var bufferSize = data.Length * 4;
            var buffers = new List<INamedBuffer>();
            for (var i = 0; i < PerformanceIterations; ++i)
            {
                var buffer = data.ToNamedBuffer(i.ToString());
                buffers.Add(buffer);
            }
            var bytes = buffers.Pack();
            Assert.IsTrue(bytes.Length > PerformanceIterations * bufferSize);
            return bytes;
        }

        public static byte[] PackNaively(int[] data)
        {
            var bufferSize = bigArray.Length * 4;
            var buffers = new List<byte[]>();
            for (var i = 0; i < PerformanceIterations; ++i)
            {
                var buffer = bigArray.ToBytes();
                buffers.Add(buffer);
            }
            var bytes = NaivePack(buffers);
            Assert.IsTrue(bytes.Length > PerformanceIterations * bufferSize);
            return bytes;
        }

        public static byte[] NaivePack(IList<byte[]> buffers)
        {
            using (var stream = new MemoryStream())
            using (var bw = new BinaryWriter(stream))
            {
                bw.Write(buffers.Count);
                foreach (var b in buffers)
                {
                    bw.Write(b.Length);
                    bw.Write(b);
                }
                return stream.ToArray();
            }
        }

        public static IList<byte[]> NaiveUnpack(byte[] data)
        {
            var r = new List<byte[]>();
            using (var stream = new MemoryStream())
            using (var br = new BinaryReader(stream))
            {
                var n = br.ReadInt32();
                for (var i = 0; i < n; ++i)
                {
                    var localN = br.ReadInt32();
                    var bytes = br.ReadBytes(localN);
                    r.Add(bytes);
                }
            }
            return r;
        }

        // TODO: demonstrate the speed of creating a BFast as opposed to
        [Test]
        public static void PerformanceTest()
        {
            {
                Console.WriteLine($"Packing with BFast");
                var sw = new Stopwatch();
                sw.Start();
                var bytes = PackWithBFast(bigArray);
                Console.WriteLine($"Created {bytes.Length} bytes in {sw.ElapsedMilliseconds} msec");
            }

            {
                Console.WriteLine($"Packing with BFast using memory stream");
                var sw = new Stopwatch();
                sw.Start();
                var bytes = PackWithBFastUsingMemoryStream(bigArray);
                Console.WriteLine($"Created {bytes.Length} bytes in {sw.ElapsedMilliseconds} msec");
            }

            {
                Console.WriteLine($"Packing big array test");
                var sw = new Stopwatch();
                sw.Start();
                var bytes = PackWithoutBuffer(bigArray);
                Console.WriteLine($"Created {bytes.Length} bytes in {sw.ElapsedMilliseconds} msec");
            }

            {
                var sw = new Stopwatch();
                sw.Start();
                var bytes = PackNaively(bigArray);
                Console.WriteLine($"Created {bytes.Length} bytes in {sw.ElapsedMilliseconds} msec");
            }
        }

        [Test]
        public static void TestAlignment()
        {
            Assert.IsTrue(IsAligned(0));
            for (var i=1ul; i < 32; ++i)
                Assert.IsFalse(IsAligned(i));
            Assert.IsTrue(IsAligned(32));

            for (var i = 1ul; i < 32; ++i)
                Assert.AreEqual(32, ComputePadding(i) + i);
        }

        public static byte GetNthByte(ulong u, int n)
            => (byte)(u >> (n * 8));       

        public static ulong SwapEndianess(ulong u)
        {
            ulong r = 0;
            for (var i=0; i < 8; ++i)
            {
                var b = GetNthByte(u, i);
                var b2 = (ulong)b << (7 - i) * 8;
                r += b2;
            }
            return r;
        }

        [Test]
        public static void TestEndianess()
        {
            ulong magic = Constants.Magic;

            Assert.AreEqual(0xA5, GetNthByte(Constants.SameEndian, 0));
            Assert.AreEqual(0xBF, GetNthByte(Constants.SameEndian, 1));
            for (var i = 2; i < 8; ++i)
                Assert.AreEqual(0x00, GetNthByte(Constants.SameEndian, i));

            Assert.AreEqual(0xA5, GetNthByte(Constants.SwappedEndian, 7));
            Assert.AreEqual(0xBF, GetNthByte(Constants.SwappedEndian, 6));
            for (var i = 5; i >= 0; --i)
                Assert.AreEqual(0x00, GetNthByte(Constants.SwappedEndian, i));

            Assert.AreEqual(Constants.SwappedEndian, SwapEndianess(magic));
            Assert.AreEqual(Constants.SameEndian, SwapEndianess(SwapEndianess(magic)));
        }

        [Test]
        public static void TestHeaderCast()
        {
            var header = new Header();
            header.Magic = Constants.Magic;
            header.DataStart = 64;
            header.DataEnd = 1024;
            header.NumArrays = 12;
            var bytes = header.ToBytes();
            Assert.AreEqual(Header.Size, bytes.Length);
            var ulongs = MemoryMarshal.Cast<byte, ulong>(bytes).ToArray();
            Assert.AreEqual(4, ulongs.Length);
            Assert.AreEqual(header.Magic, ulongs[0]);
            Assert.AreEqual(header.DataStart, ulongs[1]);
            Assert.AreEqual(header.DataEnd, ulongs[2]);
            Assert.AreEqual(header.NumArrays, ulongs[3]);
        }
    }
}
