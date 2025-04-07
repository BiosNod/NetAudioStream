using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamingApplication
{
    public class BufferPool
    {
        public static readonly BufferPool _bufferPool = new BufferPool();

        private readonly ConcurrentQueue<byte[]> _byteBuffers = new();
        public readonly ConcurrentQueue<short[]> _shortBuffers = new();

        public byte[] RentByteBuffer(int minSize)
        {
            if (_byteBuffers.TryDequeue(out var buf) && buf.Length >= minSize)
                return buf;
            return new byte[minSize];
        }

        public short[] RentShortBuffer(int minSize)
        {
            if (_shortBuffers.TryDequeue(out var buf) && buf.Length >= minSize)
                return buf;
            return new short[minSize];
        }

        public void Return(byte[] buffer) => _byteBuffers.Enqueue(buffer);
        public void Return(short[] buffer) => _shortBuffers.Enqueue(buffer);
    }
}
