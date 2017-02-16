﻿using System.IO;
using Microsoft.Extensions.Configuration;
using MqttBrokerForNet.Business;
using MqttBrokerForNet.Business.Managers;
using MqttBrokerForNet.Business.Workers;
using MqttBrokerForNet.Domain.Contracts.Managers;
using MqttBrokerForNet.Domain.Contracts.Workers;
using MqttBrokerForNet.Domain.Entities.Configuration;

namespace MqttBrokerForNet.Broker
{
    using System;
    using System.Linq;
    using System.Threading;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using MqttBrokerForNet.Business.Factories;
    using MqttBrokerForNet.Business.Handlers;
    using MqttBrokerForNet.Business.Network;
    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Contracts.Handlers;
    using MqttBrokerForNet.Domain.Contracts.Network;

    public class Program
    {
        private static IConfigurationRoot Configuration { get; set; }

        public static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("mqttsettings.json");
            Configuration = builder.Build();
            
            IServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var broker = new MqttBroker(serviceProvider);
            broker.Start();

            while (true)
            {
                Console.WriteLine("Broker running");
                Thread.Sleep(new TimeSpan(0, 1, 0));
            }

        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddOptions();

            serviceCollection.Configure<MqttNetworkOptions>(Configuration.GetSection("mqttNetworkOptions"));
            serviceCollection.Configure<MqttBrokerOptions>(Configuration.GetSection("mqttBrokerOptions"));

            var options = Configuration.GetSection("mqttBrokerOptions");
            var brokerOptions = options.Get<MqttBrokerOptions>();

            // Factories - Singleton
            serviceCollection.AddSingleton<IMqttConnectionFactory, MqttConnectionFactory>();
            serviceCollection.AddSingleton<IMqttMessageFactory, MqttMessageFactory>();
            serviceCollection.AddSingleton<IMqttRawMessageFactory, MqttRawMessageFactory>();
            serviceCollection.AddSingleton<ISocketAsyncEventArgsFactory, SocketAsyncEventArgsFactory>();

            // Handlers - Singleton
            serviceCollection.AddSingleton<ILogginHandler, ConsoleLogginHandler>();
            serviceCollection.AddSingleton<IMqttConnectionInflightHandler, MqttConnectionInflightHandler>();
            serviceCollection.AddSingleton<IMqttConnectionInternalEventHandler, MqttConnectionInternalEventHandler>();
            serviceCollection.AddSingleton<IMqttIncommingMessageHandler, MqttIncommingMessageHandler>();
            serviceCollection.AddSingleton<IMqttOutgoingMessageHandler, MqttOutgoingMessageHandler>();

            // Handlers - Transient
            serviceCollection.AddTransient<ILogginHandler, ConsoleLogginHandler>();

            // Managers - Singleton
            serviceCollection.AddSingleton<IMqttConnectionPoolManager, MqttConnectionPoolManager>();
            serviceCollection.AddSingleton<IMqttLoadbalancingManager, MqttLoadbalancingManager>();
            serviceCollection.AddSingleton<IMqttRetainedMessageManager, MqttRetainedMessageManager>();
            serviceCollection.AddSingleton<IMqttSubscriptionManager, MqttSubscriptionManager>();
            for (var i = 0; i < brokerOptions.NumberOfConnectionManagers; i++)
            {
                serviceCollection.AddSingleton<IMqttConnectionManager, MqttConnectionManager>();
            }

            serviceCollection.AddSingleton(c => c.GetServices<IMqttConnectionManager>().ToArray());

            // Managers - transient
            serviceCollection.AddTransient<IMqttPublishingManager, MqttPublishingManager>();
            serviceCollection.AddTransient<IMqttSecurityManager, MqttSecurityManager>();
            serviceCollection.AddTransient<IMqttSessionManager, MqttSessionManager>();

            // Network - Singleton
            serviceCollection.AddSingleton<IMqttAsyncTcpSender, MqttAsyncTcpSender>();
            serviceCollection.AddSingleton<IMqttAsyncTcpSocketListener, MqttAsyncTcpSocketListener>();
            serviceCollection.AddSingleton<IMqttTcpReceiver, MqttTcpReceiver>();

            // Workers - Singleton
            serviceCollection.AddSingleton<IMqttRetainedMessageWorker, MqttRetainedMessageWorker>();

            // Workers - Transient
            serviceCollection.AddTransient<IMqttConnectionWorker, MqttConnectionWorker>();
            serviceCollection.AddTransient<IMqttKeepAliveWorker, MqttKeepAliveWorker>();
            serviceCollection.AddTransient<IMqttLoadbalancingWorker, MqttLoadbalancingWorker>();
            serviceCollection.AddTransient<IMqttPublishingWorker, MqttPublishingWorker>();
        }
    }
}