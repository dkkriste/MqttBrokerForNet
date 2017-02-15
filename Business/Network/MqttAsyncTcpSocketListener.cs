using System.Net;
using Microsoft.Extensions.Options;
using MqttBrokerForNet.Domain.Entities.Configuration;

namespace MqttBrokerForNet.Business.Network
{
    using System.Collections.Concurrent;
    using System.Net.Sockets;

    using MqttBrokerForNet.Domain.Contracts.Managers;
    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Entities;

    // Inspired by http://www.codeproject.com/Articles/83102/C-SocketAsyncEventArgs-High-Performance-Socket-Cod
    public class MqttAsyncTcpSocketListener : IMqttAsyncTcpSocketListener
    {
        #region Fields

        private readonly IMqttLoadbalancingManager loadbalancingManager;

        private readonly IMqttConnectionPoolManager connectionManager;

        private readonly Socket listenSocket;

        // Pool of reusable SocketAsyncEventArgs objects.
        private readonly ConcurrentStack<SocketAsyncEventArgs> poolOfAcceptEventArgs;

        private bool isRunning;

        #endregion

        #region Constructors and Destructors

        public MqttAsyncTcpSocketListener(
            IMqttLoadbalancingManager loadbalancingManager,
            IMqttConnectionPoolManager connectionManager,
            IOptions<MqttNetworkOptions> networkOptions)
        {
            this.loadbalancingManager = loadbalancingManager;
            this.connectionManager = connectionManager;
            poolOfAcceptEventArgs = new ConcurrentStack<SocketAsyncEventArgs>();

            for (var i = 0; i < networkOptions.Value.NumberOfAcceptSaea; i++)
            {
                poolOfAcceptEventArgs.Push(CreateNewSaeaForAccept());
            }

            var endpoint = new IPEndPoint(IPAddress.Any, networkOptions.Value.Port);

            // create the socket which listens for incoming connections
            listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(endpoint);
        }

        #endregion

        #region Properties

        // The number of SocketAsyncEventArgs instances in the pool.
        internal int Count => poolOfAcceptEventArgs.Count;

        #endregion

        #region Public Methods and Operators

        public void Start()
        {
            isRunning = true;
            listenSocket.Listen(1024);
            StartAccept();
        }

        public void Stop()
        {
            isRunning = false;
            listenSocket.Shutdown(SocketShutdown.Receive);
        }

        #endregion

        #region Methods

        private void AcceptEventArgCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private SocketAsyncEventArgs CreateNewSaeaForAccept()
        {
            var acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += AcceptEventArgCompleted;
            return acceptEventArg;
        }

        private void HandleBadAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            acceptEventArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            poolOfAcceptEventArgs.Push(acceptEventArgs);
        }

        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs.SocketError != SocketError.Success)
            {
                StartAccept();
                HandleBadAccept(acceptEventArgs);
                return;
            }

            StartAccept();

            var clientConnection = connectionManager.GetConnection();
            if (clientConnection != null)
            {
                clientConnection.ReceiveSocketAsyncEventArgs.AcceptSocket = acceptEventArgs.AcceptSocket;
                loadbalancingManager.OpenClientConnection(clientConnection);
            }

            acceptEventArgs.AcceptSocket = null;
            poolOfAcceptEventArgs.Push(acceptEventArgs);
        }

        private void StartAccept()
        {
            if (!isRunning)
            {
                return;
            }

            SocketAsyncEventArgs acceptEventArg;
            if (!poolOfAcceptEventArgs.TryPop(out acceptEventArg))
            {
                acceptEventArg = CreateNewSaeaForAccept();
            }

            var willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        #endregion
    }
}