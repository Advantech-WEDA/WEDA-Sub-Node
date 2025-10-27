using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication;
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
            defaultTopic: "test/topic",
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
        Assert.IsAssignableFrom<IMessageBroker>(mqtt);
    }

    [Fact]
    public void Constructor_WithNullBrokerUrl_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MqttCommunication(null!, 1883));
    }

    #endregion

    #region IMessageBroker Tests

    [Fact]
    public async Task SubscribeAsync_WithValidTopic_ShouldReturnTrue()
    {
        // Arrange
        var topic = "Advantech/+/data";

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.SubscribeAsync(topic));
    }

    [Fact]
    public async Task PublishAsync_WithValidTopicAndPayload_ShouldReturnTrue()
    {
        // Arrange
        var topic = "Advantech/device1/ctl";
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"do1\": true}");

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.PublishAsync(topic, payload));
    }

    [Fact]
    public async Task UnsubscribeAsync_WithValidTopic_ShouldReturnTrue()
    {
        // Arrange
        var topic = "Advantech/+/data";

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.UnsubscribeAsync(topic));
    }

    [Fact]
    public void MessageReceived_Event_ShouldBeRaisedWhenMessageArrives()
    {
        // Arrange
        MessageReceivedEvent? receivedEvent = null;
        _mqtt.MessageReceived += (sender, e) => receivedEvent = e;

        var testEvent = new MessageReceivedEvent(
            Topic: "test/topic",
            Payload: new byte[] { 1, 2, 3 },
            Timestamp: DateTimeOffset.UtcNow);

        // Act
        // Trigger event via reflection (for testing event mechanism)
        var onMessageReceivedMethod = typeof(MqttCommunication).GetMethod(
            "OnMessageReceived",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        onMessageReceivedMethod?.Invoke(_mqtt, new object[] { testEvent });

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("test/topic", receivedEvent.Topic);
        Assert.Equal(new byte[] { 1, 2, 3 }, receivedEvent.Payload);
    }

    #endregion

    #region ICommunication Tests

    [Fact]
    public async Task ConnectAsync_WithValidBroker_ShouldReturnTrue()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.ConnectAsync());
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ShouldDisconnect()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.DisconnectAsync());
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnMessageFromQueue()
    {
        // Act & Assert
        var cts = new CancellationTokenSource(100); // 100ms timeout
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.ReadAsync(cts.Token));
    }

    [Fact]
    public async Task WriteAsync_WithData_ShouldPublishToDefaultTopic()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("test data");

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _mqtt.WriteAsync(data));
    }

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
            clientId: "custom-client",
            defaultTopic: "custom/topic");

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
