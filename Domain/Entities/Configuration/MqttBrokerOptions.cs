namespace MqttBrokerForNet.Domain.Entities.Configuration
{
    public class MqttBrokerOptions
    {
        public int NumberOfConnectionWorkers { get; set; }

        public int InitialNumberOfRawMessages { get; set; }
    }
}