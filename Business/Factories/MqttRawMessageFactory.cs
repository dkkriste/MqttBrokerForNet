namespace MqttBrokerForNet.Business.Factories
{
    using System;

    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttRawMessageFactory : IMqttRawMessageFactory
    {
        public MqttRawMessage CreateRawMessageWithData(
           MqttConnection connection,
           byte messageType,
           byte[] data,
           int dataOffset,
           int dataLength)
        {
            var rawMessage = new MqttRawMessage()
                                 {
                                     PayloadBuffer = new byte[dataLength],
                                     Connection = connection,
                                     MessageType = messageType,
                                     PayloadLength = dataLength
                                 };
            Buffer.BlockCopy(data, dataOffset, rawMessage.PayloadBuffer, 0, dataLength);

            return rawMessage;
        }
    }
}