namespace MqttBrokerForNet.Domain.Contracts.Managers
{
    using MqttBrokerForNet.Domain.Entities;
    using MqttBrokerForNet.Domain.Entities.Delegates;

    public interface IMqttSecurityManager
    {
        MqttPublishAuthorizationDelegate PublishAuthorization { get; set; }

        MqttSubscribeAuthorizationDelegate SubscribeAuthorization { get; set; }

        MqttUserAuthenticationDelegate UserAuthentication { get; set; }

        bool AuthorizePublish(MqttConnection connection, string topic);

        bool AuthorizeSubscriber(MqttConnection connection, string topic);

        bool AuthenticateUser(string username, string password);
    }
}