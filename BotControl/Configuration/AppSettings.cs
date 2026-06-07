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

public class GpsSettings
{
    public string SerialPort { get; set; } = "/dev/ttyACM0";
    public int BaudRate { get; set; } = 9600;
}

public class MqttSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ClientId { get; set; } = "botcontrol";
    public string BotLocationTopic { get; set; } = "botlocation";
    public int PublishIntervalMs { get; set; } = 1000;
}

public class LoggingSettings
{
    public string LogFilePath { get; set; } = "logs/botcontrol.log";
}
