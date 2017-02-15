namespace MqttBrokerForNet.Domain.Events
{
    using MqttBrokerForNet.Domain.Entities;

    /// <summary>
    /// Delegate event handler for MQTT client connected event
    /// </summary>
    /// <param name="sender">The object which raises event</param>
    /// <param name="e">Event args</param>
    public delegate void MqttClientConnectedEventHandler(object sender, MqttClientConnectedEventArgs e);
}