using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AttechServer.Applications.UserModules.Implements
{
    public class SystemMonitoringService : ISystemMonitoringService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SystemMonitoringService> _logger;

        public SystemMonitoringService(ApplicationDbContext context, ILogger<SystemMonitoringService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RecordMetricAsync(string metricName, double value, string unit, string category, string? description = null, double? thresholdValue = null)
        {
            try
            {
                var isAlert = thresholdValue.HasValue && value > thresholdValue.Value;

                var monitoring = new SystemMonitoring
                {
                    MetricName = metricName,
                    Value = value,
                    Unit = unit,
                    Category = category,
                    Description = description,
                    IsAlert = isAlert,
                    ThresholdValue = thresholdValue,
                    RecordedAt = DateTime.Now
                };

                _context.SystemMonitorings.Add(monitoring);
                await _context.SaveChangesAsync();

                if (isAlert)
                {
                    _logger.LogWarning("System alert: {MetricName} = {Value} {Unit} exceeded threshold {Threshold}",
                        metricName, value, unit, thresholdValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record metric: {MetricName}", metricName);
            }
        }

        public async Task RecordPerformanceMetricAsync(string metricName, double value, string unit)
        {
            var thresholds = new Dictionary<string, double>
            {
                ["cpu_usage"] = 80.0,
                ["memory_usage"] = 85.0,
                ["response_time"] = 5000.0
            };

            double? threshold = thresholds.ContainsKey(metricName.ToLower()) ? thresholds[metricName.ToLower()] : null;
            await RecordMetricAsync(metricName, value, unit, "Performance", null, threshold);
        }

        public async Task RecordStorageMetricAsync(string metricName, double value, string unit)
        {
            double? threshold = metricName.ToLower().Contains("usage") ? 90.0 : null;
            await RecordMetricAsync(metricName, value, unit, "Storage", null, threshold);
        }

        public async Task RecordNetworkMetricAsync(string metricName, double value, string unit)
        {
            await RecordMetricAsync(metricName, value, unit, "Network");
        }

        public async Task<List<SystemMonitoring>> GetMetricsByCategory(string category, int hours = 24)
        {
            var cutoffTime = DateTime.Now.AddHours(-hours);
            return await _context.SystemMonitorings
                .Where(s => !s.Deleted && s.Category == category && s.RecordedAt >= cutoffTime)
                .OrderByDescending(s => s.RecordedAt)
                .ToListAsync();
        }

        public async Task<List<SystemMonitoring>> GetRecentMetrics(int limit = 50)
        {
            return await _context.SystemMonitorings
                .Where(s => !s.Deleted)
                .OrderByDescending(s => s.RecordedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Dictionary<string, object>> GetSystemHealthSummary()
        {
            var last24Hours = DateTime.Now.AddHours(-24);
            
            var summary = new Dictionary<string, object>();

            // Get latest metrics by category
            var categories = new[] { "Performance", "Storage", "Network" };
            
            foreach (var category in categories)
            {
                var latestMetrics = await _context.SystemMonitorings
                    .Where(s => !s.Deleted && s.Category == category && s.RecordedAt >= last24Hours)
                    .GroupBy(s => s.MetricName)
                    .Select(g => new
                    {
                        MetricName = g.Key,
                        LatestValue = g.OrderByDescending(x => x.RecordedAt).First().Value,
                        Unit = g.OrderByDescending(x => x.RecordedAt).First().Unit,
                        IsAlert = g.OrderByDescending(x => x.RecordedAt).First().IsAlert,
                        LastRecorded = g.OrderByDescending(x => x.RecordedAt).First().RecordedAt
                    })
                    .ToListAsync();

                summary[category.ToLower()] = latestMetrics;
            }

            // Overall health status
            var alertCount = await _context.SystemMonitorings
                .CountAsync(s => !s.Deleted && s.IsAlert && s.RecordedAt >= last24Hours);

            summary["healthStatus"] = alertCount == 0 ? "Good" : alertCount < 5 ? "Warning" : "Critical";
            summary["alertCount"] = alertCount;
            summary["lastUpdated"] = DateTime.Now;

            return summary;
        }

        public async Task CleanupOldMetrics(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var oldMetrics = await _context.SystemMonitorings
                    .Where(s => s.RecordedAt < cutoffDate)
                    .ToListAsync();

                _context.SystemMonitorings.RemoveRange(oldMetrics);
                var deletedCount = await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old system monitoring records", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old monitoring metrics");
            }
        }
    }
}
