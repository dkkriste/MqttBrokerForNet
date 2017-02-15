namespace MqttBrokerForNet.Business.Workers
{
    using MqttBrokerForNet.Business.Utility;
    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Workers;

    public class MqttLoadbalancingWorker : MqttWorker, IMqttLoadbalancingWorker
    {
        private readonly IMqttLoadbalancingManager loadbalancingManager;

        public MqttLoadbalancingWorker(IMqttLoadbalancingManager loadbalancingManager)
        {
            this.loadbalancingManager = loadbalancingManager;
        }

        public override void Start()
        {
            base.Start();
            Fx.StartThread(Loadbalancer);
        }

        private void Loadbalancer()
        {
            while (this.IsRunning)
            {
               loadbalancingManager.ProcessIncommingConnections();
            }
        }
    }
}