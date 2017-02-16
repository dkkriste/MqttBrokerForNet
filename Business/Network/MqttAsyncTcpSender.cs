using MqttBrokerForNet.Domain.Entities.Configuration;

namespace MqttBrokerForNet.Business.Network
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;

    using Microsoft.Extensions.Options;

    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttAsyncTcpSender : IMqttAsyncTcpSender
    {
        #region Static Fields

        private readonly bool isInitialized;

        private readonly ConcurrentStack<SocketAsyncEventArgs> sendBufferEventArgsPool;

        private readonly BufferManager sendBufferManager;

        #endregion

        #region Public Methods and Operators

        public MqttAsyncTcpSender(IOptions<MqttNetworkOptions> networkOptions)
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            sendBufferManager = new BufferManager(networkOptions.Value.NumberOfSendBuffers, networkOptions.Value.ReadAndSendBufferSize);
            sendBufferEventArgsPool = new ConcurrentStack<SocketAsyncEventArgs>();

            for (var i = 0; i < networkOptions.Value.NumberOfSendBuffers; i++)
            {
                var args = CreateAndSetNewSendArgs();
                sendBufferEventArgsPool.Push(args);
            }
        }

        public void Send(Socket socket, byte[] message)
        {
            SocketAsyncEventArgs socketArgs;
            if (sendBufferEventArgsPool.TryPop(out socketArgs))
            {
                try
                {
                    socketArgs.AcceptSocket = socket;
                    var sendSocketArgs = (SendSocketArgs)socketArgs.UserToken;
                    Buffer.BlockCopy(message, 0, socketArgs.Buffer, sendSocketArgs.BufferOffset, message.Length);
                    socketArgs.SetBuffer(sendSocketArgs.BufferOffset, message.Length);
                    StartSend(socketArgs);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
            else
            {
                throw new Exception("No more SendArgs in pool");
            }
        }

        #endregion

        #region Methods

        private void CloseClientSocket(SocketAsyncEventArgs sendEventArgs)
        {
            try
            {
                sendEventArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }

            var sendSocketArgs = (SendSocketArgs)sendEventArgs.UserToken;
            sendEventArgs.AcceptSocket = null;
            sendEventArgs.SetBuffer(sendSocketArgs.BufferOffset, sendSocketArgs.BufferSize);
            sendBufferEventArgsPool.Push(sendEventArgs);
        }

        private SocketAsyncEventArgs CreateAndSetNewSendArgs()
        {
            var args = new SocketAsyncEventArgs();
            sendBufferManager.SetBuffer(args);
            args.Completed += SendCompleted;
            args.UserToken = new SendSocketArgs(args.Offset, args.Count);
            return args;
        }

        private void ProcessSend(SocketAsyncEventArgs sendEventArgs)
        {
            if (sendEventArgs.SocketError == SocketError.OperationAborted)
            {
                return;
            }

            if (sendEventArgs.SocketError == SocketError.Success)
            {
                if (sendEventArgs.Count == sendEventArgs.BytesTransferred)
                {
                    // Send complete, reset and return to pool
                    var sendSocketArgs = (SendSocketArgs)sendEventArgs.UserToken;
                    sendEventArgs.AcceptSocket = null;
                    sendEventArgs.SetBuffer(sendSocketArgs.BufferOffset, sendSocketArgs.BufferSize);
                    sendBufferEventArgsPool.Push(sendEventArgs);
                }
                else
                {
                    // If some of the bytes in the message have NOT been sent,
                    // then we will need to post another send operation.
                    var sendSocketArgs = (SendSocketArgs)sendEventArgs.UserToken;
                    sendSocketArgs.MessageLength -= sendEventArgs.BytesTransferred;
                    sendSocketArgs.MessageStartFromOffset += sendEventArgs.BytesTransferred;
                    sendEventArgs.SetBuffer(sendSocketArgs.MessageStartFromOffset, sendSocketArgs.MessageLength);
                    var willRaiseEvent = sendEventArgs.AcceptSocket.SendAsync(sendEventArgs);
                    if (!willRaiseEvent)
                    {
                        ProcessSend(sendEventArgs);
                    }
                }
            }
            else
            {
                CloseClientSocket(sendEventArgs);
            }
        }

        private void SendCompleted(object sender, SocketAsyncEventArgs sendEventArgs)
        {
            ProcessSend(sendEventArgs);
        }

        private void StartSend(SocketAsyncEventArgs sendEventArgs)
        {
            var willRaiseEvent = sendEventArgs.AcceptSocket.SendAsync(sendEventArgs);
            if (!willRaiseEvent)
            {
                ProcessSend(sendEventArgs);
            }
        }

        #endregion
    }
}