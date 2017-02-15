namespace MqttBrokerForNet.Domain.Contracts.Handlers
{
    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttConnectionInternalEventHandler
    {
        void ProcessInternalEventQueue(MqttConnection connection);
    }
}