using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Xunit;

namespace Weda.SubNode.Core.Tests.Communication;

public class MqttCommunicationTests
{
    private readonly MqttCommunication _mqtt;

    public MqttCommunicationTests()
    {
        _mqtt = new MqttCommunication(
            brokerUrl: "localhost",
            port: 1883,
            clientId: "test-client",
            settings: null,
            logger: NullLogger<CommunicationBase>.Instance);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var mqtt = new MqttCommunication("localhost", 1883);

        // Assert
        Assert.NotNull(mqtt);
        Assert.IsAssignableFrom<ICommunication>(mqtt);
        Assert.IsAssignableFrom<IPubSub>(mqtt);
    }

    [Fact]
    public void Constructor_WithNullBrokerUrl_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MqttCommunication(null!, 1883));
    }

    #endregion

    #region IPubSub Tests

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var topic = "Advantech/+/data";

        // Act
        var result = await _mqtt.SubscribeAsync(topic);

        // Assert
        Assert.False(result); // Should return false when not connected
    }

    [Fact]
    public async Task PublishAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var topic = "Advantech/device1/ctl";
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"do1\": true}");

        // Act
        var result = await _mqtt.PublishAsync(topic, payload);

        // Assert
        Assert.False(result); // Should return false when not connected
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenNotConnected_ShouldReturnFalse()
    {
        // Arrange
        var topic = "Advantech/+/data";

        // Act
        var result = await _mqtt.UnsubscribeAsync(topic);

        // Assert
        Assert.False(result); // Should return false when not connected
    }

    [Fact]
    public void MessageReceived_Event_ShouldBeRaisedWhenMessageArrives()
    {
        // Arrange
        MessageReceivedEvent<byte[]>? receivedEvent = null;
        _mqtt.MessageReceived += (sender, e) => receivedEvent = e;

        var topic = "test/topic";
        var payload = new byte[] { 1, 2, 3 };

        // Act
        // Trigger event via reflection (for testing event mechanism)
        var onMessageReceivedMethod = typeof(MqttCommunication).GetMethod(
            "OnMessageReceived",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        onMessageReceivedMethod?.Invoke(_mqtt, new object[] { topic, payload, 0, false });

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("test/topic", receivedEvent.Topic);
        Assert.Equal(new byte[] { 1, 2, 3 }, receivedEvent.Payload);
    }

    #endregion

    #region ICommunication Tests
    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldNotThrow()
    {
        // MqttCommunication is now implemented
        // Disconnecting when not connected should not throw exception

        // Act & Assert (should not throw)
        await _mqtt.DisconnectAsync();

        // Assert
        Assert.False(_mqtt.IsConnected);
    }

    // Note: ReadAsync/WriteAsync removed from MqttCommunication
    // MqttCommunication now uses pure Pub/Sub pattern (Subscribe/Publish)

    #endregion

    #region State Management Tests

    [Fact]
    public void State_InitialState_ShouldBeDisconnected()
    {
        // Arrange
        var mqtt = new MqttCommunication("localhost", 1883);

        // Assert
        Assert.Equal(CommunicationState.Disconnected, mqtt.State);
        Assert.False(mqtt.IsConnected);
    }

    #endregion

    #region Factory Tests

    [Fact]
    public void MqttFactory_Default_ShouldCreateInstance()
    {
        // Act
        var mqtt = Mqtt.Default;

        // Assert
        Assert.NotNull(mqtt);
    }

    [Fact]
    public void MqttFactory_Create_ShouldCreateInstanceWithParameters()
    {
        // Act
        var mqtt = Mqtt.Create(
            brokerUrl: "172.16.8.122",
            port: 1883,
            clientId: "custom-client");

        // Assert
        Assert.NotNull(mqtt);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var mqtt = new MqttCommunication("localhost", 1883);

        // Act
        mqtt.Dispose();

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    #endregion
}
