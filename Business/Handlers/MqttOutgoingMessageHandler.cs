namespace MqttBrokerForNet.Business.Handlers
{
    using System;

    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttOutgoingMessageHandler : IMqttOutgoingMessageHandler
    {
        private readonly IMqttMessageFactory messageFactory;

        private readonly IMqttAsyncTcpSender mqttAsyncTcpSender;

        public MqttOutgoingMessageHandler(IMqttMessageFactory messageFactory, IMqttAsyncTcpSender mqttAsyncTcpSender)
        {
            this.messageFactory = messageFactory;
            this.mqttAsyncTcpSender = mqttAsyncTcpSender;
        }

        #region Public Methods and Operators

        public void Connack(
            MqttConnection connection,
            MqttMsgConnect connect,
            byte returnCode,
            string clientId,
            bool sessionPresent)
        {
            connection.LastCommunicationTime = Environment.TickCount;

            var connack = messageFactory.CreateConnack(connection.ProtocolVersion, returnCode, sessionPresent);

            Send(connection, connack);

            // connection accepted, start keep alive thread checking
            if (returnCode == MqttMsgConnack.CONN_ACCEPTED)
            {
                // [v3.1.1] if client id isn't null, the CONNECT message has a cliend id with zero bytes length
                // and broker assigned a unique identifier to the client
                connection.ClientId = clientId ?? connect.ClientId;
                connection.CleanSession = connect.CleanSession;
                connection.WillFlag = connect.WillFlag;
                connection.WillTopic = connect.WillTopic;
                connection.WillMessage = connect.WillMessage;
                connection.WillQosLevel = connect.WillQosLevel;

                connection.KeepAlivePeriod = connect.KeepAlivePeriod * 1000; // convert in ms

                // broker has a tolerance of 1.5 specified keep alive period
                connection.KeepAlivePeriod += connection.KeepAlivePeriod / 2;

                connection.IsConnectionClosing = false;
                connection.IsConnected = true;
            }

            // connection refused, close TCP/IP channel
            else
            {
                connection.OnConnectionClosed();
            }
        }

        public void PingResp(MqttConnection connection)
        {
            var pingresp = messageFactory.CreatePingResp();
            Send(connection, pingresp);
        }

        public void Puback(MqttConnection connection, ushort messageId)
        {
            var puback = messageFactory.CreatePuback(messageId);
            Send(connection, puback);
        }

        public void Pubcomp(MqttConnection connection, ushort messageId)
        {
            var pubcomp = messageFactory.CreatePubcomp(messageId);
            Send(connection, pubcomp);
        }

        public void Pubrec(MqttConnection connection, ushort messageId)
        {
            var pubrec = messageFactory.CreatePubrec(messageId);
            Send(connection, pubrec);
        }

        public void Pubrel(MqttConnection connection, ushort messageId, bool duplicate)
        {
            var pubrel = messageFactory.CreatePubrel(messageId, duplicate);
            Send(connection, pubrel);
        }

        public void Send(MqttConnection connection, MqttMsgBase msg)
        {
            Send(connection, msg.GetBytes((byte)connection.ProtocolVersion));
        }

        public void Suback(MqttConnection connection, ushort messageId, byte[] grantedQosLevels)
        {
            var suback = messageFactory.CreateSuback(messageId, grantedQosLevels);
            Send(connection, suback);
        }

        public void Unsuback(MqttConnection connection, ushort messageId)
        {
            var unsuback = messageFactory.CreateUnsuback(messageId);
            Send(connection, unsuback);
        }

        private void Send(MqttConnection connection, byte[] msgBytes)
        {
            try
            {
                mqttAsyncTcpSender.Send(connection.ReceiveSocketAsyncEventArgs.AcceptSocket, msgBytes);
            }
            catch (Exception)
            {
                //TODO
            }
        }

        #endregion
    }
}