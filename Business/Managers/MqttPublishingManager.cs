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

namespace MqttBrokerForNet.Business.Managers
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text.RegularExpressions;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Business.Workers;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttPublishingManager : IMqttPublishingManager
    {
        private readonly IMqttIncommingMessageHandler incommingMessageHandler;

        private readonly IMqttSubscriptionManager subscriptionManager;

        private readonly IMqttSessionManager sessionManager;

        private readonly IMqttRetainedMessageManager retainedMessageManager;

        private readonly BlockingCollection<string> clientsForSession;

        private readonly BlockingCollection<MqttMsgBase> publishQueue;

        private readonly BlockingCollection<MqttMsgBase> sessionsPublishQueue;

        #region Constructors and Destructors

        /// <summary>
        /// Constructor
        /// </summary>
        public MqttPublishingManager(IMqttIncommingMessageHandler incommingMessageHandler, IMqttSubscriptionManager subscriptionManager, IMqttSessionManager sessionManager, IMqttRetainedMessageManager retainedMessageManager)
        {
            this.incommingMessageHandler = incommingMessageHandler;
            this.subscriptionManager = subscriptionManager;
            this.sessionManager = sessionManager;
            this.retainedMessageManager = retainedMessageManager;

            // create empty list for destination client for outgoing session message
            clientsForSession = new BlockingCollection<string>();

            // create publish messages queue
            publishQueue = new BlockingCollection<MqttMsgBase>();

            sessionsPublishQueue = new BlockingCollection<MqttMsgBase>();
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Publish message
        /// </summary>
        /// <param name="publish">Message to publish</param>
        public void Publish(MqttMsgPublish publish)
        {
            retainedMessageManager.CheckForAndSetRetainedMessage(publish);

            // enqueue
            publishQueue.Add(publish);
            sessionsPublishQueue.Add(publish);
        }

        /// <summary>
        /// Publish outgoing session messages for a client
        /// </summary>
        /// <param name="clientId">Client Id to send outgoing session messages</param>
        public void PublishSession(string clientId)
        {
            clientsForSession.Add(clientId);
        }

        #endregion

        public void ProcessSessionsQueue()
        {
            var clientId = clientsForSession.Take();
            var session = sessionManager.GetSession(clientId);

            MqttMsgPublish outgoingMsg;
            while (session.OutgoingMessages.TryDequeue(out outgoingMsg))
            {
                var query = from s in session.Subscriptions
                            where new Regex(s.Topic).IsMatch(outgoingMsg.Topic)

                            // check for topics based also on wildcard with regex
                            select s;

                var subscription = query.FirstOrDefault();

                if (subscription != null)
                {
                    var qosLevel = subscription.QosLevel < outgoingMsg.QosLevel
                                       ? subscription.QosLevel
                                       : outgoingMsg.QosLevel;
                    incommingMessageHandler.Publish(
                        subscription.Connection,
                        outgoingMsg.Topic,
                        outgoingMsg.Message,
                        qosLevel,
                        outgoingMsg.Retain);
                }
            }
        }

        public void ProcessPublishQueue()
        {
            var publish = (MqttMsgPublish)publishQueue.Take();
            if (publish != null)
            {
                // get all subscriptions for a topic
                var subscriptions = subscriptionManager.GetSubscriptionsByTopic(publish.Topic);
                foreach (var subscription in subscriptions)
                {
                    var qosLevel = subscription.QosLevel < publish.QosLevel ? subscription.QosLevel : publish.QosLevel;

                    // send PUBLISH message to the current subscriber
                    incommingMessageHandler.Publish(
                        subscription.Connection,
                        publish.Topic,
                        publish.Message,
                        qosLevel,
                        publish.Retain);
                }
            }
        }

        public void ProcessSessionPublishQueue()
        {
            var publish = (MqttMsgPublish)sessionsPublishQueue.Take();
            if (publish != null)
            {
                // get all sessions
                var sessions = sessionManager.GetSessions();

                if (sessions != null && sessions.Count > 0)
                {
                    foreach (var session in sessions)
                    {
                        var query = from s in session.Subscriptions
                                    where new Regex(s.Topic).IsMatch(publish.Topic)
                                    select s;

                        var comparer =
                            new MqttSubscriptionComparer(
                                MqttSubscriptionComparer.MqttSubscriptionComparerType.OnClientId);

                        // consider only session active for client disconnected (not online)
                        if (session.Connection == null)
                        {
                            foreach (var subscription in query.Distinct(comparer))
                            {
                                // TODO There has to be something wrong here
                                session.OutgoingMessages.Enqueue(publish);
                            }
                        }
                    }
                }
            }
        }
    }
}