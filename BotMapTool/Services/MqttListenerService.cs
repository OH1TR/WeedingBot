using System.Text.Json;
using BotMapTool.Configuration;
using BotMapTool.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace BotMapTool.Services;

/// <summary>
/// Connects to the MQTT broker over TCP, subscribes to the bot location topic
/// and forwards received positions to browser clients via the SignalR hub.
/// </summary>
public class MqttListenerService : BackgroundService
{
    private readonly MqttSettings _settings;
    private readonly IHubContext<BotHub> _hub;
    private readonly ILogger<MqttListenerService> _logger;
    private IMqttClient? _client;

    public MqttListenerService(
        IOptions<MqttSettings> settings,
        IHubContext<BotHub> hub,
        ILogger<MqttListenerService> logger)
    {
        _settings = settings.Value;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogWarning("MQTT host not configured, bot location updates disabled");
            return;
        }

        _client = new MqttClientFactory().CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(
                    e.ApplicationMessage.ConvertPayloadToString());
                await _hub.Clients.All.SendAsync("botLocation", json, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to forward MQTT message: {Error}", ex.Message);
            }
        };

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Host, _settings.Port)
            .WithClientId(_settings.ClientId)
            .WithCleanSession();

        if (_settings.UseTls)
            optionsBuilder.WithTlsOptions(o => o.UseTls());

        if (!string.IsNullOrEmpty(_settings.Username))
            optionsBuilder.WithCredentials(_settings.Username, _settings.Password);

        var options = optionsBuilder.Build();

        // Connect and keep the connection alive, reconnecting on failures.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(options, stoppingToken);
                    await _client.SubscribeAsync(_settings.BotLocationTopic, cancellationToken: stoppingToken);
                    _logger.LogInformation("MQTT connected to {Host}:{Port}, subscribed to {Topic}",
                        _settings.Host, _settings.Port, _settings.BotLocationTopic);
                }

                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("MQTT error: {Error}, retrying in 5 s", ex.Message);
                try { await Task.Delay(5000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        if (_client.IsConnected)
        {
            try { await _client.DisconnectAsync(); }
            catch { }
        }
    }

    public override void Dispose()
    {
        _client?.Dispose();
        base.Dispose();
    }
}
