namespace MqttBrokerForNet.Business.Workers
{
    using MqttBrokerForNet.Domain.Contracts.Workers;

    public abstract class MqttWorker : IMqttWorker
    {
        public bool IsRunning { get; private set; }

        public virtual void Start()
        {
            IsRunning = true;
        }

        public virtual void Stop()
        {
            IsRunning = false;
        }
    }
}