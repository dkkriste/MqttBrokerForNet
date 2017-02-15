namespace MqttBrokerForNet.Domain.Contracts.Network
{
    using System.Net.Sockets;

    public interface IMqttAsyncTcpSender
    {
        void Send(Socket socket, byte[] message);
    }
}