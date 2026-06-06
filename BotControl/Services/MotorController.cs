using System.IO.Ports;
using BotControl.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotControl.Services;

public class MotorController : IDisposable
{
    private readonly MotorSettings _settings;
    private readonly ILogger<MotorController> _logger;
    private SerialPort? _serialPort;

    public bool IsConnected => _serialPort?.IsOpen ?? false;
    public int CommandRepeatIntervalMs => _settings.CommandRepeatIntervalMs;

    public MotorController(IOptions<MotorSettings> settings, ILogger<MotorController> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public void Connect()
    {
        try
        {
            _serialPort = new SerialPort(_settings.SerialPort, _settings.BaudRate)
            {
                RtsEnable = false,
                DtrEnable = false,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.Open();
            _logger.LogInformation("Serial port {Port} opened at {Baud} baud",
                _settings.SerialPort, _settings.BaudRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {Port}", _settings.SerialPort);
        }
    }

    /// <summary>
    /// Send an empty CR to flush any partial command in the Arduino buffer,
    /// then send a single-byte immediate command (w, s, a, d).
    /// </summary>
    public void SendImmediate(char command)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            _logger.LogWarning("Serial port not open, cannot send '{Command}'", command);
            return;
        }

        try
        {
            _serialPort.Write("\r");
            _serialPort.Write(new[] { command }, 0, 1);
            _logger.LogDebug("Sent immediate: {Command}", command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending immediate command '{Command}'", command);
        }
    }

    /// <summary>
    /// Send an empty CR to flush, then send an uppercase buffered command
    /// (e.g. "W+050", "S-030", "Z") followed by CR.
    /// </summary>
    public void SendBuffered(string command)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            _logger.LogWarning("Serial port not open, cannot send '{Command}'", command);
            return;
        }

        try
        {
            _serialPort.Write("\r");
            _serialPort.Write(command + "\r");
            _logger.LogDebug("Sent buffered: {Command}", command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending buffered command '{Command}'", command);
        }
    }

    /// <summary>
    /// Stop all motors by sending a zero-speed drive command.
    /// </summary>
    public void StopAll()
    {
        SendBuffered("W+000");
        _logger.LogInformation("All motors stopped");
    }

    public void Dispose()
    {
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                StopAll();
                _serialPort.Close();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error closing serial port: {Error}", ex.Message);
            }
        }
        _serialPort?.Dispose();
    }
}
