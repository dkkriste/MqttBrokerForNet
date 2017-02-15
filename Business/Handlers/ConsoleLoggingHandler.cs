namespace MqttBrokerForNet.Business.Handlers
{
    using System;

    using MqttBrokerForNet.Domain.Contracts.Handlers;

    public class ConsoleLogginHandler : ILogginHandler
    {
        public void LogException(object sender, Exception exception)
        {
            Console.WriteLine(sender.GetType().Name + ": " + exception);
        }

        public void LogEvent(object sender, string eventName, string message)
        {
            Console.WriteLine(sender.GetType().Name + ": " + eventName + " " + message);
        }

        public void LogMetric(object sender, string metricName, double value)
        {
            Console.WriteLine(sender.GetType().Name + ": " + metricName + " " + value);
        }
    }
}