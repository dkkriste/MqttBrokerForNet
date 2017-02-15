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

using Microsoft.Extensions.Options;
using MqttBrokerForNet.Domain.Entities.Configuration;

namespace MqttBrokerForNet.Business
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using MqttBrokerForNet.Business.Managers;
    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Business.Workers;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Contracts.Workers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Entities.Delegates;

    public class MqttBroker  
    {
        #region Static Fields

        // clients connected list
        private static readonly ConcurrentDictionary<string, MqttConnection> AllConnectedClients =
            new ConcurrentDictionary<string, MqttConnection>();

        #endregion

        #region Fields

        private readonly ILogginHandler logginHandler;

        private readonly IMqttConnectionWorker[] connectionWorkers;

        private readonly IMqttAsyncTcpSocketListener socketListener;

        private readonly IMqttSecurityManager securityManager;

        #endregion

        #region Constructors and Destructors

        public MqttBroker(ILogginHandler logginHandler, IOptions<MqttBrokerOptions> options)
        {
            //MqttAsyncTcpSender.Init(networkOptions);

            this.logginHandler = logginHandler;
            securityManager = new MqttSecurityManager();

            var numberOfProcessingManagersNeeded = options.Value.NumberOfConnectionWorkers;
            connectionWorkers = new MqttConnectionWorker[numberOfProcessingManagersNeeded];
            for (var i = 0; i < connectionWorkers.Length; i++)
            {
                //connectionWorkers[i] = new MqttConnectionWorker(
                //    logginHandler,
                //    connectionPoolManager,
                //    securityManager,
                //    tcpReceiver, null);
            }

            //loadbalancingManager = new MqttLoadbalancingManager(logginHandler, connectionWorkers);

            //socketListener = new MqttAsyncTcpSocketListener(
            //    loadbalancingManager,
            //    connectionPoolManager,
            //    networkOptions);
        }

        #endregion

        #region Public Properties

        public MqttPublishAuthorizationDelegate PublishAuth
        {
            get
            {
                return securityManager.PublishAuthorization;
            }

            set
            {
                securityManager.PublishAuthorization = value;
            }
        }

        public MqttSubscribeAuthorizationDelegate SubscribeAuth
        {
            get
            {
                return securityManager.SubscribeAuthorization;
            }

            set
            {
                securityManager.SubscribeAuthorization = value;
            }
        }

        public MqttUserAuthenticationDelegate UserAuth
        {
            get
            {
                return securityManager.UserAuthentication;
            }

            set
            {
                securityManager.UserAuthentication = value;
            }
        }

        #endregion

        #region Public Methods and Operators

        public static ICollection<MqttConnection> GetAllConnectedClients()
        {
            return AllConnectedClients.Values;
        }

        /// <summary>
        /// Return reference to a client with a specified Id is already connected
        /// </summary>
        /// <param name="clientId">Client Id to verify</param>
        /// <returns>Reference to client</returns>
        public static MqttConnection GetClientConnection(string clientId)
        {
            if (AllConnectedClients.TryGetValue(clientId, out MqttConnection connectedClient))
            {
                return connectedClient;
            }

            return null;
        }

        public static bool TryAddClientConnection(string clientId, MqttConnection connection)
        {
            return AllConnectedClients.TryAdd(clientId, connection);
        }

        public static bool TryRemoveClientConnection(string clientId, out MqttConnection connection)
        {
            return AllConnectedClients.TryRemove(clientId, out connection);
        }

        public void PeriodicLogging()
        {
            logginHandler.LogMetric(this, LoggerConstants.NumberOfConnectedClients, AllConnectedClients.Count);

            //loadbalancingManager.PeriodicLogging();
            //connectionPoolManager.PeriodicLogging();

            foreach (var processingManager in connectionWorkers)
            {
                //processingManager.PeriodicLogging();
            }
        }

        public void Start()
        {
            socketListener.Start();
            //loadbalancingManager.Start();
            //MqttRetainedMessageManager.Start();
            //MqttKeepAliveManager.Start();

            foreach (var processingManager in connectionWorkers)
            {
                processingManager.Start();
            }
        }

        public void Stop()
        {
            socketListener.Stop();
            //loadbalancingManager.Stop();
            //MqttRetainedMessageManager.Stop();
            //MqttKeepAliveManager.Stop();

            foreach (var processingManager in connectionWorkers)
            {
                processingManager.Stop();
            }
        }

        #endregion
    }
}