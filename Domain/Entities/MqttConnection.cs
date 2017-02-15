/*
Copyright (c) 2013, 2014 Paolo Patierno

All rights reserved. This program and the accompanying materials
are made available under the terms of the Eclipse Public License v1.0
and Eclipse Distribution License v1.0 which accompany this distribution. 

The Eclipse Public License is available at 
   http://www.eclipse.org/legal/epl-v10.html
and the Eclipse Distribution License is available at 
   http://www.eclipse.org/org/documents/edl-v10.php.

Contributors:
   Paolo Patierno - initial API and implementation and/or initial documentation
   David Kristensen - optimalization for the azure platform
*/

namespace MqttBrokerForNet.Domain.Entities
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;
    using System.Threading;

    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities.Enums;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Messages;

    /// <summary>
    /// MQTT Connection
    /// </summary>
    public class MqttConnection
    {
        #region Fields

        // inflight messages queue
        public readonly ConcurrentQueue<MqttMsgContext> InflightQueue;

        // internal queue for received messages about inflight messages
        public readonly ConcurrentQueue<MqttMsgBase> InternalQueue;

        // internal queue for dispatching events
        public readonly ConcurrentQueue<InternalEvent> EventQueue;

        public readonly ConcurrentDictionary<string, MqttSubscription> Subscriptions;

        public readonly SocketAsyncEventArgs ReceiveSocketAsyncEventArgs;

        public readonly int ReceiveSocketOffset;

        public readonly int ReceiveSocketBufferSize;

        public int PreviouslyReceivedBytes;

        // current message identifier generated
        private ushort messageIdCounter = 0;

        #endregion

        #region Constructors

        public MqttConnection(SocketAsyncEventArgs receiveSocketAsyncEventArgs)
        {
            this.ReceiveSocketAsyncEventArgs = receiveSocketAsyncEventArgs;
            this.ReceiveSocketOffset = receiveSocketAsyncEventArgs.Offset;
            this.ReceiveSocketBufferSize = receiveSocketAsyncEventArgs.Count;
            this.ReceiveSocketAsyncEventArgs.UserToken = this;
     
            // set default MQTT protocol version (default is 3.1.1)
            this.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;

            // reference to MQTT settings
            this.Settings = MqttSettings.Instance;

            this.KeepAliveEvent = new AutoResetEvent(false);

            // queue for handling inflight messages (publishing and acknowledge)
            this.InflightQueue = new ConcurrentQueue<MqttMsgContext>();

            // queue for received message
            this.EventQueue = new ConcurrentQueue<InternalEvent>();
            this.InternalQueue = new ConcurrentQueue<MqttMsgBase>();

            this.Subscriptions = new ConcurrentDictionary<string, MqttSubscription>();

            // client not connected yet (CONNACK not send from client), some default values
            this.IsConnected = false;
            this.ClientId = null;
            this.CleanSession = true;

            // session
            this.Session = null;
        }

        #endregion
    
        #region Properties

        // running status of threads
        public bool IsRunning { get; set; }

        // keep alive period (in ms)
        public int KeepAlivePeriod { get; set; }

        // events for signaling on keep alive thread
        public AutoResetEvent KeepAliveEvent { get; set; }

        public AutoResetEvent KeepAliveEventEnd { get; set; }

        // last communication time in ticks
        public int LastCommunicationTime { get; set; }

        // connection is closing due to peer
        public bool IsConnectionClosing { get; set; }

        public IMqttConnectionManager ConnectionManager { get; set; }

        /// <summary>
        /// Connection status between client and broker
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Client identifier
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Clean session flag
        /// </summary>
        public bool CleanSession { get; set; }

        /// <summary>
        /// Will flag
        /// </summary>
        public bool WillFlag { get; set; }

        /// <summary>
        /// Will QOS level
        /// </summary>
        public byte WillQosLevel { get; set; }

        /// <summary>
        /// Will topic
        /// </summary>
        public string WillTopic { get; set; }

        /// <summary>
        /// Will message
        /// </summary>
        public string WillMessage { get; set; }

        /// <summary>
        /// MQTT protocol version
        /// </summary>
        public MqttProtocolVersion ProtocolVersion { get; set; }

        /// <summary>
        /// MQTT Client Session
        /// </summary>
        public MqttClientSession Session { get; set; }

        /// <summary>
        /// MQTT client settings
        /// </summary>
        public MqttSettings Settings { get; private set; }

        public int InflightQueueLastProcessedTime { get; set; }

        #endregion

        public void ResetSocket()
        {
            this.ReceiveSocketAsyncEventArgs.AcceptSocket = null;
            this.ReceiveSocketAsyncEventArgs.SetBuffer(this.ReceiveSocketOffset, this.ReceiveSocketBufferSize);
        }

        public void Reset()
        {
            this.ConnectionManager = null;

            this.LastCommunicationTime = 0;
            this.KeepAlivePeriod = 0;
            this.messageIdCounter = 0;

            this.InflightQueueLastProcessedTime = 0;

            this.ClientId = string.Empty;
            this.IsRunning = false;
            this.IsConnected = false;
            this.IsConnectionClosing = false;
            this.CleanSession = true;
            this.WillFlag = false;
            this.WillQosLevel = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE;
            this.WillTopic = string.Empty;
            this.WillMessage = string.Empty;
            this.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;
            this.Session = null;

            while (this.InflightQueue.TryDequeue(out MqttMsgContext msgContext))
            {
            }

            while (this.InternalQueue.TryDequeue(out MqttMsgBase message))
            {
            }

            while (this.EventQueue.TryDequeue(out InternalEvent internalEvent))
            {
            }

            if (!this.InflightQueue.IsEmpty || !this.InternalQueue.IsEmpty || !this.EventQueue.IsEmpty)
            {
                throw new Exception("Failed to empty queues");
            }
        }

        public void EnqueueInternalEvent(InternalEvent internalEvent)
        {
            if (this.ConnectionManager != null)
            {
                this.EventQueue.Enqueue(internalEvent);
                this.ConnectionManager.EnqueueClientConnectionWithInternalEventQueueToProcess(this);
            }
        }

        public void EnqueueInflight(MqttMsgContext inflightMessageContext)
        {
            if (this.ConnectionManager != null)
            {
                this.InflightQueue.Enqueue(inflightMessageContext);
                this.ConnectionManager.EnqueueClientConnectionWithInflightQueueToProcess(new InflightQueueProcessEvent { Connection = this, IsCallback = false });
            }
        }

        public void EnqueueInflightCallback(InflightQueueProcessEvent processEvent)
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.EnqueueClientConnectionWithInflightQueueToProcess(processEvent);
            }
        }

        public void EnqueueRawMessage(MqttRawMessage rawMessage)
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.EnqueueRawMessageForProcessing(rawMessage);
            }
        }

        public void OnConnectionClosing()
        {
            if (!this.IsConnectionClosing)
            {
                this.IsConnectionClosing = true;
            }
        }

        public void OnMqttMsgConnected(MqttMsgConnect connect)
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.OnMqttMsgConnected(this, connect);
            }
        }

        public void OnConnectionClosed()
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.OnConnectionClosed(this);
            }
        }

        public void OnMqttMsgPublishReceived(MqttMsgPublish msg)
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.OnMqttMsgPublishReceived(this, msg);
            }
        }

        public void OnMqttMsgSubscribeReceived(ushort messageId, string[] topics, byte[] qosLevels)
        {
            if (this.ConnectionManager != null)
            {
                this.ConnectionManager.OnMqttMsgSubscribeReceived(this, messageId, topics, qosLevels);
            }
        }

        /// <summary>
        /// Generate the next message identifier
        /// </summary>
        /// <returns>Message identifier</returns>
        public ushort GetMessageId()
        {
            // if 0 or max UInt16, it becomes 1 (first valid messageId)
            this.messageIdCounter = ((this.messageIdCounter % ushort.MaxValue) != 0) ? (ushort)(this.messageIdCounter + 1) : (ushort)1;
            return this.messageIdCounter;
        }

        /// <summary>
        /// Load a given session
        /// </summary>
        /// <param name="session">MQTT Client session to load</param>
        public void LoadSession(MqttClientSession session)
        {
            // if not clean session
            if (!this.CleanSession)
            {
                // set the session ...
                this.Session = session;

                // ... and restore it
                this.RestoreSession();
            }
        }

        /// <summary>
        /// Restore session
        /// </summary>
        private void RestoreSession()
        {
            // if not clean session
            if (!this.CleanSession)
            {
                // there is a previous session
                if (this.Session != null)
                {
                    foreach (MqttMsgContext msgContext in this.Session.InflightMessages.Values)
                    {
                        this.InflightQueue.Enqueue(msgContext);

                        // if it is a PUBLISH message to publish
                        if ((msgContext.Message.Type == MqttMsgBase.MQTT_MSG_PUBLISH_TYPE)
                            && (msgContext.Flow == MqttMsgFlow.ToPublish))
                        {
                            // it's QoS 1 and we haven't received PUBACK
                            if ((msgContext.Message.QosLevel == MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE)
                                && (msgContext.State == MqttMsgState.WaitForPuback))
                            {
                                // we haven't received PUBACK, we need to resend PUBLISH message
                                msgContext.State = MqttMsgState.QueuedQos1;
                            }
                            // it's QoS 2
                            else if (msgContext.Message.QosLevel == MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE)
                            {
                                // we haven't received PUBREC, we need to resend PUBLISH message
                                if (msgContext.State == MqttMsgState.WaitForPubrec)
                                {
                                    msgContext.State = MqttMsgState.QueuedQos2;
                                }
                                // we haven't received PUBCOMP, we need to resend PUBREL for it
                                else if (msgContext.State == MqttMsgState.WaitForPubcomp)
                                {
                                    msgContext.State = MqttMsgState.SendPubrel;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // create new session
                    this.Session = new MqttClientSession(this.ClientId);
                }
            }
            // clean any previous session
            else
            {
                if (this.Session != null)
                {
                    this.Session.Clear();
                }
            }
        }
    }
}
