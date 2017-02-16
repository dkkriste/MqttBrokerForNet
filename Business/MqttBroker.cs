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

namespace MqttBrokerForNet.Business
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using Microsoft.Extensions.DependencyInjection;

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

        private readonly ILogginHandler loggingHandler;

        private readonly IMqttConnectionWorker connectionWorker;

        private readonly IMqttKeepAliveWorker keepAliveWorker;

        private readonly IMqttLoadbalancingWorker loadbalancingWorker;

        private readonly IMqttAsyncTcpSocketListener socketListener;

        private readonly IMqttPublishingWorker publishingWorker;

        private readonly IMqttRetainedMessageWorker retainedMessageWorker;

        private readonly IMqttSecurityManager securityManager;

        #endregion

        #region Constructors and Destructors

        public MqttBroker(IServiceProvider serviceProvider)
        {
            loggingHandler = serviceProvider.GetService<ILogginHandler>();
            connectionWorker = serviceProvider.GetService<IMqttConnectionWorker>();
            loadbalancingWorker = serviceProvider.GetService<IMqttLoadbalancingWorker>();
            socketListener = serviceProvider.GetService<IMqttAsyncTcpSocketListener>();
            keepAliveWorker = serviceProvider.GetService<IMqttKeepAliveWorker>();
            publishingWorker = serviceProvider.GetService<IMqttPublishingWorker>();
            retainedMessageWorker = serviceProvider.GetService<IMqttRetainedMessageWorker>();
            securityManager = serviceProvider.GetService<IMqttSecurityManager>();
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

        public void Start()
        {
            keepAliveWorker.Start();
            loadbalancingWorker.Start();
            socketListener.Start();
            publishingWorker.Start();
            retainedMessageWorker.Start();
            connectionWorker.Start();
        }

        public void Stop()
        {
            keepAliveWorker.Stop();
            loadbalancingWorker.Stop();
            socketListener.Stop();
            publishingWorker.Stop();
            retainedMessageWorker.Stop();
            connectionWorker.Stop();
        }

        #endregion
    }
}