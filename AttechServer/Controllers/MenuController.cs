using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Menu;
using AttechServer.Shared.WebAPIBase;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Controllers
{
    [Route("api/menu")]
    [ApiController]
    [Authorize]
    public class MenuController : ApiControllerBase
    {
        private readonly IMenuService _menuService;

        public MenuController(IMenuService menuService, ILogger<MenuController> logger) : base(logger)
        {
            _menuService = menuService;
        }

        [HttpGet("list")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetAll()
        {
            try
            {
                var menus = await _menuService.GetAllAsync();
                return new ApiResponse(ApiStatusCode.Success, menus, 200, "Ok");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("tree")]
        [AllowAnonymous]
        public async Task<ApiResponse> GetMenuTree([FromQuery] string language = "vi")
        {
            try
            {
                var menuTree = await _menuService.GetMenuTreeAsync(language);
                return new ApiResponse(ApiStatusCode.Success, menuTree, 200, "Ok");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("detail/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetById(int id)
        {
            try
            {
                var menu = await _menuService.GetByIdAsync(id);
                if (menu == null)
                    return new ApiResponse(ApiStatusCode.Success, null, 404, "Menu không tồn tại");

                return new ApiResponse(ApiStatusCode.Success, menu, 200, "Ok");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("create")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Create([FromBody] CreateMenuDto input)
        {
            try
            {
                var menu = await _menuService.CreateAsync(input);
                return new ApiResponse(ApiStatusCode.Success, menu, 201, "Tạo menu thành công");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPut("update/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateMenuDto input)
        {
            try
            {
                var menu = await _menuService.UpdateAsync(id, input);
                if (menu == null)
                    return new ApiResponse(ApiStatusCode.Success, null, 404, "Menu không tồn tại");

                return new ApiResponse(ApiStatusCode.Success, menu, 200, "Cập nhật menu thành công");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpDelete("delete/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                var result = await _menuService.DeleteAsync(id);
                if (!result)
                    return new ApiResponse(ApiStatusCode.Success, false, 404, "Menu không tồn tại");

                return new ApiResponse(ApiStatusCode.Success, true, 200, "Xóa menu thành công");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPut("reorder")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Reorder([FromBody] List<int> menuIds)
        {
            try
            {
                var result = await _menuService.ReorderAsync(menuIds);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Sắp xếp menu thành công");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-categories")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncAllCategories()
        {
            try
            {
                var result = await _menuService.SyncAllCategoriesAsync();
                return new ApiResponse(ApiStatusCode.Success, result, 200, 
                    $"Đồng bộ thành công {result.TotalAdded} menu items");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-products")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncProducts()
        {
            try
            {
                var added = await _menuService.SyncProductCategoriesAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} sản phẩm categories");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-news")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncNews()
        {
            try
            {
                var added = await _menuService.SyncNewsCategoriesAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} tin tức categories");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-notifications")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncNotifications()
        {
            try
            {
                var added = await _menuService.SyncNotificationCategoriesAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} thông báo categories");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-services")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncServices()
        {
            try
            {
                var added = await _menuService.SyncServicesAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} dịch vụ");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-product-items")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncProductItems()
        {
            try
            {
                var added = await _menuService.SyncProductItemsAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} sản phẩm items");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-news-items")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncNewsItems()
        {
            try
            {
                var added = await _menuService.SyncNewsItemsAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} tin tức items");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpPost("sync-notification-items")]
        [RoleFilter(2)]
        public async Task<ApiResponse> SyncNotificationItems()
        {
            try
            {
                var added = await _menuService.SyncNotificationItemsAsync();
                return new ApiResponse(ApiStatusCode.Success, added, 200, 
                    $"Đồng bộ {added} thông báo items");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        [HttpGet("frontend")]
        [AllowAnonymous]
        public async Task<ApiResponse> GetFrontendMenu([FromQuery] string language = "vi")
        {
            try
            {
                var menu = await _menuService.GetFrontendMenuAsync(language);
                return new ApiResponse(ApiStatusCode.Success, menu, 200, "Ok");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}