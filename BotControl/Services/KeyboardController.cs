using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotControl.Services;

public class KeyboardController : BackgroundService
{
    private readonly MotorController _motor;
    private readonly CameraService _camera;
    private readonly ILogger<KeyboardController> _logger;
    private CancellationTokenSource? _repeatCts;
    private Task? _repeatTask;

    public KeyboardController(
        MotorController motor,
        CameraService camera,
        ILogger<KeyboardController> logger)
    {
        _motor = motor;
        _camera = camera;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Keyboard controller started");
        _logger.LogInformation("Controls: w=forward, s=reverse, a=left, d=right, z=zero steering");
        _logger.LogInformation("          space=stop repeating, c=start capture, x=stop capture, q=quit");

        // Yield to let other services start
        await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(20, stoppingToken);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            await HandleKeyAsync(key, stoppingToken);
        }

        await StopRepeatingAsync();
    }

    private async Task HandleKeyAsync(ConsoleKeyInfo key, CancellationToken stoppingToken)
    {
        switch (key.KeyChar)
        {
            case 'w':
                await StopRepeatingAsync();
                StartRepeating('w', stoppingToken);
                _logger.LogInformation("Forward: sending 'w' repeatedly (space to stop)");
                break;

            case 's':
                await StopRepeatingAsync();
                StartRepeating('s', stoppingToken);
                _logger.LogInformation("Reverse: sending 's' repeatedly (space to stop)");
                break;

            case 'a':
                _motor.SendImmediate('a');
                _logger.LogInformation("Steer left");
                break;

            case 'd':
                _motor.SendImmediate('d');
                _logger.LogInformation("Steer right");
                break;

            case 'z':
            case 'Z':
                await StopRepeatingAsync();
                _motor.SendBuffered("Z");
                _logger.LogInformation("Steering zeroed");
                break;

            case ' ':
                await StopRepeatingAsync();
                _motor.StopAll();
                _logger.LogInformation("Stopped");
                break;

            case 'c':
                await _camera.StartCaptureAsync();
                _logger.LogInformation("Camera capture started");
                break;

            case 'x':
                await _camera.StopCaptureAsync();
                _logger.LogInformation("Camera capture stopped");
                break;

            case 'q':
                _logger.LogInformation("Quit requested");
                await StopRepeatingAsync();
                _motor.StopAll();
                Environment.Exit(0);
                break;
        }
    }

    private void StartRepeating(char command, CancellationToken stoppingToken)
    {
        _repeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var token = _repeatCts.Token;
        var intervalMs = _motor.CommandRepeatIntervalMs;

        _repeatTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                _motor.SendImmediate(command);
                await Task.Delay(intervalMs, token);
            }
        }, token);
    }

    private async Task StopRepeatingAsync()
    {
        if (_repeatCts != null)
        {
            await _repeatCts.CancelAsync();
            try
            {
                if (_repeatTask != null)
                    await _repeatTask;
            }
            catch (OperationCanceledException)
            {
            }
            _repeatCts.Dispose();
            _repeatCts = null;
            _repeatTask = null;
        }
    }
}
