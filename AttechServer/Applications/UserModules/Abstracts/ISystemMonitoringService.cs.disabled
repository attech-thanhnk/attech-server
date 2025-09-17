using AttechServer.Domains.Entities.Main;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface ISystemMonitoringService
    {
        Task RecordMetricAsync(string metricName, double value, string unit, string category, string? description = null, double? thresholdValue = null);
        Task RecordPerformanceMetricAsync(string metricName, double value, string unit);
        Task RecordStorageMetricAsync(string metricName, double value, string unit);
        Task RecordNetworkMetricAsync(string metricName, double value, string unit);
        Task<List<SystemMonitoring>> GetMetricsByCategory(string category, int hours = 24);
        Task<List<SystemMonitoring>> GetRecentMetrics(int limit = 50);
        Task<Dictionary<string, object>> GetSystemHealthSummary();
        Task CleanupOldMetrics(int daysToKeep = 30);
    }
}
