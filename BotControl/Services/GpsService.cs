using System.Globalization;
using System.IO.Ports;
using BotControl.Configuration;
using BotControl.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotControl.Services;

public class GpsService : BackgroundService, IDisposable
{
    private readonly GpsSettings _settings;
    private readonly ILogger<GpsService> _logger;
    private SerialPort? _serialPort;
    private GpsPosition? _lastPosition;
    private readonly object _lock = new();
    private DateTime _lastLogTime = DateTime.MinValue;

    public GpsService(IOptions<GpsSettings> settings, ILogger<GpsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public GpsPosition? CurrentPosition
    {
        get { lock (_lock) return _lastPosition; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GPS service starting on {Port} at {Baud} baud",
            _settings.SerialPort, _settings.BaudRate);

        try
        {
            _serialPort = new SerialPort(_settings.SerialPort, _settings.BaudRate)
            {
                ReadTimeout = 2000,
                NewLine = "\r\n"
            };
            _serialPort.Open();
            _logger.LogInformation("GPS serial port opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open GPS serial port {Port}", _settings.SerialPort);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var line = _serialPort.ReadLine();
                    ParseNmea(line);
                }
                else
                {
                    await Task.Delay(50, stoppingToken);
                }

                LogPeriodically();
            }
            catch (TimeoutException)
            {
                // No data available, continue
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("GPS read error: {Error}", ex.Message);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private void LogPeriodically()
    {
        if ((DateTime.UtcNow - _lastLogTime).TotalSeconds < 30)
            return;

        _lastLogTime = DateTime.UtcNow;

        lock (_lock)
        {
            if (_lastPosition is { HasFix: true })
            {
                _logger.LogInformation(
                    "GPS fix: {Lat:F6}, {Lon:F6} alt={Alt:F1}m sat={Sat} quality={Q}",
                    _lastPosition.Latitude,
                    _lastPosition.Longitude,
                    _lastPosition.Altitude ?? 0,
                    _lastPosition.Satellites,
                    _lastPosition.FixQuality);
            }
            else
            {
                _logger.LogWarning("GPS: no fix");
            }
        }
    }

    private void ParseNmea(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence) || sentence[0] != '$')
            return;

        // Verify checksum
        var asterisk = sentence.IndexOf('*');
        if (asterisk > 0)
        {
            var payload = sentence.Substring(1, asterisk - 1);
            byte checksum = 0;
            foreach (var c in payload)
                checksum ^= (byte)c;

            if (byte.TryParse(sentence.Substring(asterisk + 1, 2),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var expected)
                && checksum != expected)
                return;
        }

        var body = asterisk > 0 ? sentence[..asterisk] : sentence;
        var fields = body.Split(',');

        if (fields.Length < 1)
            return;

        var msgType = fields[0];

        // $GPGGA or $GNGGA - position fix
        if (msgType.EndsWith("GGA") && fields.Length >= 15)
            ParseGga(fields);

        // $GPRMC or $GNRMC - speed and heading
        if (msgType.EndsWith("RMC") && fields.Length >= 12)
            ParseRmc(fields);
    }

    private void ParseGga(string[] fields)
    {
        int.TryParse(fields[6], out var fixQuality);
        int.TryParse(fields[7], out var satellites);

        var pos = new GpsPosition
        {
            FixQuality = fixQuality,
            Satellites = satellites,
            GpsTime = ParseNmeaTime(fields[1])
        };

        if (fixQuality > 0)
        {
            pos.Latitude = ParseNmeaCoord(fields[2], fields[3]);
            pos.Longitude = ParseNmeaCoord(fields[4], fields[5]);

            if (double.TryParse(fields[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var alt))
                pos.Altitude = alt;
        }

        // Preserve speed/heading from previous RMC
        lock (_lock)
        {
            if (_lastPosition != null)
            {
                pos.Speed = _lastPosition.Speed;
                pos.Heading = _lastPosition.Heading;
            }
            _lastPosition = pos;
        }
    }

    private void ParseRmc(string[] fields)
    {
        lock (_lock)
        {
            if (_lastPosition == null)
                return;

            // Speed in knots -> m/s
            if (double.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var knots))
                _lastPosition.Speed = knots * 0.514444;

            // Heading
            if (double.TryParse(fields[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var heading))
                _lastPosition.Heading = heading;
        }
    }

    private static double ParseNmeaCoord(string value, string direction)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(direction))
            return 0;

        // NMEA format: ddmm.mmmm (lat) or dddmm.mmmm (lon)
        var dotIndex = value.IndexOf('.');
        if (dotIndex < 2)
            return 0;

        var degreeDigits = dotIndex - 2;
        if (!double.TryParse(value[..degreeDigits], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
            return 0;
        if (!double.TryParse(value[degreeDigits..], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
            return 0;

        var result = degrees + minutes / 60.0;

        if (direction == "S" || direction == "W")
            result = -result;

        return result;
    }

    private static DateTimeOffset ParseNmeaTime(string timeStr)
    {
        if (timeStr.Length < 6)
            return DateTimeOffset.MinValue;

        int.TryParse(timeStr[..2], out var hours);
        int.TryParse(timeStr[2..4], out var minutes);
        int.TryParse(timeStr[4..6], out var seconds);

        var now = DateTime.UtcNow.Date;
        return new DateTimeOffset(now.Year, now.Month, now.Day, hours, minutes, seconds, TimeSpan.Zero);
    }

    public override void Dispose()
    {
        if (_serialPort?.IsOpen == true)
        {
            try { _serialPort.Close(); }
            catch { }
        }
        _serialPort?.Dispose();
        base.Dispose();
    }
}
