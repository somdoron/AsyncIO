using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageScale.AsyncIO
{
    public abstract class Buffer : IDisposable
    {        
        public Buffer(byte[] data)
        {
            Data = data;
        }

        public abstract void Dispose();

        public static Buffer Create(byte[] data)
        {
            return new Windows.PinnedBuffer(data);
        }

        public byte[] Data { get; private set; }   
    }
}
