using System.Text.Json;
using BotControl.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace BotControl.Services;

/// <summary>
/// Publishes the current GPS position (WGS84) to the MQTT bot location topic
/// at a configured interval.
/// </summary>
public class MqttService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MqttSettings _settings;
    private readonly GpsService _gpsService;
    private readonly ILogger<MqttService> _logger;
    private IMqttClient? _client;

    public MqttService(IOptions<MqttSettings> settings, GpsService gpsService, ILogger<MqttService> logger)
    {
        _settings = settings.Value;
        _gpsService = gpsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogWarning("MQTT host not configured, location publishing disabled");
            return;
        }

        _client = new MqttClientFactory().CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Host, _settings.Port)
            .WithClientId(_settings.ClientId)
            .WithCleanSession();

        if (_settings.UseTls)
            optionsBuilder.WithTlsOptions(o => o.UseTls());

        if (!string.IsNullOrEmpty(_settings.Username))
            optionsBuilder.WithCredentials(_settings.Username, _settings.Password);

        var options = optionsBuilder.Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(options, stoppingToken);
                    _logger.LogInformation("MQTT connected to {Host}:{Port}", _settings.Host, _settings.Port);
                }

                await PublishPositionAsync(stoppingToken);
                await Task.Delay(_settings.PublishIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("MQTT error: {Error}, retrying in 5 s", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (_client.IsConnected)
        {
            try { await _client.DisconnectAsync(); }
            catch { }
        }
    }

    private async Task PublishPositionAsync(CancellationToken ct)
    {
        var position = _gpsService.CurrentPosition;
        if (position is not { HasFix: true })
            return;

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_settings.BotLocationTopic)
            .WithPayload(JsonSerializer.Serialize(position, JsonOptions))
            .Build();

        await _client!.PublishAsync(message, ct);
    }

    public override void Dispose()
    {
        _client?.Dispose();
        base.Dispose();
    }
}
