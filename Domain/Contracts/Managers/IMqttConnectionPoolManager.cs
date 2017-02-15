namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Entities;

    public interface IMqttConnectionPoolManager 
    {
        MqttConnection GetConnection();

        void ReturnConnection(MqttConnection connection);
    }
}