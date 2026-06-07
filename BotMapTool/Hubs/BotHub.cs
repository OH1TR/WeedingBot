using Microsoft.AspNetCore.SignalR;

namespace BotMapTool.Hubs;

/// <summary>
/// SignalR hub for pushing bot updates (location etc.) to browser clients.
/// Messages are sent server-side from <see cref="Services.MqttListenerService"/>.
/// </summary>
public class BotHub : Hub
{
}
