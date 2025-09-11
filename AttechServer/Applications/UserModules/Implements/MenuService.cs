using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Menu;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using Microsoft.EntityFrameworkCore;

namespace AttechServer.Applications.UserModules.Implements
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuService> _logger;

        public MenuService(ApplicationDbContext context, ILogger<MenuService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<MenuDto>> GetAllAsync()
        {
            var menus = await _context.Menus
                .Where(m => !m.Deleted)
                .OrderBy(m => m.Order)
                .ToListAsync();

            return menus.Select(MapToDto).ToList();
        }

        public async Task<List<MenuTreeDto>> GetMenuTreeAsync(string language = "vi")
        {
            var menus = await _context.Menus
                .Where(m => !m.Deleted && m.IsActive)
                .OrderBy(m => m.Order)
                .ToListAsync();

            var rootMenus = menus.Where(m => m.ParentId == null).ToList();
            
            return rootMenus.Select(menu => MapToTreeDto(menu, menus, language, 0)).ToList();
        }

        public async Task<MenuDto?> GetByIdAsync(int id)
        {
            var menu = await _context.Menus
                .Where(m => m.Id == id && !m.Deleted)
                .FirstOrDefaultAsync();

            return menu == null ? null : MapToDto(menu);
        }

        public async Task<MenuDto> CreateAsync(CreateMenuDto input)
        {
            // Auto-assign next Order value for the same ParentId
            var maxOrder = await _context.Menus
                .Where(m => m.ParentId == input.ParentId && !m.Deleted)
                .MaxAsync(m => (int?)m.Order) ?? -1;

            var menu = new Menu
            {
                Key = input.Key,
                TitleVi = input.TitleVi,
                TitleEn = input.TitleEn,
                Url = input.Url,
                UrlEn = input.UrlEn,
                ParentId = input.ParentId,
                Order = maxOrder + 1,
                IsActive = input.IsActive,
                IsExternal = input.IsExternal,
                Target = input.Target,
                DescriptionVi = input.DescriptionVi,
                DescriptionEn = input.DescriptionEn,
                MenuType = "static",
                CreatedDate = DateTime.Now
            };

            _context.Menus.Add(menu);
            await _context.SaveChangesAsync();

            return MapToDto(menu);
        }

        public async Task<MenuDto?> UpdateAsync(int id, UpdateMenuDto input)
        {
            var menu = await _context.Menus
                .Where(m => m.Id == id && !m.Deleted)
                .FirstOrDefaultAsync();

            if (menu == null) return null;

            var oldParentId = menu.ParentId;

            menu.Key = input.Key;
            menu.TitleVi = input.TitleVi;
            menu.TitleEn = input.TitleEn;
            menu.Url = input.Url;
            menu.UrlEn = input.UrlEn;
            menu.ParentId = input.ParentId;
            menu.IsActive = input.IsActive;
            menu.IsExternal = input.IsExternal;
            menu.Target = input.Target;
            menu.DescriptionVi = input.DescriptionVi;
            menu.DescriptionEn = input.DescriptionEn;
            menu.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // If ParentId changed, reorder both old and new parent's children
            if (oldParentId != input.ParentId)
            {
                await ReorderMenuChildren(oldParentId);
                await ReorderMenuChildren(input.ParentId);
            }

            return MapToDto(menu);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var menu = await _context.Menus
                .Where(m => m.Id == id && !m.Deleted)
                .FirstOrDefaultAsync();

            if (menu == null) return false;

            var parentId = menu.ParentId;
            
            menu.Deleted = true;
            menu.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            
            // Reorder siblings after deletion
            await ReorderMenuChildren(parentId);
            
            return true;
        }

        public async Task<bool> ReorderAsync(List<int> menuIds)
        {
            var menus = await _context.Menus
                .Where(m => menuIds.Contains(m.Id) && !m.Deleted)
                .ToListAsync();

            for (int i = 0; i < menuIds.Count; i++)
            {
                var menu = menus.FirstOrDefault(m => m.Id == menuIds[i]);
                if (menu != null)
                {
                    menu.Order = i;
                    menu.ModifiedDate = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MenuSyncResult> SyncAllCategoriesAsync()
        {
            // 0. Ensure static menus exist
            await EnsureStaticMenusAsync();
            
            // 1. Hard delete all synced menus (keep manual menus with SourceType = null)
            var syncedMenus = await _context.Menus
                .Where(m => m.SourceType != null)
                .ToListAsync();
            
            if (syncedMenus.Any())
            {
                _context.Menus.RemoveRange(syncedMenus);
                await _context.SaveChangesAsync();
            }
            
            // 2. Sync fresh data  
            var result = new MenuSyncResult();
            
            // Level 2: Categories
            result.ProductCategoriesAdded = await SyncProductCategoriesAsync();
            result.NewsCategoriesAdded = await SyncNewsCategoriesAsync();
            result.NotificationCategoriesAdded = await SyncNotificationCategoriesAsync();
            result.ServicesAdded = await SyncServicesAsync();
            
            // Level 3: Items (chỉ sync product items, services đã sync ở trên)
            result.ProductItemsAdded = await SyncProductItemsAsync();
            // News và Notifications chỉ sync categories, không sync items
            
            return result;
        }

        private async Task EnsureStaticMenusAsync()
        {
            // Tạo static menus nếu chưa có
            await GetOrCreateParentMenu("home", "Trang chủ", "Home", "/", 1);
            await GetOrCreateParentMenu("company-info", "Thông tin công ty", "About Us", "/thong-tin-cong-ty", 6);
            await GetOrCreateParentMenu("contact", "Liên hệ", "Contact", "/lien-he", 7);
        }

        public async Task<int> SyncProductCategoriesAsync()
        {
            // Get or create "Sản phẩm" parent menu
            var parentMenu = await GetOrCreateParentMenu("products", "Sản phẩm", "Products", "/san-pham", 2);
            
            // Get all product categories với hierarchy
            var allCategories = await _context.ProductCategories
                .Where(pc => !pc.Deleted && pc.Status == 1)
                .OrderBy(pc => pc.Order)
                .ToListAsync();

            // Sync root categories trước (ParentId = null)  
            var rootCategories = allCategories.Where(c => c.ParentId == null).ToList();
            
            int added = 0;
            foreach (var category in rootCategories)
            {
                added += await SyncCategoryWithChildren(category, allCategories, parentMenu, "/san-pham", "/en/products");
            }
            
            return added;
        }

        private async Task<int> SyncCategoryWithChildren(ProductCategory category, List<ProductCategory> allCategories, Menu parentMenu, string baseUrlVi, string baseUrlEn)
        {
            int synced = 0;

            // Build URLs
            var url = $"{baseUrlVi}/{category.SlugVi}";
            var urlEn = $"{baseUrlEn}/{category.SlugEn ?? category.SlugVi}";

            // Check if menu exists
            var existingMenu = await _context.Menus
                .FirstOrDefaultAsync(m => m.SourceType == "ProductCategory"
                                   && m.SourceId == category.Id && !m.Deleted);

            Menu categoryMenu;
            if (existingMenu == null)
            {
                // Get Order trong parent
                var siblingCount = await _context.Menus
                    .CountAsync(m => m.ParentId == parentMenu.Id && !m.Deleted);

                // Determine if có children
                var hasChildren = allCategories.Any(c => c.ParentId == category.Id);

                categoryMenu = new Menu
                {
                    Key = $"products-cat-{category.SlugVi}", // Standardized key
                    TitleVi = category.TitleVi,
                    TitleEn = category.TitleEn,
                    Url = url,
                    UrlEn = urlEn,
                    ParentId = parentMenu.Id,
                    Order = siblingCount,
                    IsActive = category.Status == 1,
                    MenuType = hasChildren ? "category_branch" : "category_leaf",
                    SourceType = "ProductCategory",
                    SourceId = category.Id,
                    SourceParentId = category.ParentId,
                    CreatedDate = DateTime.Now
                };

                _context.Menus.Add(categoryMenu);
                await _context.SaveChangesAsync();
                synced++;
            }
            else
            {
                categoryMenu = existingMenu;
            }

            // Sync children categories
            var childCategories = allCategories
                .Where(c => c.ParentId == category.Id)
                .OrderBy(c => c.Order)
                .ToList();

            foreach (var childCategory in childCategories)
            {
                synced += await SyncCategoryWithChildren(childCategory, allCategories, categoryMenu, url, urlEn);
            }

            return synced;
        }

        public async Task<int> SyncNewsCategoriesAsync()
        {
            var parentMenu = await GetOrCreateParentMenu("news", "Tin tức", "News", "/tin-tuc", 4);
            
            // Get all news categories với hierarchy
            var allCategories = await _context.NewsCategories
                .Where(nc => !nc.Deleted && nc.Status == 1)
                .OrderBy(nc => nc.Order)
                .ToListAsync();

            // Sync root categories trước (ParentId = null)
            var rootCategories = allCategories.Where(c => c.ParentId == null).ToList();
            
            int added = 0;
            foreach (var category in rootCategories)
            {
                added += await SyncNewsCategoryWithChildren(category, allCategories, parentMenu, "/tin-tuc", "/en/news");
            }
            
            return added;
        }

        private async Task<int> SyncNewsCategoryWithChildren(NewsCategory category, List<NewsCategory> allCategories, Menu parentMenu, string baseUrlVi, string baseUrlEn)
        {
            int synced = 0;

            // Build URLs
            var url = $"{baseUrlVi}/{category.SlugVi}";
            var urlEn = $"{baseUrlEn}/{category.SlugEn ?? category.SlugVi}";

            // Check if menu exists
            var existingMenu = await _context.Menus
                .FirstOrDefaultAsync(m => m.SourceType == "NewsCategory"
                                   && m.SourceId == category.Id && !m.Deleted);

            Menu categoryMenu;
            if (existingMenu == null)
            {
                // Get Order trong parent
                var siblingCount = await _context.Menus
                    .CountAsync(m => m.ParentId == parentMenu.Id && !m.Deleted);

                // Determine if có children
                var hasChildren = allCategories.Any(c => c.ParentId == category.Id);

                categoryMenu = new Menu
                {
                    Key = $"news-cat-{category.SlugVi}", // Standardized key
                    TitleVi = category.TitleVi,
                    TitleEn = category.TitleEn,
                    Url = url,
                    UrlEn = urlEn,
                    ParentId = parentMenu.Id,
                    Order = siblingCount,
                    IsActive = category.Status == 1,
                    MenuType = hasChildren ? "category_branch" : "category_leaf",
                    SourceType = "NewsCategory",
                    SourceId = category.Id,
                    SourceParentId = category.ParentId,
                    CreatedDate = DateTime.Now
                };

                _context.Menus.Add(categoryMenu);
                await _context.SaveChangesAsync();
                synced++;
            }
            else
            {
                categoryMenu = existingMenu;
            }

            // Sync children categories
            var childCategories = allCategories
                .Where(c => c.ParentId == category.Id)
                .OrderBy(c => c.Order)
                .ToList();

            foreach (var childCategory in childCategories)
            {
                synced += await SyncNewsCategoryWithChildren(childCategory, allCategories, categoryMenu, url, urlEn);
            }

            return synced;
        }

        public async Task<int> SyncNotificationCategoriesAsync()
        {
            var parentMenu = await GetOrCreateParentMenu("notifications", "Thông báo", "Notifications", "/thong-bao", 5);
            
            var categories = await _context.NotificationCategories
                .Where(nc => !nc.Deleted && nc.Status == 1)
                .ToListAsync();

            int added = 0;
            foreach (var category in categories)
            {
                var existingMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "NotificationCategory" 
                                         && m.SourceId == category.Id 
                                         && !m.Deleted);
                
                if (existingMenu == null)
                {
                    // Get next sequential Order for this parent
                    var existingChildrenCount = await _context.Menus
                        .CountAsync(m => m.ParentId == parentMenu.Id && !m.Deleted);

                    var menu = new Menu
                    {
                        Key = $"notifications-cat-{category.SlugVi}",
                        TitleVi = category.TitleVi,
                        TitleEn = category.TitleEn,
                        Url = $"/thong-bao/{category.SlugVi}",
                        UrlEn = $"/en/notifications/{(!string.IsNullOrEmpty(category.SlugEn) ? category.SlugEn : category.SlugVi)}",
                        ParentId = parentMenu.Id,
                        Order = existingChildrenCount,
                        IsActive = true,
                        MenuType = "category_leaf",
                        SourceType = "NotificationCategory",
                        SourceId = category.Id,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Menus.Add(menu);
                    added++;
                }
            }
            
            if (added > 0)
            {
                await _context.SaveChangesAsync();
            }
            
            return added;
        }

        public async Task<int> SyncServicesAsync()
        {
            var parentMenu = await GetOrCreateParentMenu("services", "Dịch vụ", "Services", "/dich-vu", 3);
            
            var services = await _context.Services
                .Where(s => !s.Deleted && s.Status == 1)
                .ToListAsync();

            int added = 0;
            foreach (var service in services)
            {
                var existingMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "Service" 
                                         && m.SourceId == service.Id 
                                         && !m.Deleted);
                
                if (existingMenu == null)
                {
                    // Get next sequential Order for this parent
                    var existingChildrenCount = await _context.Menus
                        .CountAsync(m => m.ParentId == parentMenu.Id && !m.Deleted);

                    var menu = new Menu
                    {
                        Key = $"services-{service.SlugVi}",
                        TitleVi = service.TitleVi,
                        TitleEn = service.TitleEn,
                        Url = $"/dich-vu/{service.SlugVi}",
                        UrlEn = $"/en/services/{(!string.IsNullOrEmpty(service.SlugEn) ? service.SlugEn : service.SlugVi)}",
                        ParentId = parentMenu.Id,
                        Order = existingChildrenCount,
                        IsActive = true,
                        MenuType = "item",
                        SourceType = "Service",
                        SourceId = service.Id,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Menus.Add(menu);
                    added++;
                }
            }
            
            if (added > 0)
            {
                await _context.SaveChangesAsync();
            }
            
            return added;
        }

        private async Task ReorderMenuChildren(int? parentId)
        {
            var children = await _context.Menus
                .Where(m => m.ParentId == parentId && !m.Deleted)
                .OrderBy(m => m.Order)
                .ToListAsync();

            for (int i = 0; i < children.Count; i++)
            {
                children[i].Order = i;
                children[i].ModifiedDate = DateTime.Now;
            }
            
            await _context.SaveChangesAsync();
        }

        private async Task<Menu> GetOrCreateParentMenu(string key, string titleVi, string titleEn, string url, int order)
        {
            var parentMenu = await _context.Menus
                .FirstOrDefaultAsync(m => m.Key == key && m.ParentId == null && !m.Deleted);
            
            if (parentMenu == null)
            {
                // Generate proper UrlEn
                var urlEn = url.Replace("/san-pham", "/en/products")
                              .Replace("/tin-tuc", "/en/news")
                              .Replace("/thong-bao", "/en/notifications")
                              .Replace("/dich-vu", "/en/services")
                              .Replace("/thong-tin-cong-ty", "/en/about-us")
                              .Replace("/lien-he", "/en/contact");
                
                // Handle root path
                if (url == "/")
                {
                    urlEn = "/en/";
                }

                parentMenu = new Menu
                {
                    Key = key,
                    TitleVi = titleVi,
                    TitleEn = titleEn,
                    Url = url,
                    UrlEn = urlEn,
                    Order = order,
                    IsActive = true,
                    MenuType = "category_root",
                    SourceType = null, // Root menus không có source
                    CreatedDate = DateTime.Now
                };
                
                _context.Menus.Add(parentMenu);
                await _context.SaveChangesAsync();
            }
            
            return parentMenu;
        }

        public async Task<int> SyncProductItemsAsync()
        {
            var products = await _context.Products
                .Where(p => !p.Deleted && p.Status == 1)
                .ToListAsync();

            int added = 0;
            foreach (var product in products)
            {
                // Find parent category menu and category data
                var categoryMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "ProductCategory" 
                                         && m.SourceId == product.ProductCategoryId 
                                         && !m.Deleted);
                
                if (categoryMenu == null) continue;

                // Get category for slug info
                var category = await _context.ProductCategories
                    .FirstOrDefaultAsync(c => c.Id == product.ProductCategoryId);
                
                if (category == null) continue;

                // Check if product menu already exists
                var existingMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "Product" 
                                         && m.SourceId == product.Id 
                                         && !m.Deleted);
                
                if (existingMenu == null)
                {
                    // Get next sequential Order for this parent
                    var existingChildrenCount = await _context.Menus
                        .CountAsync(m => m.ParentId == categoryMenu.Id && !m.Deleted);

                    var menu = new Menu
                    {
                        Key = $"products-{product.SlugVi}",
                        TitleVi = product.TitleVi,
                        TitleEn = product.TitleEn,
                        Url = $"/san-pham/{product.SlugVi}",
                        UrlEn = $"/en/products/{(!string.IsNullOrEmpty(product.SlugEn) ? product.SlugEn : product.SlugVi)}",
                        ParentId = categoryMenu.Id,
                        Order = existingChildrenCount,
                        IsActive = true,
                        MenuType = "item",
                        SourceType = "Product",
                        SourceId = product.Id,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Menus.Add(menu);
                    added++;
                }
            }
            
            if (added > 0)
            {
                await _context.SaveChangesAsync();
            }
            
            return added;
        }

        public async Task<int> SyncNewsItemsAsync()
        {
            var news = await _context.News
                .Where(n => !n.Deleted && n.Status == 1)
                .ToListAsync();

            int added = 0;
            foreach (var newsItem in news)
            {
                var categoryMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "NewsCategory" 
                                         && m.SourceId == newsItem.NewsCategoryId 
                                         && !m.Deleted);
                
                if (categoryMenu == null) continue;

                var existingMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "News" 
                                         && m.SourceId == newsItem.Id 
                                         && !m.Deleted);
                
                if (existingMenu == null)
                {
                    // Get next sequential Order for this parent
                    var existingChildrenCount = await _context.Menus
                        .CountAsync(m => m.ParentId == categoryMenu.Id && !m.Deleted);

                    var menu = new Menu
                    {
                        Key = $"news-{newsItem.SlugVi}",
                        TitleVi = newsItem.TitleVi,
                        TitleEn = newsItem.TitleEn,
                        Url = $"/news/{newsItem.SlugVi}",
                        UrlEn = $"/en/news/{(!string.IsNullOrEmpty(newsItem.SlugEn) ? newsItem.SlugEn : newsItem.SlugVi)}",
                        ParentId = categoryMenu.Id,
                        Order = existingChildrenCount,
                        IsActive = true,
                        MenuType = "item",
                        SourceType = "News",
                        SourceId = newsItem.Id,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Menus.Add(menu);
                    added++;
                }
            }
            
            if (added > 0)
            {
                await _context.SaveChangesAsync();
            }
            
            return added;
        }

        public async Task<int> SyncNotificationItemsAsync()
        {
            var notifications = await _context.Notifications
                .Where(n => !n.Deleted && n.Status == 1)
                .ToListAsync();

            int added = 0;
            foreach (var notification in notifications)
            {
                var categoryMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "NotificationCategory" 
                                         && m.SourceId == notification.NotificationCategoryId 
                                         && !m.Deleted);
                
                if (categoryMenu == null) continue;

                var existingMenu = await _context.Menus
                    .FirstOrDefaultAsync(m => m.SourceType == "Notification" 
                                         && m.SourceId == notification.Id 
                                         && !m.Deleted);
                
                if (existingMenu == null)
                {
                    // Get next sequential Order for this parent
                    var existingChildrenCount = await _context.Menus
                        .CountAsync(m => m.ParentId == categoryMenu.Id && !m.Deleted);

                    var menu = new Menu
                    {
                        Key = $"notifications-{notification.SlugVi}",
                        TitleVi = notification.TitleVi,
                        TitleEn = notification.TitleEn,
                        Url = $"/notifications/{notification.SlugVi}",
                        UrlEn = $"/en/notifications/{(!string.IsNullOrEmpty(notification.SlugEn) ? notification.SlugEn : notification.SlugVi)}",
                        ParentId = categoryMenu.Id,
                        Order = existingChildrenCount,
                        IsActive = true,
                        MenuType = "item",
                        SourceType = "Notification",
                        SourceId = notification.Id,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Menus.Add(menu);
                    added++;
                }
            }
            
            if (added > 0)
            {
                await _context.SaveChangesAsync();
            }
            
            return added;
        }

        public async Task<object> GetFrontendMenuAsync(string language = "vi")
        {
            var allMenus = await _context.Menus
                .Where(m => !m.Deleted && m.IsActive)
                .ToListAsync();

            return BuildFlatMenuList(allMenus);
        }

        private object[] BuildFlatMenuList(List<Menu> allMenus)
        {
            var result = new List<object>();
            
            // Get root menus and sort by Order
            var rootMenus = allMenus
                .Where(m => m.ParentId == null)
                .OrderBy(m => m.Order)
                .ToList();
            
            foreach (var rootMenu in rootMenus)
            {
                // Add root menu
                result.Add(CreateFlatMenuItem(rootMenu, 0));
                
                // Add children recursively
                AddChildrenToFlatList(rootMenu, allMenus, result, 1);
            }
            
            return result.ToArray();
        }

        private void AddChildrenToFlatList(Menu parent, List<Menu> allMenus, List<object> result, int level)
        {
            var children = allMenus
                .Where(m => m.ParentId == parent.Id)
                .OrderBy(m => m.Order)
                .ToList();
            
            foreach (var child in children)
            {
                result.Add(CreateFlatMenuItem(child, level));
                
                // Recursively add grandchildren
                AddChildrenToFlatList(child, allMenus, result, level + 1);
            }
        }

        private object CreateFlatMenuItem(Menu menu, int level)
        {
            return new
            {
                id = menu.Id,
                key = menu.Key,
                titleVi = menu.TitleVi,
                titleEn = menu.TitleEn,
                url = menu.Url,
                urlEn = menu.UrlEn ?? menu.Url,
                level = level,
                order = menu.Order,
                parentId = menu.ParentId,
                isActive = menu.IsActive,
                isExternal = menu.IsExternal,
                target = menu.Target ?? "_self",
                menuType = menu.MenuType
            };
        }



        private static MenuDto MapToDto(Menu menu)
        {
            return new MenuDto
            {
                Id = menu.Id,
                TitleVi = menu.TitleVi,
                TitleEn = menu.TitleEn,
                Url = menu.Url,
                ParentId = menu.ParentId,
                Order = menu.Order,
                IsActive = menu.IsActive,
                IsExternal = menu.IsExternal,
                Target = menu.Target,
                DescriptionVi = menu.DescriptionVi,
                DescriptionEn = menu.DescriptionEn,
                MenuType = menu.MenuType,
                SourceType = menu.SourceType,
                SourceId = menu.SourceId,
                CreatedDate = menu.CreatedDate
            };
        }

        private static MenuTreeDto MapToTreeDto(Menu menu, List<Menu> allMenus, string language, int level = 0)
        {
            var children = allMenus
                .Where(m => m.ParentId == menu.Id && m.IsActive)
                .OrderBy(m => m.Order)
                .Select(child => MapToTreeDto(child, allMenus, language, level + 1))
                .ToList();

            return new MenuTreeDto
            {
                Id = menu.Id,
                Key = menu.Key,
                Title = language == "en" ? menu.TitleEn : menu.TitleVi,
                TitleVi = menu.TitleVi,
                TitleEn = menu.TitleEn,
                Url = language == "en" ? menu.UrlEn : menu.Url,
                UrlVi = menu.Url,
                UrlEn = menu.UrlEn,
                Level = level,
                Order = menu.Order,
                ParentId = menu.ParentId,
                IsActive = menu.IsActive,
                IsExternal = menu.IsExternal,
                Target = menu.Target,
                MenuType = menu.MenuType,
                Children = children
            };
        }
    }
}