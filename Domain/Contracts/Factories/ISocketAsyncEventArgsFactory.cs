namespace MqttBrokerForNet.Domain.Contracts.Factories
{
    using System.Net.Sockets;

    public interface ISocketAsyncEventArgsFactory
    {
        SocketAsyncEventArgs[] Create(int numberOfSockets, int bufferBytesAllocatedForEachSaea);
    }
}