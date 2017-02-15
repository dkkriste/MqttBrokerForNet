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
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Entities.Delegates;

    /// <summary>
    /// Manager for User Access Control
    /// </summary>
    public class MqttSecurityManager : IMqttSecurityManager
    {
        #region Public Properties

        public MqttPublishAuthorizationDelegate PublishAuthorization { get; set; }

        public MqttSubscribeAuthorizationDelegate SubscribeAuthorization { get; set; }

        /// <summary>
        /// User authentication method
        /// </summary>
        public MqttUserAuthenticationDelegate UserAuthentication { get; set; }

        #endregion

        #region Public Methods and Operators

        public bool AuthorizePublish(MqttConnection connection, string topic)
        {
            return PublishAuthorization == null || PublishAuthorization(connection, topic);
        }

        public bool AuthorizeSubscriber(MqttConnection connection, string topic)
        {
            return SubscribeAuthorization == null || SubscribeAuthorization(connection, topic);
        }

        /// <summary>
        /// Execute user authentication
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <returns>Access granted or not</returns>
        public bool AuthenticateUser(string username, string password)
        {
            return UserAuthentication == null || UserAuthentication(username, password);
        }

        #endregion
    }
}