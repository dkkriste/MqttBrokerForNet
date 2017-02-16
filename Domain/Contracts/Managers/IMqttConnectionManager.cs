namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Messages;

    public interface IMqttConnectionManager
    {
        int AssignedClients { get; }

        void EnqueueRawMessageForProcessing(MqttRawMessage rawMessage);

        void EnqueueClientConnectionWithInternalEventQueueToProcess(MqttConnection connection);

        void EnqueueClientConnectionWithInflightQueueToProcess(InflightQueueProcessEvent processEvent);

        void ProcessRawMessageQueue();

        void ProcessInflightQueue();

        void ProcessInternalEventQueue();

        void OnConnectionClosed(MqttConnection connection);

        void OnMqttMsgConnected(MqttConnection connection, MqttMsgConnect message);

        void OnMqttMsgPublishReceived(MqttConnection connection, MqttMsgPublish msg);

        void OnMqttMsgSubscribeReceived(MqttConnection connection, ushort messageId, string[] topics, byte[] qosLevels);

        void OpenClientConnection(MqttConnection connection);
    }
}