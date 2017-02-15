namespace MqttBrokerForNet.Business.Handlers
{
    using System;
    using System.Linq;

    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Exceptions;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttIncommingMessageHandler : IMqttIncommingMessageHandler
    {
        private readonly IMqttOutgoingMessageHandler outgoingMessageHandler;

        public MqttIncommingMessageHandler(IMqttOutgoingMessageHandler outgoingMessageHandler)
        {
            this.outgoingMessageHandler = outgoingMessageHandler;
        }

        #region Public Methods and Operators

        public void ProcessReceivedMessage(MqttRawMessage rawMessage)
        {
            if (!rawMessage.Connection.IsRunning)
            {
                return;
            }

            // update last message received ticks
            rawMessage.Connection.LastCommunicationTime = Environment.TickCount;

            // extract message type from received byte
            var msgType = (byte)((rawMessage.MessageType & MqttMsgBase.MSG_TYPE_MASK) >> MqttMsgBase.MSG_TYPE_OFFSET);
            var protocolVersion = (byte)rawMessage.Connection.ProtocolVersion;
            switch (msgType)
            {
                case MqttMsgBase.MQTT_MSG_CONNECT_TYPE:
                    var connect = MqttMsgConnect.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer);
                    rawMessage.Connection.EnqueueInternalEvent(new InternalEvent(connect));
                    break;

                case MqttMsgBase.MQTT_MSG_PINGREQ_TYPE:
                    var pingReqest = MqttMsgPingReq.Parse(rawMessage.MessageType, protocolVersion);
                    outgoingMessageHandler.PingResp(rawMessage.Connection);
                    break;

                case MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE:
                    var subscribe = MqttMsgSubscribe.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer,
                        rawMessage.PayloadLength);
                    rawMessage.Connection.EnqueueInternalEvent(new InternalEvent(subscribe));
                    break;

                case MqttMsgBase.MQTT_MSG_PUBLISH_TYPE:
                    var publish = MqttMsgPublish.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer,
                        rawMessage.PayloadLength);
                    EnqueueInflight(rawMessage.Connection, publish, MqttMsgFlow.ToAcknowledge);
                    break;

                case MqttMsgBase.MQTT_MSG_PUBACK_TYPE:
                    // enqueue PUBACK message received (for QoS Level 1) into the internal queue
                    var puback = MqttMsgPuback.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer);
                    EnqueueInternal(rawMessage.Connection, puback);
                    break;

                case MqttMsgBase.MQTT_MSG_PUBREC_TYPE:
                    // enqueue PUBREC message received (for QoS Level 2) into the internal queue
                    var pubrec = MqttMsgPubrec.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer);
                    EnqueueInternal(rawMessage.Connection, pubrec);
                    break;

                case MqttMsgBase.MQTT_MSG_PUBREL_TYPE:
                    // enqueue PUBREL message received (for QoS Level 2) into the internal queue
                    var pubrel = MqttMsgPubrel.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer);
                    EnqueueInternal(rawMessage.Connection, pubrel);
                    break;

                case MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE:
                    // enqueue PUBCOMP message received (for QoS Level 2) into the internal queue
                    var pubcomp = MqttMsgPubcomp.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer);
                    EnqueueInternal(rawMessage.Connection, pubcomp);
                    break;

                case MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE:
                    var unsubscribe = MqttMsgUnsubscribe.Parse(
                        rawMessage.MessageType,
                        protocolVersion,
                        rawMessage.PayloadBuffer,
                        rawMessage.PayloadLength);
                    rawMessage.Connection.EnqueueInternalEvent(new InternalEvent(unsubscribe));
                    break;

                case MqttMsgBase.MQTT_MSG_DISCONNECT_TYPE:
                    var disconnect = MqttMsgDisconnect.Parse(rawMessage.MessageType, protocolVersion);
                    rawMessage.Connection.EnqueueInternalEvent(new InternalEvent(disconnect));
                    break;

                case MqttMsgBase.MQTT_MSG_CONNACK_TYPE:
                case MqttMsgBase.MQTT_MSG_PINGRESP_TYPE:
                case MqttMsgBase.MQTT_MSG_SUBACK_TYPE:
                case MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE:
                    throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);

                default:
                    throw new MqttClientException(MqttClientErrorCode.WrongBrokerMessage);
            }
        }

        public ushort Publish(
            MqttConnection connection,
            string topic,
            byte[] message,
            byte qosLevel,
            bool retain)
        {
            var publish = new MqttMsgPublish(topic, message, false, qosLevel, retain);
            publish.MessageId = connection.GetMessageId();

            // enqueue message to publish into the inflight queue
            EnqueueInflight(connection, publish, MqttMsgFlow.ToPublish);

            return publish.MessageId;
        }

        #endregion

        #region Methods

        private void EnqueueInflight(MqttConnection connection, MqttMsgBase msg, MqttMsgFlow flow)
        {
            // enqueue is needed (or not)
            var enqueue = true;

            // if it is a PUBLISH message with QoS Level 2
            if (msg.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE && msg.QosLevel == MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE)
            {
                // if it is a PUBLISH message already received (it is in the inflight queue), the publisher
                // re-sent it because it didn't received the PUBREC. In connection case, we have to re-send PUBREC

                // NOTE : I need to find on message id and flow because the broker could be publish/received
                // to/from client and message id could be the same (one tracked by broker and the other by client)
                var msgCtxFinder = new MqttMsgContextFinder(msg.MessageId, MqttMsgFlow.ToAcknowledge);
                var msgCtx = (MqttMsgContext)connection.InflightQueue.FirstOrDefault(msgCtxFinder.Find);

                // the PUBLISH message is alredy in the inflight queue, we don't need to re-enqueue but we need
                // to change state to re-send PUBREC
                if (msgCtx != null)
                {
                    msgCtx.State = MqttMsgState.QueuedQos2;
                    msgCtx.Flow = MqttMsgFlow.ToAcknowledge;
                    enqueue = false;
                }
            }

            if (enqueue)
            {
                // set a default state
                var state = MqttMsgState.QueuedQos0;

                // based on QoS level, the messages flow between broker and client changes
                switch (msg.QosLevel)
                {
                    // QoS Level 0
                    case MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE:
                        state = MqttMsgState.QueuedQos0;
                        break;

                    // QoS Level 1
                    case MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE:
                        state = MqttMsgState.QueuedQos1;
                        break;

                    // QoS Level 2
                    case MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE:
                        state = MqttMsgState.QueuedQos2;
                        break;
                }

                // [v3.1.1] SUBSCRIBE and UNSUBSCRIBE aren't "officially" QOS = 1
                // so QueuedQos1 state isn't valid for them
                if (msg.Type == MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE)
                {
                    state = MqttMsgState.SendSubscribe;
                }
                else if (msg.Type == MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE)
                {
                    state = MqttMsgState.SendUnsubscribe;
                }

                // queue message context
                var msgContext = new MqttMsgContext()
                                                {
                                                    Message = msg,
                                                    State = state,
                                                    Flow = flow,
                                                    Attempt = 0
                                                };

                // enqueue message and unlock send thread
                connection.EnqueueInflight(msgContext);

                // PUBLISH message
                if (msg.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE)
                {
                    if (msgContext.Flow == MqttMsgFlow.ToPublish
                        && (msg.QosLevel == MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE
                            || msg.QosLevel == MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE))
                    {
                        if (connection.Session != null)
                        {
                            connection.Session.InflightMessages.TryAdd(msgContext.Key, msgContext);
                        }
                    }

                    // to acknowledge and QoS level 2
                    else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge
                             && msg.QosLevel == MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE)
                    {
                        if (connection.Session != null)
                        {
                            connection.Session.InflightMessages.TryAdd(msgContext.Key, msgContext);
                        }
                    }
                }
            }
        }

        private void EnqueueInternal(MqttConnection connection, MqttMsgBase msg)
        {
            // enqueue is needed (or not)
            var enqueue = true;

            // if it is a PUBREL message (for QoS Level 2)
            if (msg.Type == MqttMsgBase.MQTT_MSG_PUBREL_TYPE)
            {
                // if it is a PUBREL but the corresponding PUBLISH isn't in the inflight queue,
                // it means that we processed PUBLISH message and received PUBREL and we sent PUBCOMP
                // but publisher didn't receive PUBCOMP so it re-sent PUBREL. We need only to re-send PUBCOMP.

                // NOTE : I need to find on message id and flow because the broker could be publish/received
                // to/from client and message id could be the same (one tracked by broker and the other by client)
                var msgCtxFinder = new MqttMsgContextFinder(msg.MessageId, MqttMsgFlow.ToAcknowledge);
                var msgCtx = (MqttMsgContext)connection.InflightQueue.FirstOrDefault(msgCtxFinder.Find);

                // the PUBLISH message isn't in the inflight queue, it was already processed so
                // we need to re-send PUBCOMP only
                if (msgCtx == null)
                {
                    outgoingMessageHandler.Pubcomp(connection, msg.MessageId);
                    enqueue = false;
                }
            }

            // if it is a PUBCOMP message (for QoS Level 2)
            else if (msg.Type == MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE)
            {
                // if it is a PUBCOMP but the corresponding PUBLISH isn't in the inflight queue,
                // it means that we sent PUBLISH message, sent PUBREL (after receiving PUBREC) and already received PUBCOMP
                // but publisher didn't receive PUBREL so it re-sent PUBCOMP. We need only to ignore connection PUBCOMP.

                // NOTE : I need to find on message id and flow because the broker could be publish/received
                // to/from client and message id could be the same (one tracked by broker and the other by client)
                var msgCtxFinder = new MqttMsgContextFinder(msg.MessageId, MqttMsgFlow.ToPublish);
                var msgCtx = (MqttMsgContext)connection.InflightQueue.FirstOrDefault(msgCtxFinder.Find);

                // the PUBLISH message isn't in the inflight queue, it was already sent so we need to ignore connection PUBCOMP
                if (msgCtx == null)
                {
                    enqueue = false;
                }
            }

            // if it is a PUBREC message (for QoS Level 2)
            else if (msg.Type == MqttMsgBase.MQTT_MSG_PUBREC_TYPE)
            {
                // if it is a PUBREC but the corresponding PUBLISH isn't in the inflight queue,
                // it means that we sent PUBLISH message more times (retries) but broker didn't send PUBREC in time
                // the publish is failed and we need only to ignore rawMessage.Connection PUBREC.

                // NOTE : I need to find on message id and flow because the broker could be publish/received
                // to/from client and message id could be the same (one tracked by broker and the other by client)
                var msgCtxFinder = new MqttMsgContextFinder(msg.MessageId, MqttMsgFlow.ToPublish);
                var msgCtx = (MqttMsgContext)connection.InflightQueue.FirstOrDefault(msgCtxFinder.Find);

                // the PUBLISH message isn't in the inflight queue, it was already sent so we need to ignore rawMessage.Connection PUBREC
                if (msgCtx == null)
                {
                    enqueue = false;
                }
            }

            if (enqueue)
            {
                connection.InternalQueue.Enqueue(msg);
            }
        }

        #endregion

        /// <summary>
        /// Finder class for PUBLISH message inside a queue
        /// </summary>
        internal class MqttMsgContextFinder
        {
            #region Constructors and Destructors

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="messageId">Message Id</param>
            /// <param name="flow">Message flow inside inflight queue</param>
            internal MqttMsgContextFinder(ushort messageId, MqttMsgFlow flow)
            {
                MessageId = messageId;
                Flow = flow;
            }

            #endregion

            #region Properties

            // message flow into inflight queue
            internal MqttMsgFlow Flow { get; set; }

            // PUBLISH message id
            internal ushort MessageId { get; set; }

            #endregion

            #region Methods

            internal bool Find(object item)
            {
                var msgCtx = (MqttMsgContext)item;
                return msgCtx.Message.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE
                       && msgCtx.Message.MessageId == MessageId && msgCtx.Flow == Flow;
            }

            #endregion
        }
    }
}