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
    using System.Collections.Generic;

    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Messages;

    /// <summary>
    /// Manager for client session
    /// </summary>
    public class MqttSessionManager : IMqttSessionManager
    {
        #region Static Fields

        // subscription info for each client
        private static readonly ConcurrentDictionary<string, MqttBrokerSession> Sessions;

        #endregion

        #region Constructors and Destructors

        static MqttSessionManager()
        {
            Sessions = new ConcurrentDictionary<string, MqttBrokerSession>();
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Clear session for a client (all related subscriptions)
        /// </summary>
        /// <param name="clientId">Client Id to clear session</param>
        public void ClearSession(string clientId)
        {
            MqttBrokerSession sessionToBeRemoved;
            if (Sessions.TryRemove(clientId, out sessionToBeRemoved))
            {
                sessionToBeRemoved.Clear();
            }
        }

        /// <summary>
        /// Get session for a client
        /// </summary>
        /// <param name="clientId">Client Id to get subscriptions</param>
        /// <returns>Subscriptions for the client</returns>
        public MqttBrokerSession GetSession(string clientId)
        {
            MqttBrokerSession session;
            Sessions.TryGetValue(clientId, out session);

            return session;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IList<MqttBrokerSession> GetSessions()
        {
            return new List<MqttBrokerSession>(Sessions.Values);
        }

        /// <summary>
        /// Save session for a client (all related subscriptions)
        /// </summary>
        /// <param name="clientId">Client Id to save subscriptions</param>
        /// <param name="clientSession">Client session with inflight messages</param>
        /// <param name="subscriptions">Subscriptions to save</param>
        public void SaveSession(
            string clientId,
            MqttClientSession clientSession,
            List<MqttSubscription> subscriptions)
        {
            var session = Sessions.GetOrAdd(clientId, new MqttBrokerSession { ClientId = clientId });

            // null reference to disconnected client
            session.Connection = null;

            // update subscriptions
            session.Subscriptions = new List<MqttSubscription>();
            foreach (var subscription in subscriptions)
            {
                session.Subscriptions.Add(
                    new MqttSubscription(subscription.ClientId, subscription.Topic, subscription.QosLevel, null));
            }

            // update inflight messages
            session.InflightMessages = new ConcurrentDictionary<string, MqttMsgContext>();
            foreach (var msgContext in clientSession.InflightMessages.Values)
            {
                session.InflightMessages.TryAdd(msgContext.Key, msgContext);
            }
        }

        #endregion
    }
}