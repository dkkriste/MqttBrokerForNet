namespace MqttBrokerForNet.Business.Managers
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text.RegularExpressions;

    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttRetainedMessageManager : IMqttRetainedMessageManager
    {
        #region Static Fields

        private static readonly ConcurrentDictionary<string, MqttMsgPublish> RetainedMessages;

        // subscriptions to send retained messages (new subscriber or reconnected client)
        private static readonly BlockingCollection<MqttSubscription> SubscribersForRetained;

        #endregion

        private readonly IMqttIncommingMessageHandler incommingMessageHandler;

        private readonly IMqttSubscriptionManager subscriptionManager;

        #region Constructors and Destructors

        static MqttRetainedMessageManager()
        {
            // create empty list for retained messages
            RetainedMessages = new ConcurrentDictionary<string, MqttMsgPublish>();

            // create empty list for destination subscribers for retained message
            SubscribersForRetained = new BlockingCollection<MqttSubscription>();
        }

        public MqttRetainedMessageManager(IMqttIncommingMessageHandler incommingMessageHandler, IMqttSubscriptionManager subscriptionManager)
        {
            this.incommingMessageHandler = incommingMessageHandler;
            this.subscriptionManager = subscriptionManager;
        }

        #endregion

        #region Public Methods and Operators

        public void CheckForAndSetRetainedMessage(MqttMsgPublish publish)
        {
            if (publish.Retain)
            {
                if (RetainedMessages.ContainsKey(publish.Topic))
                {
                    if (publish.Message.Length == 0)
                    {
                        MqttMsgPublish oldRetained;
                        RetainedMessages.TryRemove(publish.Topic, out oldRetained);
                    }
                    else
                    {
                        // set new retained message for the topic
                        RetainedMessages[publish.Topic] = publish;
                    }
                }
                else
                {
                    RetainedMessages.TryAdd(publish.Topic, publish);
                }
            }
        }

        /// <summary>
        /// Publish retained message for a topic to a client
        /// </summary>
        /// <param name="topic">Topic to search for a retained message</param>
        /// <param name="clientId">Client Id to send retained message</param>
        public void PublishRetaind(string topic, MqttConnection connection)
        {
            var subscription = subscriptionManager.GetSubscription(topic, connection);

            // add subscription to list of subscribers for receiving retained messages
            if (subscription != null)
            {
                SubscribersForRetained.Add(subscription);
            }
        }

        public void ProcessSubscribersForRetainedQueue()
        {
            var subscription = SubscribersForRetained.Take();
            var query = from p in RetainedMessages
                        where new Regex(subscription.Topic).IsMatch(p.Key)
                        select p.Value;

            foreach (var retained in query)
            {
                var qosLevel = subscription.QosLevel < retained.QosLevel ? subscription.QosLevel : retained.QosLevel;

                // send PUBLISH message to the current subscriber
                incommingMessageHandler.Publish(
                    subscription.Connection,
                    retained.Topic,
                    retained.Message,
                    qosLevel,
                    retained.Retain);
            }
        }

        #endregion
    }
}