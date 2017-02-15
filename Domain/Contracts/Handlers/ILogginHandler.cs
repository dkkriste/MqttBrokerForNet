namespace MqttBrokerForNet.Domain.Contracts.Handlers
{
    using System;

    public interface ILogginHandler
    {
        void LogException(object sender, Exception exception);

        void LogEvent(object sender, string eventName, string message);

        void LogMetric(object sender, string metricName, double value);
    }
}