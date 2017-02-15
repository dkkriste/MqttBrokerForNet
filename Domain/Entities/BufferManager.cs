namespace MqttBrokerForNet.Domain.Entities
{
    using System;
    using System.Net.Sockets;

    public class BufferManager
    {
        private readonly byte[] bufferBlock;

        private readonly int totalBytesInBufferBlock;

        private readonly int bufferBytesAllocatedForEachSaea;

        private int nextAvailableIndex;

        public BufferManager(int numberOfBuffers, int bufferBytesAllocatedForEachSaea)
        {
            this.bufferBytesAllocatedForEachSaea = bufferBytesAllocatedForEachSaea;
            this.totalBytesInBufferBlock = numberOfBuffers * bufferBytesAllocatedForEachSaea;
            this.bufferBlock = new byte[this.totalBytesInBufferBlock];
            this.nextAvailableIndex = 0;
        }

        public void SetBuffer(SocketAsyncEventArgs args)
        {
            if (this.nextAvailableIndex >= this.totalBytesInBufferBlock)
            {
                throw new Exception("Buffer is full");
            }

            args.SetBuffer(this.bufferBlock, this.nextAvailableIndex, this.bufferBytesAllocatedForEachSaea);
            this.nextAvailableIndex += this.bufferBytesAllocatedForEachSaea;
        }
    }
}