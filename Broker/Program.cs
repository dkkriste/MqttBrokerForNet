namespace MqttBrokerForNet.Broker
{
    using System;

    using Microsoft.Extensions.DependencyInjection;

    using MqttBrokerForNet.Business.Factories;
    using MqttBrokerForNet.Business.Handlers;
    using MqttBrokerForNet.Business.Network;
    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Network;

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            IServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            //Application application = new Application(serviceCollection);
            // Run
            // ...
        }


        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Factories
            serviceCollection.AddSingleton<IMqttConnectionFactory, MqttConnectionFactory>();
            serviceCollection.AddSingleton<IMqttMessageFactory, MqttMessageFactory>();
            serviceCollection.AddSingleton<IMqttRawMessageFactory, MqttRawMessageFactory>();
            serviceCollection.AddSingleton<ISocketAsyncEventArgsFactory, SocketAsyncEventArgsFactory>();

            // Handlers
            serviceCollection.AddSingleton<ILogginHandler, ConsoleLogginHandler>();
            serviceCollection.AddSingleton<IMqttConnectionInflightHandler, MqttConnectionInflightHandler>();
            serviceCollection.AddSingleton<IMqttConnectionInternalEventHandler, MqttConnectionInternalEventHandler>();
            serviceCollection.AddSingleton<IMqttIncommingMessageHandler, MqttIncommingMessageHandler>();
            serviceCollection.AddSingleton<IMqttOutgoingMessageHandler, MqttOutgoingMessageHandler>();

            // Managers
            serviceCollection.AddTransient<ILogginHandler, ConsoleLogginHandler>(); // HER ER JEG NÅ

            // Network
            serviceCollection.AddSingleton<IMqttAsyncTcpSender, MqttAsyncTcpSender>();
            serviceCollection.AddSingleton<IMqttAsyncTcpSocketListener, MqttAsyncTcpSocketListener>();
            serviceCollection.AddSingleton<IMqttTcpReceiver, MqttTcpReceiver>();

            // Workers

            //ILoggerFactory loggerFactory = new Logging.LoggerFactory();
            //serviceCollection.AddInstance<ILoggerFactory>(loggerFactory);
        }
    }
}