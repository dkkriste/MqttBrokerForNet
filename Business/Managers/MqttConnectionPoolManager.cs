using Microsoft.Extensions.Options;
using MqttBrokerForNet.Domain.Entities.Configuration;

namespace MqttBrokerForNet.Business.Managers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttConnectionPoolManager : IMqttConnectionPoolManager
    {
        #region Fields

        private readonly ILogginHandler logginHandler;

        private readonly ConcurrentStack<MqttConnection> unconnectedClientPool;

        private int numberOfConnectionsGotten;

        private int numberOfConnectionsReturned;

        #endregion

        #region Constructors and Destructors

        public MqttConnectionPoolManager(IMqttConnectionFactory connectionFactory, IMqttTcpReceiver receiver, ILogginHandler logginHandler, IOptions<MqttNetworkOptions> networkOptions)
        {
            this.logginHandler = logginHandler;
            numberOfConnectionsGotten = 0;
            numberOfConnectionsReturned = 0;

            unconnectedClientPool = new ConcurrentStack<MqttConnection>();
            var connections = connectionFactory.Create(networkOptions.Value.MaxConnections, networkOptions.Value.ReadAndSendBufferSize);
            for (var i = 0; i < networkOptions.Value.MaxConnections; i++)
            {
                connections[i].ReceiveSocketAsyncEventArgs.Completed += receiver.ReceiveCompleted;
                unconnectedClientPool.Push(connections[i]);
            }
        }

        #endregion

        public void ReturnConnection(MqttConnection connection)
        {
            try
            {
                connection.ResetSocket();
            }
            catch (Exception)
            {
            }

            connection.Reset();

            Interlocked.Increment(ref numberOfConnectionsReturned);

            unconnectedClientPool.Push(connection);
        }

        public MqttConnection GetConnection()
        {
            MqttConnection connection;
            if (unconnectedClientPool.TryPop(out connection))
            {
                Interlocked.Increment(ref numberOfConnectionsGotten);
                return connection;
            }

            var exception = new Exception("Maximum number of connections reached");
            logginHandler.LogException(this, exception);

            throw exception;
        }
    }
}