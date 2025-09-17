using AttechServer.Applications.UserModules.Abstracts;
using System.Diagnostics;

namespace AttechServer.Services
{
    public class SystemMonitoringBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemMonitoringBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5); // Record metrics every 5 minutes

        public SystemMonitoringBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SystemMonitoringBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Monitoring Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var monitoringService = scope.ServiceProvider.GetRequiredService<ISystemMonitoringService>();

                    await RecordSystemMetrics(monitoringService);
                    
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while recording system metrics");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait 1 minute before retry
                }
            }

            _logger.LogInformation("System Monitoring Background Service stopped");
        }

        private async Task RecordSystemMetrics(ISystemMonitoringService monitoringService)
        {
            try
            {
                // CPU Usage
                var process = Process.GetCurrentProcess();
                var cpuTime = process.TotalProcessorTime;
                await Task.Delay(1000); // Wait 1 second
                var cpuTime2 = process.TotalProcessorTime;
                var cpuUsage = (cpuTime2 - cpuTime).TotalMilliseconds / 10.0; // Approximate CPU usage
                await monitoringService.RecordPerformanceMetricAsync("cpu_usage", cpuUsage, "%");

                // Memory Usage
                var workingSet = process.WorkingSet64;
                var memoryUsageGB = Math.Round((double)workingSet / (1024 * 1024 * 1024), 2);
                await monitoringService.RecordPerformanceMetricAsync("memory_usage", memoryUsageGB, "GB");

                // GC Memory
                var gcMemory = GC.GetTotalMemory(false);
                var gcMemoryMB = Math.Round((double)gcMemory / (1024 * 1024), 2);
                await monitoringService.RecordPerformanceMetricAsync("gc_memory", gcMemoryMB, "MB");

                // Storage Usage (Uploads folder)
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (Directory.Exists(uploadsPath))
                {
                    var dirInfo = new DirectoryInfo(uploadsPath);
                    var totalSize = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                    var storageMB = Math.Round((double)totalSize / (1024 * 1024), 2);
                    await monitoringService.RecordStorageMetricAsync("uploads_storage", storageMB, "MB");
                }

                // Disk Usage
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                foreach (var drive in drives)
                {
                    var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
                    var usagePercentage = Math.Round((double)usedSpace / drive.TotalSize * 100, 2);
                    await monitoringService.RecordStorageMetricAsync($"disk_usage_{drive.Name.Replace("\\", "")}", usagePercentage, "%");
                }

                // Thread Count
                var threadCount = process.Threads.Count;
                await monitoringService.RecordPerformanceMetricAsync("thread_count", threadCount, "count");

                // Handle Count
                var handleCount = process.HandleCount;
                await monitoringService.RecordPerformanceMetricAsync("handle_count", handleCount, "count");

                _logger.LogDebug("System metrics recorded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record system metrics");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Monitoring Background Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
