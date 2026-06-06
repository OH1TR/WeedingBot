using BotControl.Configuration;
using BotControl.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var logFilePath = configuration.GetValue<string>("Logging:LogFilePath") ?? "logs/botcontrol.log";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logFilePath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("BotControl starting");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.Configure<CameraSettings>(context.Configuration.GetSection("Camera"));
            services.Configure<UploadSettings>(context.Configuration.GetSection("Upload"));
            services.Configure<MotorSettings>(context.Configuration.GetSection("Motor"));

            services.AddSingleton<MotorController>();
            services.AddSingleton<RobotDriver>();
            services.AddSingleton<CameraService>();

            services.AddHostedService(sp => sp.GetRequiredService<CameraService>());
            services.AddHostedService<ImageUploadService>();
            services.AddHostedService<KeyboardController>();
        })
        .Build();

    // Connect motor controller before starting host
    var motor = host.Services.GetRequiredService<MotorController>();
    motor.Connect();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("BotControl shutting down");
    Log.CloseAndFlush();
}
