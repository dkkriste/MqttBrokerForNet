namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttLoadbalancingManager
    {
        void OpenClientConnection(MqttConnection connection);

        void ProcessIncommingConnections();
    }
}