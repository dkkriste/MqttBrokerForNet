namespace MqttBrokerForNet.Business.Managers
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Entities.Enums;
    using MqttBrokerForNet.Domain.Events;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttConnectionManager : IMqttConnectionManager
    {
        #region Fields

        private readonly IMqttConnectionInflightHandler connectionInflightHandler;

        private readonly IMqttConnectionInternalEventHandler connectionInternalEventHandler;

        private readonly IMqttIncommingMessageHandler incommingMessageHandler;

        private readonly IMqttSubscriptionManager subscriptionManager;

        private readonly IMqttSessionManager mqttSessionManager;

        private readonly IMqttOutgoingMessageHandler outgoingMessageHandler;

        private readonly IMqttPublishingManager publishingManager;

        private readonly IMqttSecurityManager securityManager;

        private readonly IMqttConnectionPoolManager connectionPoolManager;

        private readonly IMqttRetainedMessageManager retainedMessageManager;

        private readonly BlockingCollection<InflightQueueProcessEvent> connectionsWithInflightQueuesToProcess;

        private readonly BlockingCollection<MqttConnection> connectionsWithInternalEventQueuesToProcess;

        private readonly BlockingCollection<MqttRawMessage> rawMessageQueue;

        #endregion

        public MqttConnectionManager(
            IMqttConnectionInflightHandler connectionInflightHandler,
            IMqttConnectionInternalEventHandler connectionInternalEventHandler,
            IMqttIncommingMessageHandler incommingMessageHandler,
            IMqttSubscriptionManager subscriptionManager,
            IMqttSessionManager mqttSessionManager,
            IMqttOutgoingMessageHandler outgoingMessageHandler,
            IMqttPublishingManager publishingManager,
            IMqttSecurityManager securityManager,
            IMqttConnectionPoolManager connectionPoolManager,
            IMqttRetainedMessageManager retainedMessageManager)
        {
            this.connectionInflightHandler = connectionInflightHandler;
            this.connectionInternalEventHandler = connectionInternalEventHandler;
            this.incommingMessageHandler = incommingMessageHandler;
            this.subscriptionManager = subscriptionManager;
            this.mqttSessionManager = mqttSessionManager;
            this.outgoingMessageHandler = outgoingMessageHandler;
            this.publishingManager = publishingManager;
            this.securityManager = securityManager;
            this.connectionPoolManager = connectionPoolManager;
            this.retainedMessageManager = retainedMessageManager;
            rawMessageQueue = new BlockingCollection<MqttRawMessage>();
            connectionsWithInflightQueuesToProcess = new BlockingCollection<InflightQueueProcessEvent>();
            connectionsWithInternalEventQueuesToProcess = new BlockingCollection<MqttConnection>();
        }

        #region Public Methods and Operators

        public void EnqueueRawMessageForProcessing(MqttRawMessage rawMessage)
        {
            rawMessageQueue.Add(rawMessage);
        }

        public void EnqueueClientConnectionWithInternalEventQueueToProcess(MqttConnection connection)
        {
            connectionsWithInternalEventQueuesToProcess.Add(connection);
        }

        public void EnqueueClientConnectionWithInflightQueueToProcess(InflightQueueProcessEvent processEvent)
        {
            connectionsWithInflightQueuesToProcess.Add(processEvent);
        }

        public void ProcessRawMessageQueue()
        {
            var rawMessage = rawMessageQueue.Take();
            incommingMessageHandler.ProcessReceivedMessage(rawMessage);
        }

        public void ProcessInflightQueue()
        {
            var processEvent = connectionsWithInflightQueuesToProcess.Take();
            connectionInflightHandler.ProcessInflightQueue(processEvent);
        }

        public void ProcessInternalEventQueue()
        {
            var clientConnection = connectionsWithInternalEventQueuesToProcess.Take();
            connectionInternalEventHandler.ProcessInternalEventQueue(clientConnection);
        }


        public void OnConnectionClosed(MqttConnection connection)
        {
            // if client is connected
            MqttConnection connectedClient;
            if (connection.IsConnected && MqttBroker.TryRemoveClientConnection(connection.ClientId, out connectedClient))
            {
                // client has a will message
                if (connection.WillFlag)
                {
                    // create the will PUBLISH message
                    var publish = new MqttMsgPublish(
                        connection.WillTopic,
                        Encoding.UTF8.GetBytes(connection.WillMessage),
                        false,
                        connection.WillQosLevel,
                        false);

                    // publish message through publisher manager
                    publishingManager.Publish(publish);
                }

                // if not clean session
                if (!connection.CleanSession)
                {
                    var subscriptions = connection.Subscriptions.Values.ToList();

                    if (subscriptions.Count > 0)
                    {
                        mqttSessionManager.SaveSession(connection.ClientId, connection.Session, subscriptions);
                    }
                }

                foreach (var topic in connection.Subscriptions.Keys)
                {
                    subscriptionManager.Unsubscribe(topic, connection);
                }

                // close the client
                CloseClientConnection(connection);
                connectionPoolManager.ReturnConnection(connection);
                //Interlocked.Decrement(ref numberOfConnectedClients);
            }
        }

        public void OnMqttMsgConnected(MqttConnection connection, MqttMsgConnect message)
        {
            connection.ProtocolVersion = (MqttProtocolVersion)message.ProtocolVersion;

            // verify message to determine CONNACK message return code to the client
            var returnCode = MqttConnectVerify(message);

            // [v3.1.1] if client id is zero length, the broker assigns a unique identifier to it
            var clientId = message.ClientId.Length != 0 ? message.ClientId : Guid.NewGuid().ToString();

            // connection "could" be accepted
            if (returnCode == MqttMsgConnack.CONN_ACCEPTED)
            {
                // check if there is a client already connected with same client Id
                var connectionConnected = MqttBroker.GetClientConnection(clientId);

                // force connection close to the existing client (MQTT protocol)
                if (connectionConnected != null)
                {
                    OnConnectionClosed(connectionConnected);
                }

                // add client to the collection
                MqttBroker.TryAddClientConnection(clientId, connection);
                //Interlocked.Increment(ref numberOfConnectedClients);
            }

            // connection accepted, load (if exists) client session
            if (returnCode == MqttMsgConnack.CONN_ACCEPTED)
            {
                if (!message.CleanSession)
                {
                    // create session for the client
                    var clientSession = new MqttClientSession(clientId);

                    // get session for the connected client
                    var session = mqttSessionManager.GetSession(clientId);

                    // [v3.1.1] session present flag
                    var sessionPresent = false;

                    // set inflight queue into the client session
                    if (session != null)
                    {
                        clientSession.InflightMessages = session.InflightMessages;

                        // [v3.1.1] session present flag
                        if (connection.ProtocolVersion == MqttProtocolVersion.Version_3_1_1)
                        {
                            sessionPresent = true;
                        }
                    }

                    // send CONNACK message to the client
                    outgoingMessageHandler.Connack(connection, message, returnCode, clientId, sessionPresent);

                    // load/inject session to the client
                    connection.LoadSession(clientSession);

                    if (session != null)
                    {
                        // set reference to connected client into the session
                        session.Connection = connection;

                        // there are saved subscriptions
                        if (session.Subscriptions != null)
                        {
                            foreach (var subscription in session.Subscriptions)
                            {
                                subscriptionManager.Subscribe(subscription.Topic, subscription.QosLevel, connection);

                                // publish retained message on the current subscription
                                retainedMessageManager.PublishRetaind(subscription.Topic, connection);
                            }
                        }

                        // there are saved outgoing messages
                        if (session.OutgoingMessages.Count > 0)
                        {
                            publishingManager.PublishSession(session.ClientId);
                        }
                    }
                }

                // requested clean session
                else
                {
                    // send CONNACK message to the client
                    outgoingMessageHandler.Connack(connection, message, returnCode, clientId, false);

                    mqttSessionManager.ClearSession(clientId);
                }
            }
            else
            {
                outgoingMessageHandler.Connack(connection, message, returnCode, clientId, false);
            }
        }

        public void OnMqttMsgPublishReceived(MqttConnection connection, MqttMsgPublish msg)
        {
            if (securityManager.AuthorizePublish(connection, msg.Topic))
            {
                // create PUBLISH message to publish
                // [v3.1.1] DUP flag from an incoming PUBLISH message is not propagated to subscribers
                // It should be set in the outgoing PUBLISH message based on transmission for each subscriber
                var publish = new MqttMsgPublish(msg.Topic, msg.Message, false, msg.QosLevel, msg.Retain);

                // publish message through publisher manager
                publishingManager.Publish(publish);
            }
        }

        public void OnMqttMsgSubscribeReceived(
            MqttConnection connection,
            ushort messageId,
            string[] topics,
            byte[] qosLevels)
        {
            var wasSubscribed = false;
            for (var i = 0; i < topics.Length; i++)
            {
                if (securityManager.AuthorizeSubscriber(connection, topics[i]))
                {
                    // TODO : business logic to grant QoS levels based on some conditions ?
                    // now the broker granted the QoS levels requested by client

                    // subscribe client for each topic and QoS level requested
                    subscriptionManager.Subscribe(topics[i], qosLevels[i], connection);
                    wasSubscribed = true;
                }
            }

            if (wasSubscribed)
            {
                // send SUBACK message to the client
                outgoingMessageHandler.Suback(connection, messageId, qosLevels);

                foreach (var topic in topics)
                {
                    retainedMessageManager.PublishRetaind(topic, connection);
                }
            }
        }

        public void OpenClientConnection(MqttConnection connection)
        {
            connection.IsRunning = true;

            // connection.ConnectionManager = this;
            //tcpReceiver.StartReceive(connection.ReceiveSocketAsyncEventArgs);
            Task.Factory.StartNew(() => CheckForClientTimeout(connection));
        }


        //public void PeriodicLogging()
        //{
        //    var numberOfConnectionsGottenCopy = numberOfConnectionsGotten;
        //    var numberOfConnectionsReturnedCopy = numberOfConnectionsReturned;

        //    logginHandler.LogMetric(this, LoggerConstants.NumberOfClientConnectionsGotten, numberOfConnectionsGottenCopy);
        //    logginHandler.LogMetric(
        //        this,
        //        LoggerConstants.NumberOfClientConnectionsReturned,
        //        numberOfConnectionsReturnedCopy);
        //    logginHandler.LogMetric(
        //        this,
        //        LoggerConstants.NumberOfClientConnectionsAvailable,
        //        unconnectedClientPool.Count);

        //    Interlocked.Add(ref numberOfConnectionsGotten, -numberOfConnectionsGottenCopy);
        //    Interlocked.Add(ref numberOfConnectionsReturned, -numberOfConnectionsReturnedCopy);
        //}


        #endregion

        #region Methods

        private async void CheckForClientTimeout(MqttConnection connection)
        {
            await Task.Delay(connection.Settings.TimeoutOnConnection);

            // broker need to receive the first message (CONNECT)
            // within a reasonable amount of time after TCP/IP connection
            // wait on receiving message from client with a connection timeout
            if (connection.IsRunning && !connection.IsConnected && connection.EventQueue.IsEmpty)
            {
                connection.OnConnectionClosed();
            }
        }

        private void CloseClientConnection(MqttConnection connection)
        {
            // stop receiving thread
            connection.IsRunning = false;

            connection.IsConnected = false;
        }

        /// <summary>
        /// Check CONNECT message to accept or not the connection request 
        /// </summary>
        /// <param name="connect">CONNECT message received from client</param>
        /// <returns>Return code for CONNACK message</returns>
        private byte MqttConnectVerify(MqttMsgConnect connect)
        {
            // unacceptable protocol version
            if (connect.ProtocolVersion != MqttMsgConnect.PROTOCOL_VERSION_V3_1
                && connect.ProtocolVersion != MqttMsgConnect.PROTOCOL_VERSION_V3_1_1)
            {
                return MqttMsgConnack.CONN_REFUSED_PROT_VERS;
            }

            // client id length exceeded (only for old MQTT 3.1)
            if (connect.ProtocolVersion == MqttMsgConnect.PROTOCOL_VERSION_V3_1
                && connect.ClientId.Length > MqttMsgConnect.CLIENT_ID_MAX_LENGTH)
            {
                return MqttMsgConnack.CONN_REFUSED_IDENT_REJECTED;
            }

            // [v.3.1.1] client id zero length is allowed but clean session must be true
            if (connect.ClientId.Length == 0 && !connect.CleanSession)
            {
                return MqttMsgConnack.CONN_REFUSED_IDENT_REJECTED;
            }

            // check user authentication
            if (!securityManager.AuthenticateUser(connect.Username, connect.Password))
            {
                return MqttMsgConnack.CONN_REFUSED_USERNAME_PASSWORD;
            }

            // server unavailable and not authorized ?
            else
            {
                // TODO : other checks on CONNECT message
            }

            return MqttMsgConnack.CONN_ACCEPTED;
        }

        #endregion

    }
}