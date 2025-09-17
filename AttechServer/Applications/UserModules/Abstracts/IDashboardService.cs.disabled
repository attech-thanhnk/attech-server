using AttechServer.Applications.UserModules.Dtos.Dashboard;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IDashboardService
    {
        Task<DashboardOverviewDto> GetOverviewAsync();
        Task<UserStatisticsDto> GetUserStatisticsAsync();
        Task<ContentStatisticsDto> GetContentStatisticsAsync();
        Task<ContactStatisticsDto> GetContactStatisticsAsync();
        Task<SystemStatisticsDto> GetSystemStatisticsAsync();
        Task<List<ActivityDto>> GetRecentActivitiesAsync(int limit = 10);
        Task<RealtimeDataDto> GetRealtimeDataAsync();
        Task<ChartDataDto> GetChartDataAsync(string chartType);
        Task<UserGrowthChartDto> GetUserGrowthChartAsync(int days = 7);
        Task<ContentDistributionChartDto> GetContentDistributionChartAsync();
        Task<ContactChartDataDto> GetContactChartAsync(int days = 7);
        Task<Dictionary<string, object>> GetComprehensiveDashboardAsync();
        Task InvalidateCacheAsync(string? key = null);
    }
}