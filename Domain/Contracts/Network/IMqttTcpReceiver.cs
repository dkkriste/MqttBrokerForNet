namespace MqttBrokerForNet.Domain.Contracts.Network
{
    using System.Net.Sockets;

    public interface IMqttTcpReceiver
    {
        void ReceiveCompleted(object sender, SocketAsyncEventArgs e);

        void StartReceive(SocketAsyncEventArgs receiveEventArgs);
    }
}