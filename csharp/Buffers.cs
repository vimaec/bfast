using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Vim
{
    /// <summary>
    /// Wraps an object that provides a span of bytes, for example a Memory object.
    /// This is useful because we cannot have a collection of Span[byte], but 
    /// we can have a collection of IBuffer. 
    /// </summary>
    public interface IBuffer
    {
        Span<byte> Bytes { get; }
    }

    /// <summary>
    /// Represents a buffer associated with a string name. 
    /// </summary>
    public interface INamedBuffer : IBuffer
    {
        string Name { get; }
    }

    /// <summary>
    /// A memory buffer is a concrete implementation of IBuffer
    /// </summary>
    public class MemoryBuffer<T> : IBuffer where T : struct
    {
        public MemoryBuffer(Memory<T> memory) => Memory = memory;
        public Memory<T> Memory { get; }
        public Span<byte> Bytes => MemoryMarshal.Cast<T, byte>(Memory.Span);
    }

    /// <summary>
    /// Associates a buffer implementation with a name.
    /// </summary>
    public class NamedBuffer : INamedBuffer
    {
        public NamedBuffer(IBuffer buffer, string name = "")
        {
            Buffer = buffer;
            Name = name;
        }

        public IBuffer Buffer { get; }
        public Span<byte> Bytes => Buffer.Bytes;
        public string Name { get; }
    }

    /// <summary>
    /// Helper functions for working with buffers 
    /// </summary>
    public static class BufferExtensions
    {
        public static IBuffer ToBuffer<T>(this Span<T> span) where T : struct
            => span.ToArray().ToBuffer();

        public static IBuffer ToBuffer<T>(this Memory<T> memory) where T : struct
            => new MemoryBuffer<T>(memory);

        public static IBuffer ToBuffer<T>(this T[] xs) where T : struct
            => new Memory<T>(xs).ToBuffer();

        public static IBuffer ToBuffer(this string self)
            => System.Text.Encoding.UTF8.GetBytes(self).ToBuffer();

        public static IBuffer ToBuffer(this IEnumerable<string> strings)
            => string.Join("\0", strings).ToBuffer();

        public static INamedBuffer ToNamedBuffer<T>(this T[] xs, string name = "") where T: struct
            => xs.ToBuffer().ToNamedBuffer(name);

        public static INamedBuffer ToNamedBuffer(this IBuffer buffer, string name = "")
            => new NamedBuffer(buffer, name);

        public static INamedBuffer ToNamedBuffer<T>(this Span<T> span, string name = "") where T : struct
            => span.ToBuffer().ToNamedBuffer(name);

        public static INamedBuffer ToNamedBuffer<T>(this Memory<T> memory, string name = "") where T : struct
            => memory.ToBuffer().ToNamedBuffer(name);

        public static INamedBuffer ToNamedBuffer(this string self, string name = "")
            => self.ToBuffer().ToNamedBuffer(name);

        public static IBuffer ToNamedBuffer(this IEnumerable<string> strings, string name = "")
            => strings.ToBuffer().ToNamedBuffer(name);

        public static IEnumerable<INamedBuffer> ToNamedBuffers(this IEnumerable<IBuffer> buffers, IEnumerable<string> names = null)
            => names == null ? buffers.Select(b => b.ToNamedBuffer("")) : buffers.Zip(names, ToNamedBuffer);

        public static IDictionary<string, INamedBuffer> ToDictionary(this IEnumerable<INamedBuffer> buffers)
            => buffers.ToDictionary(b => b.Name, b => b);

        public static Memory<byte> ToMemory(this IBuffer buffer)
            => buffer is MemoryBuffer<byte> mb ? mb.Memory : new Memory<byte>(buffer.Bytes.ToArray());

        public static IEnumerable<INamedBuffer> ToNamedBuffers(this IDictionary<string, IBuffer> d)
            => d.Select(kv => kv.Value.ToNamedBuffer(kv.Key));

        public static IEnumerable<INamedBuffer> ToNamedBuffers(this IDictionary<string, byte[]> d)
            => d.Select(kv => kv.Value.ToNamedBuffer(kv.Key));

        public static Span<T> AsSpan<T>(this IBuffer buffer) where T : struct
            => MemoryMarshal.Cast<byte, T>(buffer.Bytes);

        public static string GetString(this IBuffer buffer)
            => System.Text.Encoding.UTF8.GetString(buffer.Bytes.ToArray());

        public static string[] GetStrings(this IBuffer buffer)
            => buffer.GetString().Split('\0');

        public static byte[] ToBytes<T>(this T[] xs) where T : struct
            => MemoryMarshal.Cast<T, byte>(xs).ToArray();

        public static byte[] ToBytes<T>(this T x) where T : struct
            => ToBytes(new[] { x });
    }
}