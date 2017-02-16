namespace MqttBrokerForNet.Business.Workers
{
    using System;
    using System.Threading;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;

    public class MqttConnectionWorker : MqttWorker, IMqttConnectionWorker
    {
        #region Fields

        private readonly ILogginHandler logginHandler;

        private readonly IMqttConnectionManager[] connectionManagers;

        //private int numberOfAssignedClients;

        //private int numberOfConnectedClients;

        private int numberOfRawMessagsProcessed;

        private int numberOfInflightQueuesProcessed;

        private int numberOfInternalEventQueuesProcessed;

        #endregion

        #region Constructors and Destructors

        public MqttConnectionWorker(
            ILogginHandler logginHandler,
            IMqttConnectionManager[] connectionManagers)
        {
            this.logginHandler = logginHandler;
            this.connectionManagers = connectionManagers;
        }

        #endregion

        #region Public Properties

        //public int AssignedClients => numberOfAssignedClients;

        //public int ConnectedClients => numberOfConnectedClients;

        #endregion

        //public void PeriodicLogging()
        //{
        //    logginHandler.LogMetric(this, LoggerConstants.RawMessageQueueSize, rawMessageQueue.Count);
        //    logginHandler.LogMetric(
        //        this,
        //        LoggerConstants.InflightQueuesToProcessSize,
        //        clientConnectionsWithInflightQueuesToProcess.Count);
        //    logginHandler.LogMetric(
        //        this,
        //        LoggerConstants.EventQueuesToProcessSize,
        //        clientConnectionsWithInternalEventQueuesToProcess.Count);

        //    var rawMessageCopy = numberOfRawMessagsProcessed;
        //    var inflightCopy = numberOfInflightQueuesProcessed;
        //    var internalEventCopy = numberOfInternalEventQueuesProcessed;
        //    logginHandler.LogMetric(this, LoggerConstants.NumberOfRawMessagsProcessed, rawMessageCopy);
        //    logginHandler.LogMetric(this, LoggerConstants.NumberOfInflightQueuesProcessed, inflightCopy);
        //    logginHandler.LogMetric(this, LoggerConstants.NumberOfInternalEventQueuesProcessed, internalEventCopy);

        //    Interlocked.Add(ref numberOfRawMessagsProcessed, -rawMessageCopy);
        //    Interlocked.Add(ref numberOfInflightQueuesProcessed, -inflightCopy);
        //    Interlocked.Add(ref numberOfInternalEventQueuesProcessed, -internalEventCopy);

        //}

        public override void Start()
        {
            base.Start();
            foreach (var connectionManager in connectionManagers)
            {
                Fx.StartThread(() => ProcessRawMessageThread(connectionManager));
                Fx.StartThread(() => ProcessInflightThread(connectionManager));
                Fx.StartThread(() => ProcessInternalEventThread(connectionManager));
            }
        }

        #region ProcessingThreads

        private void ProcessRawMessageThread(IMqttConnectionManager connectionManager)
        {
            while (this.IsRunning)
            {
                try
                {
                    connectionManager.ProcessRawMessageQueue();
                    Interlocked.Increment(ref numberOfRawMessagsProcessed);
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }

        private void ProcessInflightThread(IMqttConnectionManager connectionManager)
        {
            while (this.IsRunning)
            {
                try
                {
                    connectionManager.ProcessInflightQueue();
                    Interlocked.Increment(ref numberOfInflightQueuesProcessed);
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }

        private void ProcessInternalEventThread(IMqttConnectionManager connectionManager)
        {
            while (this.IsRunning)
            {
                try
                {
                    connectionManager.ProcessInternalEventQueue();
                    Interlocked.Increment(ref numberOfInternalEventQueuesProcessed);
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }

        #endregion
    }
}