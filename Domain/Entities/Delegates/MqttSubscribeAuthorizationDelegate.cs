namespace MqttBrokerForNet.Domain.Entities.Delegates
{
    public delegate bool MqttSubscribeAuthorizationDelegate(MqttConnection connection, string topic);
}