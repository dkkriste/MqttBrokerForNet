namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using System.Collections.Generic;

    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttSessionManager
    {
        void ClearSession(string clientId);

        MqttBrokerSession GetSession(string clientId);

        IList<MqttBrokerSession> GetSessions();

        void SaveSession(string clientId, MqttClientSession clientSession, List<MqttSubscription> subscriptions);
    }
}