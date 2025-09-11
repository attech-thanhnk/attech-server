using AttechServer.Applications.UserModules.Dtos.Menu;
using AttechServer.Domains.Entities.Main;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IMenuService
    {
        Task<List<MenuDto>> GetAllAsync();
        Task<List<MenuTreeDto>> GetMenuTreeAsync(string language = "vi");
        Task<MenuDto?> GetByIdAsync(int id);
        Task<MenuDto> CreateAsync(CreateMenuDto input);
        Task<MenuDto?> UpdateAsync(int id, UpdateMenuDto input);
        Task<bool> DeleteAsync(int id);
        Task<bool> ReorderAsync(List<int> menuIds);
        
        // Sync methods
        Task<MenuSyncResult> SyncAllCategoriesAsync();
        Task<int> SyncProductCategoriesAsync();
        Task<int> SyncNewsCategoriesAsync();
        Task<int> SyncNotificationCategoriesAsync();
        Task<int> SyncServicesAsync();
        
        // Level 3 sync methods
        Task<int> SyncProductItemsAsync();
        Task<int> SyncNewsItemsAsync();
        Task<int> SyncNotificationItemsAsync();
        
        // Frontend format
        Task<object> GetFrontendMenuAsync(string language = "vi");
    }

    public class MenuSyncResult
    {
        public int ProductCategoriesAdded { get; set; }
        public int NewsCategoriesAdded { get; set; }
        public int NotificationCategoriesAdded { get; set; }
        public int ServicesAdded { get; set; }
        public int ProductItemsAdded { get; set; }
        public int NewsItemsAdded { get; set; }
        public int NotificationItemsAdded { get; set; }
        public int TotalAdded => ProductCategoriesAdded + NewsCategoriesAdded + NotificationCategoriesAdded + ServicesAdded + ProductItemsAdded + NewsItemsAdded + NotificationItemsAdded;
    }
}