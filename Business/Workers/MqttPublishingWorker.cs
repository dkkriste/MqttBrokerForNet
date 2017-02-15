namespace MqttBrokerForNet.Business.Workers
{
    using System;
    using System.Threading;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;

    public class MqttPublishingWorker : MqttWorker, IMqttPublishingWorker
    {
        private readonly ILogginHandler logginHandler;

        private readonly IMqttPublishingManager publishingManager;

        private int numberOfMessagesPublished;

        public MqttPublishingWorker(ILogginHandler logginHandler, IMqttPublishingManager publishingManager)
        {
            this.logginHandler = logginHandler;
            this.publishingManager = publishingManager;
        }

        public override void Start()
        {
            base.Start();
            Fx.StartThread(this.PublishThread);
            Fx.StartThread(this.ClientsForSessionThread);
            Fx.StartThread(this.SessionPublishThread);
        }

        public void PeriodicLogging()
        {
            //logginHandler.LogMetric(this, LoggerConstants.PublishQueueSize, publishingManager.PublishQueue.Count);

            var messagesPublishedCopy = numberOfMessagesPublished;
            logginHandler.LogMetric(this, LoggerConstants.NumberOfMessagesPublished, messagesPublishedCopy);

            Interlocked.Add(ref numberOfMessagesPublished, -messagesPublishedCopy);
        }

        #region Worker threads

        private void ClientsForSessionThread()
        {
            while (this.IsRunning)
            {
                try
                {
                    publishingManager.ProcessSessionsQueue();
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }

        private void PublishThread()
        {
            while (this.IsRunning)
            {
                try
                {
                    Interlocked.Increment(ref numberOfMessagesPublished);
                    publishingManager.ProcessPublishQueue();
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }

        private void SessionPublishThread()
        {
            while (this.IsRunning)
            {
                try
                {
                    publishingManager.ProcessSessionPublishQueue();
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