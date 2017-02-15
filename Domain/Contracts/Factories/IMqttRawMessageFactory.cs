namespace MqttBrokerForNet.Domain.Contracts.Factories
{
    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttRawMessageFactory
    {
        MqttRawMessage CreateRawMessageWithData(
            MqttConnection connection,
            byte messageType,
            byte[] data,
            int dataOffset,
            int dataLength);
    }
}