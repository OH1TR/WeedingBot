using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using BotControl.Configuration;
using BotControl.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotControl.Services;

public class CameraService : IHostedService, IDisposable
{
    private readonly CameraSettings _settings;
    private readonly ILogger<CameraService> _logger;
    private readonly GpsService _gpsService;
    private Process? _pythonProcess;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;

    private readonly SemaphoreSlim _responseLock = new(0, 1);
    private string? _lastResponse;

    public event EventHandler<CaptureInfo>? ImageCaptured;

    public CameraService(IOptions<CameraSettings> settings, ILogger<CameraService> logger, GpsService gpsService)
    {
        _settings = settings.Value;
        _logger = logger;
        _gpsService = gpsService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting camera service on port {Port}", _settings.TcpPort);

        KillExistingCameraProcesses();

        Directory.CreateDirectory(_settings.ImageFolder);

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "camera_capture.py");
        if (!File.Exists(scriptPath))
        {
            _logger.LogError("Camera script not found at {Path}", scriptPath);
            return;
        }

        _pythonProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _settings.PythonPath,
                Arguments = string.Join(" ",
                    $"\"{scriptPath}\"",
                    _settings.TcpPort,
                    $"\"{_settings.ImageFolder}\"",
                    _settings.CameraIndex,
                    _settings.FrameWidth,
                    _settings.FrameHeight),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _pythonProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) _logger.LogDebug("[Camera] {Line}", e.Data);
        };
        _pythonProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) _logger.LogWarning("[Camera] {Line}", e.Data);
        };

        _pythonProcess.Start();
        _pythonProcess.BeginOutputReadLine();
        _pythonProcess.BeginErrorReadLine();
        WritePidFile();
        _logger.LogInformation("Python camera process started (PID: {Pid})", _pythonProcess.Id);

        await ConnectAsync(cancellationToken);

        if (_stream != null)
        {
            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token));
            await SendCommandAsync($"INTERVAL {_settings.CaptureIntervalMs}");
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync("127.0.0.1", _settings.TcpPort, cancellationToken);
                _stream = _tcpClient.GetStream();
                _logger.LogInformation("Connected to camera process");
                return;
            }
            catch
            {
                _tcpClient?.Dispose();
                _tcpClient = null;
                await Task.Delay(500, cancellationToken);
            }
        }
        _logger.LogError("Failed to connect to camera process after retries");
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var pending = "";

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                var read = await _stream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;

                pending += Encoding.UTF8.GetString(buffer, 0, read);

                while (pending.Contains('\n'))
                {
                    var newlineIdx = pending.IndexOf('\n');
                    var line = pending[..newlineIdx].Trim();
                    pending = pending[(newlineIdx + 1)..];

                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("CAPTURED|"))
                    {
                        HandleCaptureNotification(line);
                    }
                    else
                    {
                        _lastResponse = line;
                        _responseLock.Release();
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug("Reader loop ended: {Error}", ex.Message);
        }
    }

    private void HandleCaptureNotification(string line)
    {
        // Format: CAPTURED|filename|sequence|uptime_seconds
        var parts = line.Split('|');
        if (parts.Length < 4) return;

        var fileName = parts[1];
        var filePath = Path.Combine(_settings.ImageFolder, fileName);

        int.TryParse(parts[2], out var sequence);

        double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var uptimeSeconds);

        var gps = _gpsService.CurrentPosition;

        var info = new CaptureInfo
        {
            FileName = fileName,
            FilePath = filePath,
            Sequence = sequence,
            UptimeSeconds = uptimeSeconds,
            WallClockUtc = gps is { HasFix: true } ? gps.GpsTime : DateTimeOffset.UtcNow,
            IsTimeSynced = gps is { HasFix: true },
            Latitude = gps?.Latitude,
            Longitude = gps?.Longitude,
            Altitude = gps?.Altitude,
            Heading = gps?.Heading,
            Speed = gps?.Speed
        };

        if (info.IsTimeSynced)
            _logger.LogInformation(
                "Image captured: {FileName} seq={Sequence} GPS={Lat:F6},{Lon:F6}",
                fileName, sequence, info.Latitude, info.Longitude);
        else
            _logger.LogInformation(
                "Image captured: {FileName} seq={Sequence} (no GPS fix)",
                fileName, sequence);
        ImageCaptured?.Invoke(this, info);
    }

    public async Task SendCommandAsync(string command)
    {
        if (_stream == null || !_stream.CanWrite)
        {
            _logger.LogWarning("Cannot send camera command, stream not available");
            return;
        }

        var data = Encoding.UTF8.GetBytes(command + "\n");
        await _stream.WriteAsync(data);

        // Wait for response from read loop
        if (await _responseLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            _logger.LogDebug("Camera command '{Command}' -> {Response}", command, _lastResponse);
        }
        else
        {
            _logger.LogWarning("Timeout waiting for response to '{Command}'", command);
        }
    }

    public Task StartCaptureAsync()
    {
        _logger.LogInformation("Starting image capture");
        return SendCommandAsync("START");
    }

    public Task StopCaptureAsync()
    {
        _logger.LogInformation("Stopping image capture");
        return SendCommandAsync("STOP");
    }

    public Task SetIntervalAsync(int ms)
    {
        _logger.LogInformation("Setting capture interval to {Ms}ms", ms);
        return SendCommandAsync($"INTERVAL {ms}");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping camera service");
        try
        {
            await SendCommandAsync("QUIT");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error sending QUIT: {Error}", ex.Message);
        }

        if (_readerCts != null)
        {
            await _readerCts.CancelAsync();
            if (_readerTask != null)
            {
                try { await _readerTask; } catch { }
            }
            _readerCts.Dispose();
        }

        _stream?.Dispose();
        _tcpClient?.Dispose();

        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            try
            {
                _pythonProcess.Kill();
                await _pythonProcess.WaitForExitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error killing camera process: {Error}", ex.Message);
            }
        }

        DeletePidFile();
    }

    private void KillExistingCameraProcesses()
    {
        // Kill by PID file
        var pidFile = Path.Combine(AppContext.BaseDirectory, "camera.pid");
        if (File.Exists(pidFile))
        {
            try
            {
                var pid = int.Parse(File.ReadAllText(pidFile).Trim());
                var process = Process.GetProcessById(pid);
                process.Kill();
                process.WaitForExit(3000);
                _logger.LogInformation("Killed previous camera process (PID: {Pid})", pid);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not kill process from PID file: {Error}", ex.Message);
            }
            File.Delete(pidFile);
        }

        // Also scan for any remaining camera_capture.py processes
        try
        {
            var candidates = Process.GetProcessesByName("python")
                .Concat(Process.GetProcessesByName("python3"));

            foreach (var proc in candidates)
            {
                try
                {
                    var cmdLine = proc.MainModule?.FileName ?? "";
                    // On Linux, check /proc/pid/cmdline for the script name
                    var cmdLineFile = $"/proc/{proc.Id}/cmdline";
                    if (File.Exists(cmdLineFile))
                    {
                        cmdLine = File.ReadAllText(cmdLineFile);
                    }

                    if (cmdLine.Contains("camera_capture.py"))
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                        _logger.LogInformation("Killed orphaned camera process (PID: {Pid})", proc.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error inspecting process {Pid}: {Error}", proc.Id, ex.Message);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error scanning for camera processes: {Error}", ex.Message);
        }
    }

    private void WritePidFile()
    {
        if (_pythonProcess == null) return;
        var pidFile = Path.Combine(AppContext.BaseDirectory, "camera.pid");
        File.WriteAllText(pidFile, _pythonProcess.Id.ToString());
    }

    private void DeletePidFile()
    {
        var pidFile = Path.Combine(AppContext.BaseDirectory, "camera.pid");
        try { File.Delete(pidFile); } catch { }
    }

    public void Dispose()
    {
        _readerCts?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _pythonProcess?.Dispose();
        _responseLock.Dispose();
        DeletePidFile();
    }
}
