namespace MqttBrokerForNet.Domain.Contracts.Factories
{
    using System.Net.Sockets;

    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttConnectionFactory
    {
        MqttConnection[] Create(int numberOfConnection, int bufferBytesAllocatedForEachSaea);
    }
}