namespace BotControl.Models;

public class GpsPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public int Satellites { get; set; }
    public int FixQuality { get; set; }
    public DateTimeOffset GpsTime { get; set; }
    public bool HasFix => FixQuality > 0;
}
