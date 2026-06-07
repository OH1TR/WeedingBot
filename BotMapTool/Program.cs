using System.Net.Http.Headers;
using System.Text;
using BotMapTool.Configuration;
using BotMapTool.Hubs;
using BotMapTool.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.Configure<MmlSettings>(builder.Configuration.GetSection(MmlSettings.SectionName));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection(MqttSettings.SectionName));

// HttpClient for Maanmittauslaitos karttakuvapalvelu: API key as Basic auth username, empty password.
builder.Services.AddHttpClient(MmlSettings.HttpClientName, (serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MmlSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ApiKey}:"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

// Listens to the MQTT broker and pushes bot locations to browsers via SignalR.
builder.Services.AddHostedService<MqttListenerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<BotHub>("/bothub");

app.Run();
