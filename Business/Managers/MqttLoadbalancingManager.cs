namespace MqttBrokerForNet.Business.Managers
{
    using System.Threading;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Business.Workers;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttLoadbalancingManager : IMqttLoadbalancingManager
    {
        #region Fields

        private readonly AutoResetEvent loadbalanceAwaitHandler;

        private readonly ILogginHandler logginHandler;

        private readonly IMqttConnectionManager[] connectionManagers;

        private int indexOfProcessingManagerToGetNextConnection;

        private int numberOfConnectionsLoadbalanced;

        #endregion

        #region Constructors and Destructors

        public MqttLoadbalancingManager(ILogginHandler logginHandler, IMqttConnectionManager[] connectionManagers)
        {
            this.logginHandler = logginHandler;
            this.connectionManagers = connectionManagers;
            loadbalanceAwaitHandler = new AutoResetEvent(false);
        }

        #endregion

        #region Public Methods and Operators

        public void OpenClientConnection(MqttConnection connection)
        {
            connectionManagers[indexOfProcessingManagerToGetNextConnection].OpenClientConnection(connection);
            loadbalanceAwaitHandler.Set();
            Interlocked.Increment(ref numberOfConnectionsLoadbalanced);
        }

        public void PeriodicLogging()
        {
            var loadbalancedCopy = numberOfConnectionsLoadbalanced;
            logginHandler.LogMetric(this, LoggerConstants.NumberOfConnectionsLoadbalanced, loadbalancedCopy);
            Interlocked.Add(ref numberOfConnectionsLoadbalanced, -loadbalancedCopy);
        }

        public void ProcessIncommingConnections()
        {
            loadbalanceAwaitHandler.WaitOne();

            var loadbalancerWithTheLeastConnections = 0;
            var leastNumberOfConnections = int.MaxValue;
            for (var i = 0; i < connectionManagers.Length; i++)
            {
                if (connectionManagers[i].AssignedClients < leastNumberOfConnections)
                {
                    leastNumberOfConnections = connectionManagers[i].AssignedClients;
                    loadbalancerWithTheLeastConnections = i;
                }
            }

            indexOfProcessingManagerToGetNextConnection = loadbalancerWithTheLeastConnections;
        }

        #endregion
    }
}