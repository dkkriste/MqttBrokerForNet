namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using System.Collections.Generic;

    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttSubscriptionManager
    {
        MqttSubscription GetSubscription(string topic, MqttConnection connection);

        IEnumerable<MqttSubscription> GetSubscriptionsByTopic(string topic);

        void Subscribe(string topic, byte qosLevel, MqttConnection connection);

        void Unsubscribe(string topic, MqttConnection connection);
    }
}