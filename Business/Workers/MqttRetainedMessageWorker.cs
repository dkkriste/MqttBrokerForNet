namespace MqttBrokerForNet.Business.Workers
{
    using System;
    using System.Text.RegularExpressions;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;

    public class MqttRetainedMessageWorker : MqttWorker, IMqttRetainedMessageWorker
    {
        private readonly IMqttRetainedMessageManager retainedMessageManager;

        private readonly ILogginHandler logginHandler;

        public MqttRetainedMessageWorker(IMqttRetainedMessageManager retainedMessageManager, ILogginHandler logginHandler)
        {
            this.retainedMessageManager = retainedMessageManager;
            this.logginHandler = logginHandler;
        }

        public override void Start()
        {
            base.Start();
            Fx.StartThread(SubscribersForRetainedThread);
        }

        private void SubscribersForRetainedThread()
        {
            while (this.IsRunning)
            {
                try
                {
                    retainedMessageManager.ProcessSubscribersForRetainedQueue();
                }
                catch (Exception exception)
                {
                    logginHandler.LogException(this, exception);
                }
            }
        }
    }
}