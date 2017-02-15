namespace MqttBrokerForNet.Business.Factories
{
    using System.Net.Sockets;

    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttConnectionFactory : IMqttConnectionFactory
    {
        private readonly ISocketAsyncEventArgsFactory socketAsyncEventArgsFactory;

        public MqttConnectionFactory(ISocketAsyncEventArgsFactory socketAsyncEventArgsFactory)
        {
            this.socketAsyncEventArgsFactory = socketAsyncEventArgsFactory;
        }

        public MqttConnection[] Create(int numberOfConnection, int bufferBytesAllocatedForEachSaea)
        {
            var sockets = socketAsyncEventArgsFactory.Create(numberOfConnection, bufferBytesAllocatedForEachSaea);
            var connections = new MqttConnection[numberOfConnection];

            for (var i = 0; i < numberOfConnection; i++)
            {
                connections[i] = new MqttConnection(sockets[i]);
            }

            return connections;
        } 
    }
}