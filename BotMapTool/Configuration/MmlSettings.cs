namespace BotMapTool.Configuration;

public class MmlSettings
{
    public const string SectionName = "Mml";
    public const string HttpClientName = "MmlKarttakuva";

    public string BaseUrl { get; set; } = "https://avoin-karttakuva.maanmittauslaitos.fi/avoin";
    public string ApiKey { get; set; } = "";
}
