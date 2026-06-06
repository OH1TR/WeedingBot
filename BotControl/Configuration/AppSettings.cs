namespace BotControl.Configuration;

public class CameraSettings
{
    public int CaptureIntervalMs { get; set; } = 5000;
    public string ImageFolder { get; set; } = "/tmp/botimages";
    public int TcpPort { get; set; } = 9998;
    public string PythonPath { get; set; } = "python3";
    public int CameraIndex { get; set; } = 0;
    public int FrameWidth { get; set; } = 1280;
    public int FrameHeight { get; set; } = 720;
}

public class UploadSettings
{
    public string ConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "botimages";
    public int CheckIntervalMs { get; set; } = 10000;
}

public class MotorSettings
{
    public string SerialPort { get; set; } = "/dev/ttyUSB0";
    public int BaudRate { get; set; } = 1200;
    public int CommandRepeatIntervalMs { get; set; } = 200;
}

public class LoggingSettings
{
    public string LogFilePath { get; set; } = "logs/botcontrol.log";
}
