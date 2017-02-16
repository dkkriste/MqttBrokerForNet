namespace MqttBrokerForNet.Domain.Entities.Configuration
{
    public class MqttBrokerOptions
    {
        public int NumberOfConnectionManagers { get; set; }

        public int InitialNumberOfRawMessages { get; set; }
    }
}