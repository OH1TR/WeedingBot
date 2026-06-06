using Azure.Storage.Blobs;
using BotControl.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotControl.Services;

public class ImageUploadService : BackgroundService
{
    private readonly UploadSettings _uploadSettings;
    private readonly CameraSettings _cameraSettings;
    private readonly ILogger<ImageUploadService> _logger;
    private BlobContainerClient? _containerClient;

    public ImageUploadService(
        IOptions<UploadSettings> uploadSettings,
        IOptions<CameraSettings> cameraSettings,
        ILogger<ImageUploadService> logger)
    {
        _uploadSettings = uploadSettings.Value;
        _cameraSettings = cameraSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_uploadSettings.ConnectionString))
        {
            _logger.LogWarning("Azure Blob connection string not configured, upload service disabled");
            return;
        }

        try
        {
            var uri = new Uri(_uploadSettings.ConnectionString);
            _containerClient = new BlobContainerClient(uri);
            _logger.LogInformation("Upload service started, target: {Uri}", uri.GetLeftPart(UriPartial.Path));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob client");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UploadPendingImagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during upload cycle");
            }

            await Task.Delay(_uploadSettings.CheckIntervalMs, stoppingToken);
        }
    }

    private async Task UploadPendingImagesAsync(CancellationToken cancellationToken)
    {
        var imageFolder = _cameraSettings.ImageFolder;
        if (!Directory.Exists(imageFolder))
            return;

        var files = Directory.GetFiles(imageFolder, "*.jpg");
        if (files.Length == 0)
            return;

        _logger.LogDebug("Found {Count} images to upload", files.Length);

        foreach (var filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var fileName = Path.GetFileName(filePath);

            try
            {
                var blobClient = _containerClient!.GetBlobClient(fileName);
                await using var stream = File.OpenRead(filePath);
                await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

                stream.Close();
                File.Delete(filePath);
                _logger.LogInformation("Uploaded and deleted: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload {FileName}", fileName);
            }
        }
    }
}
