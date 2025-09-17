namespace AttechServer.Applications.UserModules.Dtos.Dashboard
{
    public class SystemStatisticsDto
    {
        public double ServerUptimeHours { get; set; }
        public long StorageUsedMB { get; set; }
        public double MemoryUsageGB { get; set; }
        public SystemEntitiesDto TotalEntities { get; set; } = new();
        public List<SystemMetricDto> RecentMetrics { get; set; } = new();
        public SystemHealthDto Health { get; set; } = new();
    }

    public class SystemEntitiesDto
    {
        public int Users { get; set; }
        public int Posts { get; set; }
        public int Products { get; set; }
        public int Services { get; set; }
        public int Files { get; set; }
        public int ActivityLogs { get; set; }
        // public int SystemMonitorings { get; set; } // REMOVED
    }

    public class SystemMetricDto
    {
        public string Category { get; set; } = string.Empty;
        public double LatestValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int AlertCount { get; set; }
        public DateTime LastRecorded { get; set; }
    }

    public class SystemHealthDto
    {
        public string Status { get; set; } = string.Empty; // "healthy", "warning", "critical"
        public List<string> Issues { get; set; } = new();
        public double OverallScore { get; set; }
    }
}