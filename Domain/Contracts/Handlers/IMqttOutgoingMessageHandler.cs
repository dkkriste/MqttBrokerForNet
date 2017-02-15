namespace MqttBrokerForNet.Domain.Contracts.Handlers
{
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Messages;

    public interface IMqttOutgoingMessageHandler
    {
        void Connack(
            MqttConnection connection,
            MqttMsgConnect connect,
            byte returnCode,
            string clientId,
            bool sessionPresent);

        void PingResp(MqttConnection connection);

        void Puback(MqttConnection connection, ushort messageId);

        void Pubcomp(MqttConnection connection, ushort messageId);

        void Pubrec(MqttConnection connection, ushort messageId);

        void Pubrel(MqttConnection connection, ushort messageId, bool duplicate);

        void Send(MqttConnection connection, MqttMsgBase msg);

        void Suback(MqttConnection connection, ushort messageId, byte[] grantedQosLevels);

        void Unsuback(MqttConnection connection, ushort messageId);
    }
}