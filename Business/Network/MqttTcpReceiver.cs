namespace MqttBrokerForNet.Business.Network
{
    using System;
    using System.Net.Sockets;

    using MqttBrokerForNet.Domain.Contracts.Factories;
    using MqttBrokerForNet.Domain.Contracts.Network;
    using MqttBrokerForNet.Domain.Entities;

    public class MqttTcpReceiver : IMqttTcpReceiver
    {
        #region Fields

        private readonly IMqttRawMessageFactory rawMessageFactory;

        #endregion

        #region Constructors and Destructors

        public MqttTcpReceiver(IMqttRawMessageFactory rawMessageFactory)
        {
            this.rawMessageFactory = rawMessageFactory;
        }

        #endregion

        #region Public Methods and Operators

        public void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessReceive(e);
        }

        public void StartReceive(SocketAsyncEventArgs receiveEventArgs)
        {
            try
            {
                var willRaiseEvent = receiveEventArgs.AcceptSocket.ReceiveAsync(receiveEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessReceive(receiveEventArgs);
                }
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Methods

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            try
            {
                e.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }

            var clientConnection = (MqttConnection)e.UserToken;
            clientConnection.OnConnectionClosed();
        }

        private byte GetMessageType(byte[] buffer, ref int offset, ref int remainingBytesToProcess)
        {
            if (remainingBytesToProcess > 0)
            {
                var messageType = buffer[offset++];
                remainingBytesToProcess--;
                return messageType;
            }

            throw new AggregateException();
        }

        private int GetPayloadLength(byte[] buffer, ref int offset, ref int remainingBytesToProcess)
        {
            if (remainingBytesToProcess > 0)
            {
                var multiplier = 1;
                var value = 0;
                var digit = 0;
                do
                {
                    digit = buffer[offset];
                    value += (digit & 127) * multiplier;
                    multiplier *= 128;
                    offset++;
                    remainingBytesToProcess--;
                }
                while ((digit & 128) != 0 && remainingBytesToProcess > 0);

                if ((digit & 128) == 0)
                {
                    return value;
                }
            }

            throw new AggregateException();
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {
            var clientConnection = (MqttConnection)receiveSendEventArgs.UserToken;

            if (receiveSendEventArgs.SocketError == SocketError.OperationAborted)
            {
                return;
            }
            else if (receiveSendEventArgs.SocketError != SocketError.Success)
            {
                CloseClientSocket(receiveSendEventArgs);
                return;
            }
            else if (receiveSendEventArgs.BytesTransferred == 0)
            {
                CloseClientSocket(receiveSendEventArgs);
                return;
            }
            else
            {
                // We got at least one byte
                TryProcessMessage(clientConnection, receiveSendEventArgs);
            }

            StartReceive(receiveSendEventArgs);
        }

        private void TryProcessMessage(MqttConnection connection, SocketAsyncEventArgs receiveSendEventArgs)
        {
            var lastProcessedByteByCompleteMessage = -1;
            var remainingBytesToProcess = connection.PreviouslyReceivedBytes + receiveSendEventArgs.BytesTransferred;
            var bufferOffset = connection.ReceiveSocketOffset;

            while (remainingBytesToProcess > 0)
            {
                try
                {
                    var messageType = GetMessageType(
                        receiveSendEventArgs.Buffer,
                        ref bufferOffset,
                        ref remainingBytesToProcess);
                    var payloadLength = GetPayloadLength(
                        receiveSendEventArgs.Buffer,
                        ref bufferOffset,
                        ref remainingBytesToProcess);
                    if (payloadLength <= remainingBytesToProcess)
                    {
                        var rawMessage = rawMessageFactory.CreateRawMessageWithData(
                            connection,
                            messageType,
                            receiveSendEventArgs.Buffer,
                            bufferOffset,
                            payloadLength);
                        connection.EnqueueRawMessage(rawMessage);
                        bufferOffset += payloadLength;
                        remainingBytesToProcess -= payloadLength;
                        lastProcessedByteByCompleteMessage = bufferOffset - connection.ReceiveSocketOffset;
                    }
                    else
                    {
                        throw new AggregateException();
                    }
                }
                catch (AggregateException)
                {
                    var totalUnprocessedBytes = connection.PreviouslyReceivedBytes
                                                + receiveSendEventArgs.BytesTransferred
                                                - lastProcessedByteByCompleteMessage;
                    if (lastProcessedByteByCompleteMessage > 0)
                    {
                        Buffer.BlockCopy(
                            receiveSendEventArgs.Buffer,
                            connection.ReceiveSocketOffset + lastProcessedByteByCompleteMessage,
                            receiveSendEventArgs.Buffer,
                            connection.ReceiveSocketOffset,
                            totalUnprocessedBytes);
                    }

                    receiveSendEventArgs.SetBuffer(
                        connection.ReceiveSocketOffset + totalUnprocessedBytes,
                        connection.ReceiveSocketBufferSize - totalUnprocessedBytes);
                    connection.PreviouslyReceivedBytes = totalUnprocessedBytes;
                    return;
                }
            }

            receiveSendEventArgs.SetBuffer(connection.ReceiveSocketOffset, connection.ReceiveSocketBufferSize);
        }

        #endregion
    }
}