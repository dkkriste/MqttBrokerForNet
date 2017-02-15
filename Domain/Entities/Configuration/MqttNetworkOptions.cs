using System.Net;

namespace MqttBrokerForNet.Domain.Entities.Configuration
{
    public class MqttNetworkOptions
    {
        public int NumberOfAcceptSaea { get; set; }

        public int MaxConnections { get; set; }

        public int Port { get; set; }
        
        public int IndividualMessageBufferSize { get; set; }

        public int NumberOfSendBuffers { get; set; }

        public int ReadAndSendBufferSize { get; set; }
    }
}