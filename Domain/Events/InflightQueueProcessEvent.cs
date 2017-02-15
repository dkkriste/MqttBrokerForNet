namespace MqttBrokerForNet.Domain.Events
{
    using MqttBrokerForNet.Domain.Entities;

    public class InflightQueueProcessEvent
    {
        public MqttConnection Connection { get; set; }

        public bool IsCallback { get; set; }

        public int CallbackCreationTime { get; set; }
    }
}