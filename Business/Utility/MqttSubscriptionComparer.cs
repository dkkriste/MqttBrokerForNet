namespace MqttBrokerForNet.Business.Utility
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using MqttBrokerForNet.Domain.Entities;

    /// <summary>
    /// MQTT subscription comparer
    /// </summary>
    public class MqttSubscriptionComparer : IEqualityComparer<MqttSubscription>
    {
        #region Constructors and Destructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">MQTT subscription comparer type</param>
        public MqttSubscriptionComparer(MqttSubscriptionComparerType type)
        {
            Type = type;
        }

        #endregion

        #region Enums

        /// <summary>
        /// MQTT subscription comparer type
        /// </summary>
        public enum MqttSubscriptionComparerType
        {
            OnClientId,

            OnTopic
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// MQTT subscription comparer type
        /// </summary>
        public MqttSubscriptionComparerType Type { get; set; }

        #endregion

        #region Public Methods and Operators

        public bool Equals(MqttSubscription x, MqttSubscription y)
        {
            if (Type == MqttSubscriptionComparerType.OnClientId)
            {
                return x.ClientId.Equals(y.ClientId);
            }
            else if (Type == MqttSubscriptionComparerType.OnTopic)
            {
                return new Regex(x.Topic).IsMatch(y.Topic);
            }
            else
            {
                return false;
            }
        }

        public int GetHashCode(MqttSubscription obj)
        {
            return obj.ClientId.GetHashCode();
        }

        #endregion
    }
}