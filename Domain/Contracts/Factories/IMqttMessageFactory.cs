namespace MqttBrokerForNet.Domain.Contracts.Factories
{
    using MqttBrokerForNet.Domain.Entities.Enums;
    using MqttBrokerForNet.Domain.Messages;

    public interface IMqttMessageFactory
    {
        MqttMsgConnack CreateConnack(MqttProtocolVersion proctocolVersion, byte returnCode, bool sessionPresent);

        MqttMsgPingResp CreatePingResp();

        MqttMsgPuback CreatePuback(ushort messageId);

        MqttMsgPubcomp CreatePubcomp(ushort messageId);

        MqttMsgPubrec CreatePubrec(ushort messageId);

        MqttMsgPubrel CreatePubrel(ushort messageId, bool duplicate);

        MqttMsgSuback CreateSuback(ushort messageId, byte[] grantedQosLevels);

        MqttMsgUnsuback CreateUnsuback(ushort messageId);
    }
}