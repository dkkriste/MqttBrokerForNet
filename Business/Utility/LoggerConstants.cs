namespace MqttBrokerForNet.Business.Utility
{
    public class LoggerConstants
    {
        #region Constants

        #region MqttBroker

        public const string NumberOfConnectedClients = "NumberOfConnectedClients";

        #endregion

        #region Loadbalancer

        public const string NumberOfConnectionsLoadbalanced = "NumberOfConnectionsLoadbalanced";

        #endregion

        #endregion

        #region MqttConnectionProcessingManager

        public const string RawMessageQueueSize = "RawMessageQueueSize";

        public const string InflightQueuesToProcessSize = "InflightQueuesToProcessSize";

        public const string EventQueuesToProcessSize = "EventQueuesToProcessSize";

        public const string NumberOfRawMessagsProcessed = "NumberOfRawMessagsProcessed";

        public const string NumberOfInflightQueuesProcessed = "NumberOfInflightQueuesProcessed";

        public const string NumberOfInternalEventQueuesProcessed = "NumberOfEventQueuesProcessed";

        #endregion

        #region PublishManager

        public const string PublishQueueSize = "PublishQueueSize";

        public const string NumberOfMessagesPublished = "NumberOfMessagesPublished";

        #endregion

        #region ClientConnectionManager

        public const string NumberOfClientConnectionsGotten = "NumberOfClientConnectionsGotten";

        public const string NumberOfClientConnectionsReturned = "NumberOfClientConnectionsReturned";

        public const string NumberOfClientConnectionsAvailable = "NumberOfClientConnectionsAvailable";

        #endregion
    }
}