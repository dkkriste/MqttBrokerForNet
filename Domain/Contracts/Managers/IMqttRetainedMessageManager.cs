namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Messages;

    public interface IMqttRetainedMessageManager
    {
        void CheckForAndSetRetainedMessage(MqttMsgPublish publish);

        void PublishRetaind(string topic, MqttConnection connection);

        void ProcessSubscribersForRetainedQueue();
    }
}