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

        private readonly IMqttConnectionWorker[] workers;

        private int indexOfProcessingManagerToGetNextConnection;

        private bool isRunning;

        private int numberOfConnectionsLoadbalanced;

        #endregion

        #region Constructors and Destructors

        public MqttLoadbalancingManager(ILogginHandler logginHandler, IMqttConnectionWorker[] workers)
        {
            this.logginHandler = logginHandler;
            this.workers = workers;
            loadbalanceAwaitHandler = new AutoResetEvent(false);
        }

        #endregion

        #region Public Methods and Operators

        public void OpenClientConnection(MqttConnection connection)
        {
            //processingManagers[indexOfProcessingManagerToGetNextConnection].OpenClientConnection(connection);
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
            for (var i = 0; i < workers.Length; i++)
            {
                //if (workers[i].AssignedClients < leastNumberOfConnections)
                //{
                //    leastNumberOfConnections = workers[i].AssignedClients;
                //    loadbalancerWithTheLeastConnections = i;
                //}
            }

            indexOfProcessingManagerToGetNextConnection = loadbalancerWithTheLeastConnections;
        }

        #endregion
    }
}