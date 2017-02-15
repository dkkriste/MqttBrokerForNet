namespace MqttBrokerForNet.Business.Handlers
{
    using System;
    using System.Threading.Tasks;

    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Entities.Enums;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttConnectionInflightHandler : IMqttConnectionInflightHandler
    {
        private readonly IMqttOutgoingMessageHandler outgoingMessageHandler;

        public MqttConnectionInflightHandler(IMqttOutgoingMessageHandler outgoingMessageHandler)
        {
            this.outgoingMessageHandler = outgoingMessageHandler;
        }

        #region Public Methods and Operators

        public void ProcessInflightQueue(InflightQueueProcessEvent processEvent)
        {
            var clientConnection = processEvent.Connection;
            if (!clientConnection.IsRunning)
            {
                return;
            }

            // Check if queue has been processed since callback was created
            if (processEvent.IsCallback
                && clientConnection.InflightQueueLastProcessedTime != processEvent.CallbackCreationTime)
            {
                return;
            }

            MqttMsgBase msgReceived = null;
            var msgReceivedProcessed = false;
            var timeout = int.MaxValue;

            // a message inflight could be re-enqueued but we have to
            // analyze it only just one time for cycle
            var count = clientConnection.InflightQueue.Count;
            while (count > 0 && clientConnection.IsRunning)
            {
                count--;
                msgReceived = null;

                // dequeue message context from queue
                if (!clientConnection.InflightQueue.TryDequeue(out MqttMsgContext msgContext))
                {
                    break;
                }

                // get inflight message
                var msgInflight = msgContext.Message;

                switch (msgContext.State)
                {
                    case MqttMsgState.QueuedQos0:
                        HandleQueuedQos0(clientConnection, msgContext, msgInflight);
                        break;

                    // [v3.1.1] SUBSCRIBE and UNSIBSCRIBE aren't "officially" QOS = 1
                    case MqttMsgState.QueuedQos1:
                    case MqttMsgState.SendSubscribe:
                    case MqttMsgState.SendUnsubscribe:
                        timeout = HandleQueuedQos1SendSubscribeAndSendUnsubscribe(
                            clientConnection,
                            msgContext,
                            msgInflight,
                            timeout);
                        break;

                    case MqttMsgState.QueuedQos2:
                        timeout = HandleQueuedQos2(clientConnection, msgContext, msgInflight, timeout);
                        break;

                    case MqttMsgState.WaitForPuback:
                    case MqttMsgState.WaitForSuback:
                    case MqttMsgState.WaitForUnsuback:
                        msgReceived = HandleWaitForPubackSubackUbsuback(
                            clientConnection,
                            msgContext,
                            msgInflight,
                            ref msgReceivedProcessed,
                            ref timeout);
                        break;

                    case MqttMsgState.WaitForPubrec:
                        msgReceived = HandleWaitForPubrec(
                            clientConnection,
                            msgContext,
                            msgInflight,
                            ref msgReceivedProcessed,
                            ref timeout);
                        break;

                    case MqttMsgState.WaitForPubrel:
                        msgReceived = WaitForPubrel(clientConnection, msgContext, msgInflight, ref msgReceivedProcessed);
                        break;

                    case MqttMsgState.WaitForPubcomp:
                        msgReceived = WaitForPubcomp(
                            clientConnection,
                            msgContext,
                            msgInflight,
                            ref msgReceivedProcessed,
                            ref timeout);
                        break;

                    case MqttMsgState.SendPubrec:
                        // TODO : impossible ? --> QueuedQos2 ToAcknowledge
                        break;

                    case MqttMsgState.SendPubrel:
                        timeout = SendPubrel(clientConnection, msgContext, msgInflight, timeout);
                        break;

                    case MqttMsgState.SendPubcomp:
                        // TODO : impossible ?
                        break;
                    case MqttMsgState.SendPuback:
                        // TODO : impossible ? --> QueuedQos1 ToAcknowledge
                        break;
                    default:
                        break;
                }
            }

            // if message received is orphan, no corresponding message in inflight queue
            // based on messageId, we need to remove from the queue
            if (msgReceived != null && !msgReceivedProcessed)
            {
                clientConnection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
            }

            clientConnection.InflightQueueLastProcessedTime = Environment.TickCount;
            if (!clientConnection.InflightQueue.IsEmpty)
            {
                var inflightQueueProcessEvent = new InflightQueueProcessEvent
                                                    {
                                                        Connection = clientConnection,
                                                        IsCallback = true,
                                                        CallbackCreationTime = timeout
                                                    };
                Task.Factory.StartNew(() => SetTimer(inflightQueueProcessEvent, timeout));
            }
        }

        #endregion

        #region Methods

        private void HandleQueuedQos0(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight)
        {
            // QoS 0, PUBLISH message to send to broker, no state change, no acknowledge
            if (msgContext.Flow == MqttMsgFlow.ToPublish)
            {
                outgoingMessageHandler.Send(connection, msgInflight);
            }

            // QoS 0, no need acknowledge
            else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
            {
                var internalEvent = new InternalEvent(msgInflight);
                connection.EnqueueInternalEvent(internalEvent);
            }
        }

        private int HandleQueuedQos1SendSubscribeAndSendUnsubscribe(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            int timeout)
        {
            InternalEvent internalEvent;

            // QoS 1, PUBLISH or SUBSCRIBE/UNSUBSCRIBE message to send to broker, state change to wait PUBACK or SUBACK/UNSUBACK
            if (msgContext.Flow == MqttMsgFlow.ToPublish)
            {
                msgContext.Timestamp = Environment.TickCount;
                msgContext.Attempt++;

                if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE)
                {
                    // PUBLISH message to send, wait for PUBACK
                    msgContext.State = MqttMsgState.WaitForPuback;

                    // retry ? set dup flag [v3.1.1] only for PUBLISH message
                    if (msgContext.Attempt > 1)
                    {
                        msgInflight.DupFlag = true;
                    }
                }
                else if (msgInflight.Type == MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE)
                {
                    // SUBSCRIBE message to send, wait for SUBACK
                    msgContext.State = MqttMsgState.WaitForSuback;
                }
                else if (msgInflight.Type == MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE)
                {
                    // UNSUBSCRIBE message to send, wait for UNSUBACK
                    msgContext.State = MqttMsgState.WaitForUnsuback;
                }

                outgoingMessageHandler.Send(connection, msgInflight);

                // update timeout : minimum between delay (based on current message sent) or current timeout
                timeout = connection.Settings.DelayOnRetry < timeout ? connection.Settings.DelayOnRetry : timeout;

                // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                connection.EnqueueInflight(msgContext);
            }

            // QoS 1, PUBLISH message received from broker to acknowledge, send PUBACK
            else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
            {
                outgoingMessageHandler.Puback(connection, msgInflight.MessageId);

                internalEvent = new InternalEvent(msgInflight);

                // notify published message from broker and acknowledged
                connection.EnqueueInternalEvent(internalEvent);
            }

            return timeout;
        }

        private int HandleQueuedQos2(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            int timeout)
        {
            // QoS 2, PUBLISH message to send to broker, state change to wait PUBREC
            if (msgContext.Flow == MqttMsgFlow.ToPublish)
            {
                msgContext.Timestamp = Environment.TickCount;
                msgContext.Attempt++;
                msgContext.State = MqttMsgState.WaitForPubrec;

                // retry ? set dup flag
                if (msgContext.Attempt > 1)
                {
                    msgInflight.DupFlag = true;
                }

                outgoingMessageHandler.Send(connection, msgInflight);

                // update timeout : minimum between delay (based on current message sent) or current timeout
                timeout = connection.Settings.DelayOnRetry < timeout ? connection.Settings.DelayOnRetry : timeout;

                // re-enqueue message (I have to re-analyze for receiving PUBREC)
                connection.EnqueueInflight(msgContext);
            }

            // QoS 2, PUBLISH message received from broker to acknowledge, send PUBREC, state change to wait PUBREL
            else if (msgContext.Flow == MqttMsgFlow.ToAcknowledge)
            {
                msgContext.State = MqttMsgState.WaitForPubrel;

                outgoingMessageHandler.Pubrec(connection, msgInflight.MessageId);

                // re-enqueue message (I have to re-analyze for receiving PUBREL)
                connection.EnqueueInflight(msgContext);
            }

            return timeout;
        }

        private MqttMsgBase HandleWaitForPubackSubackUbsuback(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            ref bool msgReceivedProcessed,
            ref int timeout)
        {
            // QoS 1, waiting for PUBACK of a PUBLISH message sent or
            // waiting for SUBACK of a SUBSCRIBE message sent or
            // waiting for UNSUBACK of a UNSUBSCRIBE message sent or
            if (msgContext.Flow != MqttMsgFlow.ToPublish)
            {
                return null;
            }

            if (!connection.InternalQueue.TryPeek(out MqttMsgBase msgReceived))
            {
                return null;
            }

            var acknowledge = false;
            InternalEvent internalEvent;

            // PUBACK message or SUBACK/UNSUBACK message for the current message
            if (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBACK_TYPE
                && msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE
                && msgReceived.MessageId == msgInflight.MessageId
                || msgReceived.Type == MqttMsgBase.MQTT_MSG_SUBACK_TYPE
                && msgInflight.Type == MqttMsgBase.MQTT_MSG_SUBSCRIBE_TYPE
                && msgReceived.MessageId == msgInflight.MessageId
                || msgReceived.Type == MqttMsgBase.MQTT_MSG_UNSUBACK_TYPE
                && msgInflight.Type == MqttMsgBase.MQTT_MSG_UNSUBSCRIBE_TYPE
                && msgReceived.MessageId == msgInflight.MessageId)
            {
                // received message processed
                connection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
                acknowledge = true;
                msgReceivedProcessed = true;

                // if PUBACK received, confirm published with flag
                if (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBACK_TYPE)
                {
                    internalEvent = new PublishedInternalEvent(msgReceived, true);
                }
                else
                {
                    internalEvent = new InternalEvent(msgReceived);
                }

                // notify received acknowledge from broker of a published message or subscribe/unsubscribe message
                connection.EnqueueInternalEvent(internalEvent);

                // PUBACK received for PUBLISH message with QoS Level 1, remove from session state
                if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE && connection.Session != null
                    && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                {
                    connection.Session.InflightMessages.TryRemove(msgContext.Key, out MqttMsgContext contextToBeRemoved);
                }
            }

            // current message not acknowledged, no PUBACK or SUBACK/UNSUBACK or not equal messageid 
            if (!acknowledge)
            {
                var delta = Environment.TickCount - msgContext.Timestamp;

                // check timeout for receiving PUBACK since PUBLISH was sent or
                // for receiving SUBACK since SUBSCRIBE was sent or
                // for receiving UNSUBACK since UNSUBSCRIBE was sent
                if (delta >= connection.Settings.DelayOnRetry)
                {
                    // max retry not reached, resend
                    if (msgContext.Attempt < connection.Settings.AttemptsOnRetry)
                    {
                        msgContext.State = MqttMsgState.QueuedQos1;

                        // re-enqueue message
                        connection.EnqueueInflight(msgContext);

                        // update timeout (0 -> reanalyze queue immediately)
                        timeout = 0;
                    }
                    else
                    {
                        // if PUBACK for a PUBLISH message not received after retries, raise event for not published
                        if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE)
                        {
                            // PUBACK not received in time, PUBLISH retries failed, need to remove from session inflight messages too
                            if (connection.Session != null
                                && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                            {
                                connection.Session.InflightMessages.TryRemove(
                                    msgContext.Key,
                                    out MqttMsgContext contextToBeRemoved);
                            }

                            internalEvent = new PublishedInternalEvent(msgInflight, false);

                            // notify not received acknowledge from broker and message not published
                            connection.EnqueueInternalEvent(internalEvent);
                        }

                        // NOTE : not raise events for SUBACK or UNSUBACK not received
                        // for the user no event raised means subscribe/unsubscribe failed
                    }
                }
                else
                {
                    // re-enqueue message (I have to re-analyze for receiving PUBACK, SUBACK or UNSUBACK)
                    connection.EnqueueInflight(msgContext);

                    // update timeout
                    var msgTimeout = connection.Settings.DelayOnRetry - delta;
                    timeout = msgTimeout < timeout ? msgTimeout : timeout;
                }
            }

            return msgReceived;
        }

        private MqttMsgBase HandleWaitForPubrec(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            ref bool msgReceivedProcessed,
            ref int timeout)
        {
            // QoS 2, waiting for PUBREC of a PUBLISH message sent
            if (msgContext.Flow != MqttMsgFlow.ToPublish)
            {
                return null;
            }

            if (!connection.InternalQueue.TryPeek(out MqttMsgBase msgReceived))
            {
                return null;
            }

            var acknowledge = false;
            InternalEvent internalEvent;

            // it is a PUBREC message
            if (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBREC_TYPE)
            {
                if (msgReceived.MessageId == msgInflight.MessageId)
                {
                    // received message processed
                    connection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
                    acknowledge = true;
                    msgReceivedProcessed = true;

                    outgoingMessageHandler.Pubrel(connection, msgInflight.MessageId, false);

                    msgContext.State = MqttMsgState.WaitForPubcomp;
                    msgContext.Timestamp = Environment.TickCount;
                    msgContext.Attempt = 1;

                    // update timeout : minimum between delay (based on current message sent) or current timeout
                    timeout = connection.Settings.DelayOnRetry < timeout ? connection.Settings.DelayOnRetry : timeout;

                    // re-enqueue message
                    connection.EnqueueInflight(msgContext);
                }
            }

            // current message not acknowledged
            if (!acknowledge)
            {
                var delta = Environment.TickCount - msgContext.Timestamp;

                // check timeout for receiving PUBREC since PUBLISH was sent
                if (delta >= connection.Settings.DelayOnRetry)
                {
                    // max retry not reached, resend
                    if (msgContext.Attempt < connection.Settings.AttemptsOnRetry)
                    {
                        msgContext.State = MqttMsgState.QueuedQos2;

                        // re-enqueue message
                        connection.EnqueueInflight(msgContext);

                        // update timeout (0 -> reanalyze queue immediately)
                        timeout = 0;
                    }
                    else
                    {
                        // PUBREC not received in time, PUBLISH retries failed, need to remove from session inflight messages too
                        if (connection.Session != null
                            && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                        {
                            connection.Session.InflightMessages.TryRemove(
                                msgContext.Key,
                                out MqttMsgContext contextToBeRemoved);
                        }

                        // if PUBREC for a PUBLISH message not received after retries, raise event for not published
                        internalEvent = new PublishedInternalEvent(msgInflight, false);

                        // notify not received acknowledge from broker and message not published
                        connection.EnqueueInternalEvent(internalEvent);
                    }
                }
                else
                {
                    // re-enqueue message
                    connection.EnqueueInflight(msgContext);

                    // update timeout
                    var msgTimeout = connection.Settings.DelayOnRetry - delta;
                    timeout = msgTimeout < timeout ? msgTimeout : timeout;
                }
            }

            return msgReceived;
        }

        private int SendPubrel(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            int timeout)
        {
            // QoS 2, PUBREL message to send to broker, state change to wait PUBCOMP
            if (msgContext.Flow == MqttMsgFlow.ToPublish)
            {
                msgContext.State = MqttMsgState.WaitForPubcomp;
                msgContext.Timestamp = Environment.TickCount;
                msgContext.Attempt++;

                // retry ? set dup flag [v3.1.1] no needed
                if (connection.ProtocolVersion == MqttProtocolVersion.Version_3_1 && msgContext.Attempt > 1)
                {
                    outgoingMessageHandler.Pubrel(connection, msgInflight.MessageId, true);
                }
                else
                {
                    outgoingMessageHandler.Pubrel(connection, msgInflight.MessageId, false);
                }

                // update timeout : minimum between delay (based on current message sent) or current timeout
                timeout = connection.Settings.DelayOnRetry < timeout ? connection.Settings.DelayOnRetry : timeout;

                // re-enqueue message
                connection.EnqueueInflight(msgContext);
            }

            return timeout;
        }

        private async void SetTimer(InflightQueueProcessEvent processEvent, int timeout)
        {
            await Task.Delay(timeout);
            processEvent.Connection.EnqueueInflightCallback(processEvent);
        }

        private MqttMsgBase WaitForPubcomp(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            ref bool msgReceivedProcessed,
            ref int timeout)
        {
            // QoS 2, waiting for PUBCOMP of a PUBREL message sent
            if (msgContext.Flow != MqttMsgFlow.ToPublish)
            {
                return null;
            }

            if (!connection.InternalQueue.TryPeek(out MqttMsgBase msgReceived))
            {
                return null;
            }

            var acknowledge = false;
            InternalEvent internalEvent;

            // it is a PUBCOMP message
            if (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBCOMP_TYPE)
            {
                // PUBCOMP message for the current message
                if (msgReceived.MessageId == msgInflight.MessageId)
                {
                    connection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
                    acknowledge = true;
                    msgReceivedProcessed = true;

                    internalEvent = new PublishedInternalEvent(msgReceived, true);

                    // notify received acknowledge from broker of a published message
                    connection.EnqueueInternalEvent(internalEvent);

                    // PUBCOMP received for PUBLISH message with QoS Level 2, remove from session state
                    if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE && connection.Session != null
                        && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                    {
                        connection.Session.InflightMessages.TryRemove(
                            msgContext.Key,
                            out MqttMsgContext contextToBeRemoved);
                    }
                }
            }

            // it is a PUBREC message
            else if (msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBREC_TYPE)
            {
                // another PUBREC message for the current message due to a retransmitted PUBLISH
                // I'm in waiting for PUBCOMP, so I can discard connection PUBREC
                if (msgReceived.MessageId == msgInflight.MessageId)
                {
                    connection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
                    acknowledge = true;
                    msgReceivedProcessed = true;

                    // re-enqueue message
                    connection.EnqueueInflight(msgContext);
                }
            }

            // current message not acknowledged
            if (!acknowledge)
            {
                var delta = Environment.TickCount - msgContext.Timestamp;

                // check timeout for receiving PUBCOMP since PUBREL was sent
                if (delta >= connection.Settings.DelayOnRetry)
                {
                    // max retry not reached, resend
                    if (msgContext.Attempt < connection.Settings.AttemptsOnRetry)
                    {
                        msgContext.State = MqttMsgState.SendPubrel;

                        // re-enqueue message
                        connection.EnqueueInflight(msgContext);

                        // update timeout (0 -> reanalyze queue immediately)
                        timeout = 0;
                    }
                    else
                    {
                        // PUBCOMP not received, PUBREL retries failed, need to remove from session inflight messages too
                        if (connection.Session != null
                            && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                        {
                            connection.Session.InflightMessages.TryRemove(
                                msgContext.Key,
                                out MqttMsgContext contextToBeRemoved);
                        }

                        // if PUBCOMP for a PUBLISH message not received after retries, raise event for not published
                        internalEvent = new PublishedInternalEvent(msgInflight, false);

                        // notify not received acknowledge from broker and message not published
                        connection.EnqueueInternalEvent(internalEvent);
                    }
                }
                else
                {
                    // re-enqueue message
                    connection.EnqueueInflight(msgContext);

                    // update timeout
                    var msgTimeout = connection.Settings.DelayOnRetry - delta;
                    timeout = msgTimeout < timeout ? msgTimeout : timeout;
                }
            }

            return msgReceived;
        }

        private MqttMsgBase WaitForPubrel(
            MqttConnection connection,
            MqttMsgContext msgContext,
            MqttMsgBase msgInflight,
            ref bool msgReceivedProcessed)
        {
            // QoS 2, waiting for PUBREL of a PUBREC message sent
            if (msgContext.Flow != MqttMsgFlow.ToAcknowledge)
            {
                return null;
            }

            if (!connection.InternalQueue.TryPeek(out MqttMsgBase msgReceived))
            {
                return null;
            }

            InternalEvent internalEvent;

            // it is a PUBREL message
            if (msgReceived != null && msgReceived.Type == MqttMsgBase.MQTT_MSG_PUBREL_TYPE)
            {
                if (msgReceived.MessageId == msgInflight.MessageId)
                {
                    // received message processed
                    connection.InternalQueue.TryDequeue(out MqttMsgBase dequeuedMsg);
                    msgReceivedProcessed = true;

                    outgoingMessageHandler.Pubcomp(connection, msgInflight.MessageId);

                    internalEvent = new InternalEvent(msgInflight);

                    // notify published message from broker and acknowledged
                    connection.EnqueueInternalEvent(internalEvent);

                    // PUBREL received (and PUBCOMP sent) for PUBLISH message with QoS Level 2, remove from session state
                    if (msgInflight.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE && connection.Session != null
                        && connection.Session.InflightMessages.ContainsKey(msgContext.Key))
                    {
                        connection.Session.InflightMessages.TryRemove(
                            msgContext.Key,
                            out MqttMsgContext contextToBeRemoved);
                    }
                }
                else
                {
                    // re-enqueue message
                    connection.EnqueueInflight(msgContext);
                }
            }
            else
            {
                connection.EnqueueInflight(msgContext);
            }

            return msgReceived;
        }

        #endregion
    }
}