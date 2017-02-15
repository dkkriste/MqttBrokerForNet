namespace MqttBrokerForNet.Business.Handlers
{
    using MqttBrokerForNet.Business.Managers;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttConnectionInternalEventHandler : IMqttConnectionInternalEventHandler
    {
        private readonly IMqttOutgoingMessageHandler outgoingMessageHandler;

        private readonly IMqttSubscriptionManager subscriptionManager;

        public MqttConnectionInternalEventHandler(IMqttOutgoingMessageHandler outgoingMessageHandler, IMqttSubscriptionManager subscriptionManager)
        {
            this.outgoingMessageHandler = outgoingMessageHandler;
            this.subscriptionManager = subscriptionManager;
        }

        #region Public Methods and Operators

        public void ProcessInternalEventQueue(MqttConnection connection)
        {
            if (!connection.IsRunning)
            {
                return;
            }

            if (connection.EventQueue.TryDequeue(out InternalEvent internalEvent))
            {
                var msg = internalEvent.Message;
                if (msg != null)
                {
                    switch (msg.Type)
                    {
                        // CONNECT message received
                        case MqttMsgBase.MQTT_MSG_CONNECT_TYPE:
                            connection.OnMqttMsgConnected((MqttMsgConnect)msg);
                            break;

                        // SUBSCRIBE message received
                        case MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE:
                            var subscribe = (MqttMsgSubscribe)msg;
                            connection.OnMqttMsgSubscribeReceived(
                                subscribe.MessageId,
                                subscribe.Topics,
                                subscribe.QoSLevels);
                            break;

                        // SUBACK message received
                        case MqttMsgBase.MQTT_MSG_SUBACK_TYPE:
                            OnMqttMsgSubscribed(connection, (MqttMsgSuback)msg);
                            break;

                        // PUBLISH message received
                        case MqttMsgBase.MQTT_MSG_PUBLISH_TYPE:
                            // PUBLISH message received in a published internal event, no publish succeeded
                            if (internalEvent.GetType() == typeof(PublishedInternalEvent))
                            {
                                OnMqttMsgPublished(connection, msg.MessageId, false);
                            }
                            else
                            {
                                connection.OnMqttMsgPublishReceived((MqttMsgPublish)msg);
                            }

                            break;

                        // (PUBACK received for QoS Level 1)
                        case MqttMsgBase.MQTT_MSG_PUBACK_TYPE:
                            OnMqttMsgPublished(connection, msg.MessageId, true);
                            break;

                        // (PUBREL received for QoS Level 2)
                        case MqttMsgBase.MQTT_MSG_PUBREL_TYPE:
                            connection.OnMqttMsgPublishReceived((MqttMsgPublish)msg);
                            break;

                        // (PUBCOMP received for QoS Level 2)
                        case MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE:
                            OnMqttMsgPublished(connection, msg.MessageId, true);
                            break;

                        // UNSUBSCRIBE message received from client
                        case MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE:
                            var unsubscribe = (MqttMsgUnsubscribe)msg;
                            OnMqttMsgUnsubscribeReceived(connection, unsubscribe.MessageId, unsubscribe.Topics);
                            break;

                        // UNSUBACK message received
                        case MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE:
                            OnMqttMsgUnsubscribed(connection, msg.MessageId);
                            break;

                        // DISCONNECT message received from client
                        case MqttMsgBase.MQTT_MSG_DISCONNECT_TYPE:
                            OnMqttMsgDisconnected(connection);
                            break;
                    }
                }
            }

            // all events for received messages dispatched, check if there is closing connection
            if (connection.EventQueue.Count == 0 && connection.IsConnectionClosing)
            {
                connection.OnConnectionClosed();
            }
        }

        #endregion

        #region Methods

        private void OnMqttMsgDisconnected(MqttConnection connection)
        {
            // close the client
            connection.OnConnectionClosed();
        }

        private void OnMqttMsgPublished(MqttConnection connection, ushort messageId, bool isPublished)
        {
            // TODO
        }

        private void OnMqttMsgSubscribed(MqttConnection connection, MqttMsgSuback suback)
        {
        }

        private void OnMqttMsgUnsubscribed(MqttConnection connection, ushort messageId)
        {
        }

        private void OnMqttMsgUnsubscribeReceived(MqttConnection connection, ushort messageId, string[] topics)
        {
            for (var i = 0; i < topics.Length; i++)
            {
                subscriptionManager.Unsubscribe(topics[i], connection);
            }

            // send UNSUBACK message to the client
            outgoingMessageHandler.Unsuback(connection, messageId);
        }

        #endregion
    }
}