using Microsoft.Extensions.Logging;

namespace BotControl.Services;

/// <summary>
/// High-level robot driver for programmatic control using buffered
/// S (steering) and W (drive) commands. Values use the Arduino format:
/// sign + 3 digits, e.g. "+050", "-120".
/// </summary>
public class RobotDriver
{
    private readonly MotorController _motor;
    private readonly ILogger<RobotDriver> _logger;

    public RobotDriver(MotorController motor, ILogger<RobotDriver> logger)
    {
        _motor = motor;
        _logger = logger;
    }

    /// <summary>
    /// Set drive motor speed. Positive = forward, negative = reverse.
    /// Range roughly -255 to +255.
    /// </summary>
    public void SetDriveSpeed(int speed)
    {
        speed = Math.Clamp(speed, -255, 255);
        var value = FormatValue(speed);
        _motor.SendBuffered($"W{value}");
        _logger.LogInformation("Drive speed set to {Speed}", speed);
    }

    /// <summary>
    /// Set steering target position. Positive = right, negative = left.
    /// Note: Arduino negates the value internally.
    /// </summary>
    public void SetSteering(int position)
    {
        position = Math.Clamp(position, -999, 999);
        var value = FormatValue(position);
        _motor.SendBuffered($"S{value}");
        _logger.LogInformation("Steering set to {Position}", position);
    }

    /// <summary>
    /// Zero the steering encoder position.
    /// </summary>
    public void ZeroSteering()
    {
        _motor.SendBuffered("Z");
        _logger.LogInformation("Steering zeroed");
    }

    /// <summary>
    /// Stop drive motor.
    /// </summary>
    public void Stop()
    {
        SetDriveSpeed(0);
    }

    private static string FormatValue(int value)
    {
        var sign = value >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(value):D3}";
    }
}
