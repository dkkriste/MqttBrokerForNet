namespace MqttBrokerForNet.Domain.Contracts.Handlers
{
    using MqttBrokerForNet.Domain.Events;

    public interface IMqttConnectionInflightHandler
    {
        void ProcessInflightQueue(InflightQueueProcessEvent processEvent);
    }
}