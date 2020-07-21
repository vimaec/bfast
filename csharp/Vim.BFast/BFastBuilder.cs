using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Vim.BFast
{
    /// <summary>
    /// Anything that can be add to a BFAST has to be abled to compute its size, and write to a stream.
    /// </summary>
    public interface IBFastComponent
    {
        long GetSize();
        void Write(Stream stream);
    }

    /// <summary>
    /// A wrapper around a buffer so that it can be used as a BFAST component 
    /// </summary>
    public class BufferAsBFastComponent : IBFastComponent
    {
        public BufferAsBFastComponent(IBuffer buffer)
            => Buffer = buffer;
        public IBuffer Buffer { get; }
        public void Write(Stream stream) => stream.Write(Buffer);
        public long GetSize() => Buffer.NumBytes();
    }

    /// <summary>
    /// Used to build BFASTs incrementally that contain named buffers and/or other BFASTs. 
    /// </summary>
    public class BFastBuilder : IBFastComponent
    {
        public BFastHeader Header { get; private set; }
        public long GetSize() => GetOrComputeHeader().Preamble.DataEnd;

        private List<(string, IBFastComponent)> _children = new List<(string, IBFastComponent)>();

        public void Write(Stream stream)
            => stream.WriteBFastBody(GetOrComputeHeader(), BufferNames().ToArray(), BufferSizes().ToArray(), OnBuffer);

        public void OnBuffer(Stream stream, int index, string name, long size)
        {
            (string bufferName, object x) = _children[index];
            Debug.Assert(name == bufferName);
            Debug.Assert(size != GetSize());
            if (x is BFastBuilder bb)
                bb.Write(stream);
            if (x is IBuffer b)
                stream.Write(b);
        }

        private BFastHeader GetOrComputeHeader()
            => Header ?? (Header = BFast.CreateBFastHeader(
                BufferSizes().ToArray(), BufferNames().ToArray()));

        private BFastBuilder _add(string name, IBFastComponent component)
        {
            Header = null;
            _children.Add((name, component));
            return this;
        }

        public BFastBuilder Add(string name, IBFastComponent component)
            => _add(name, component);

        public BFastBuilder Add(string name, IBuffer buffer)
            => _add(name, new BufferAsBFastComponent(buffer));

        public BFastBuilder Add(INamedBuffer buffer)
            => Add(buffer.Name, buffer);

        public BFastBuilder Add(IEnumerable<INamedBuffer> buffers)
            => buffers.Aggregate(this, (x, y) => x.Add(y));

        public BFastBuilder Add(string name, IEnumerable<INamedBuffer> buffers)
            => Add(name, new BFastBuilder().Add(buffers));

        public IEnumerable<string> BufferNames()
            => _children.Select(x => x.Item1);

        public IEnumerable<long> BufferSizes()
            => _children.Select(x => x.Item2.GetSize());
    }
}
