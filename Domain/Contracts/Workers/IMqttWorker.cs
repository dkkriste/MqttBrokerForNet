namespace MqttBrokerForNet.Domain.Contracts.Workers
{
    public interface IMqttWorker
    {
        bool IsRunning { get; }

        void Start();

        void Stop();
    }
}