using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atamatay.Services
{
    public class FolderCleanerService(ILogger<FolderCleanerService> logger) : BackgroundService
    {
        private const string FolderPath = @"songs";
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation($"Cleaning folder at: {FolderPath}");
                    CleanFolder();
                    logger.LogInformation("Folder cleaned successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while cleaning folder");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private void CleanFolder()
        {
            if (!Directory.Exists(FolderPath))
            {
                logger.LogWarning($"Folder does not exist: {FolderPath}");
                return;
            }

            var di = new DirectoryInfo(FolderPath);

            foreach (var file in di.GetFiles())
            {
                try
                {
                    file.Delete();
                    logger.LogInformation($"Deleted file: {file.Name}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error deleting file: {file.Name}");
                }
            }

            foreach (var dir in di.GetDirectories())
            {
                try
                {
                    dir.Delete(true);
                    logger.LogInformation($"Deleted directory: {dir.Name}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error deleting directory: {dir.Name}");
                }
            }
        }
    }
}
