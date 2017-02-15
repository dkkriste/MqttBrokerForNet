namespace MqttBrokerForNet.Domain.Contracts.Network
{
    public interface IMqttAsyncTcpSocketListener
    {
        void Start();

        void Stop();
    }
}