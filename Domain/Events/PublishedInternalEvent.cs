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

namespace MqttBrokerForNet.Domain.Events
{
    using MqttBrokerForNet.Domain.Messages;

    /// <summary>
    /// Internal event for a published message
    /// </summary>
    public class PublishedInternalEvent : InternalEvent
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message published</param>
        /// <param name="isPublished">Publish flag</param>
        public PublishedInternalEvent(MqttMsgBase message, bool isPublished) 
            : base(message)
        {
            this.IsPublished = isPublished;
        }

        #region Properties...

        /// <summary>
        /// Message published (or failed due to retries)
        /// </summary>
        public bool IsPublished { get; internal set; }

        #endregion
    }
}
