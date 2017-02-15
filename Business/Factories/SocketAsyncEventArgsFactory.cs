namespace MqttBrokerForNet.Business.Factories
{
    using System.Net.Sockets;

    using MqttBrokerForNet.Domain.Contracts.Factories;

    public class SocketAsyncEventArgsFactory : ISocketAsyncEventArgsFactory
    {
        public SocketAsyncEventArgs[] Create(int numberOfSockets, int bufferBytesAllocatedForEachSaea)
        {
            var totalBytesInBufferBlock = numberOfSockets * bufferBytesAllocatedForEachSaea;
            var bufferBlock = new byte[totalBytesInBufferBlock];
            var sockets = new SocketAsyncEventArgs[numberOfSockets];
            for (var i = 0; i < numberOfSockets; i++)
            {
                sockets[i] = new SocketAsyncEventArgs();
                sockets[i].SetBuffer(bufferBlock, i * bufferBytesAllocatedForEachSaea, bufferBytesAllocatedForEachSaea);
            }

            return sockets;
        }
    }
}