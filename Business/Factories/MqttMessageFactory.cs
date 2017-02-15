namespace MqttBrokerForNet.Business.Factories
{
    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Entities.Enums;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttMessageFactory : IMqttMessageFactory
    {
        public MqttMsgConnack CreateConnack(MqttProtocolVersion proctocolVersion, byte returnCode, bool sessionPresent)
        {
            var connack = new MqttMsgConnack { ReturnCode = returnCode };
            if (proctocolVersion == MqttProtocolVersion.Version_3_1_1)
            {
                connack.SessionPresent = sessionPresent;
            }

            return connack;
        }

        public MqttMsgPingResp CreatePingResp()
        {
            return new MqttMsgPingResp();
        }

        public MqttMsgPuback CreatePuback(ushort messageId)
        {
            return new MqttMsgPuback { MessageId = messageId };
        }

        public MqttMsgPubcomp CreatePubcomp(ushort messageId)
        {
            return new MqttMsgPubcomp { MessageId = messageId };
        }

        public MqttMsgPubrec CreatePubrec(ushort messageId)
        {
            return new MqttMsgPubrec { MessageId = messageId};
        }

        public MqttMsgPubrel CreatePubrel(ushort messageId, bool duplicate)
        {
            return new MqttMsgPubrel { MessageId = messageId, DupFlag = duplicate };
        }

        public MqttMsgSuback CreateSuback(ushort messageId, byte[] grantedQosLevels)
        {
            return new MqttMsgSuback { MessageId = messageId, GrantedQoSLevels = grantedQosLevels };
        }

        public MqttMsgUnsuback CreateUnsuback(ushort messageId)
        {
            return new MqttMsgUnsuback { MessageId = messageId };
        }
    }
}