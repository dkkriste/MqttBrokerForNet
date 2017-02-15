namespace MqttBrokerForNet.Domain.Entities
{
    public class MqttRawMessage
    {
        public byte[] PayloadBuffer { get; set; }       

        public MqttConnection Connection { get; set; }

        public byte MessageType { get; set; }

        public int PayloadLength { get; set; }
    }
}