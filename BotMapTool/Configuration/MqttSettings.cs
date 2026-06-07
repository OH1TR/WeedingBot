namespace BotMapTool.Configuration;

public class MqttSettings
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ClientId { get; set; } = "botmaptool";
    public string BotLocationTopic { get; set; } = "botlocation";
}
