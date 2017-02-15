namespace MqttBrokerForNet.Business.Workers
{
    using System;
    using System.Threading;

    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;
    using MqttBrokerForNet.Domain.Messages;

    public class MqttKeepAliveWorker : MqttWorker, IMqttKeepAliveWorker
    {
        #region Public Methods and Operators

        public override void Start()
        {
            base.Start();
            Fx.StartThread(this.KeepAliveThread);
        }

        #endregion

        #region Methods

        private void KeepAliveThread()
        {
            var sleepPeriod = new TimeSpan(0, 0, MqttMsgConnect.KEEP_ALIVE_PERIOD_DEFAULT);
            while (this.IsRunning)
            {
                try
                {
                    var now = Environment.TickCount;
                    foreach (var clientConnection in MqttBroker.GetAllConnectedClients())
                    {
                        if (clientConnection.IsRunning && clientConnection.IsConnected)
                        {
                            var delta = now - clientConnection.LastCommunicationTime;
                            if (delta >= clientConnection.KeepAlivePeriod)
                            {
                                clientConnection.OnConnectionClosed();
                            }
                        }
                    }

                    Thread.Sleep(sleepPeriod);
                }
                catch (Exception)
                {
                }
            }
        }

        #endregion
    }
}