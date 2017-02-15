namespace MqttBrokerForNet.Domain.Entities.Delegates
{
    public delegate bool MqttPublishAuthorizationDelegate(MqttConnection connection, string topic);
}