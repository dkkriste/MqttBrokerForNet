namespace MqttBrokerForNet.Domain.Contracts.Handlers
{
    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttIncommingMessageHandler
    {
        void ProcessReceivedMessage(MqttRawMessage rawMessage);

        ushort Publish(MqttConnection connection, string topic, byte[] message, byte qosLevel, bool retain);
    }
}