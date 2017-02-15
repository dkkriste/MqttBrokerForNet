namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Messages;

    public interface IMqttPublishingManager
    {
        void Publish(MqttMsgPublish publish);

        void PublishSession(string clientId);

        void ProcessSessionsQueue();

        void ProcessPublishQueue();

        void ProcessSessionPublishQueue();
    }
}