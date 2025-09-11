using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.GlobalSearch;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/search")]
    [ApiController]
    [AllowAnonymous]
    public class GlobalSearchController : ApiControllerBase
    {
        private readonly INewsService _newsService;
        private readonly IProductService _productService;
        private readonly IServiceService _serviceService;
        private readonly INotificationService _notificationService;

        public GlobalSearchController(
            INewsService newsService,
            IProductService productService,
            IServiceService serviceService,
            INotificationService notificationService,
            ILogger<GlobalSearchController> logger) 
            : base(logger)
        {
            _newsService = newsService;
            _productService = productService;
            _serviceService = serviceService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Global search across Products, News, Services, Notifications
        /// </summary>
        /// <param name="keyword">Search keyword</param>
        /// <param name="limit">Max items per category (default: 5)</param>
        [HttpGet("global")]
        [CacheResponse(CacheProfiles.ShortCache, "global-search", varyByQueryString: true)]
        public async Task<ApiResponse> GlobalSearch([FromQuery] string keyword, [FromQuery] int limit = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 400, "Từ khóa tìm kiếm không được để trống");
                }

                if (keyword.Length < 2)
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 400, "Từ khóa tìm kiếm phải có ít nhất 2 ký tự");
                }

                var results = new List<SearchCategoryResultDto>();

                // Search sequentially to avoid DbContext threading issues
                var newsResult = await SearchNewsAsync(keyword, limit);
                if (newsResult.Count > 0) results.Add(newsResult);

                var productResult = await SearchProductsAsync(keyword, limit);
                if (productResult.Count > 0) results.Add(productResult);

                var serviceResult = await SearchServicesAsync(keyword, limit);
                if (serviceResult.Count > 0) results.Add(serviceResult);

                var notificationResult = await SearchNotificationsAsync(keyword, limit);
                if (notificationResult.Count > 0) results.Add(notificationResult);

                var response = new GlobalSearchResultDto
                {
                    Keyword = keyword,
                    Results = results,
                    TotalResults = results.Sum(r => r.Count),
                    SearchTime = DateTime.UtcNow
                };

                return new ApiResponse(ApiStatusCode.Success, response, 200, "Tìm kiếm thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing global search with keyword: {Keyword}", keyword);
                return OkException(ex);
            }
        }

        private async Task<SearchCategoryResultDto> SearchNewsAsync(string keyword, int limit)
        {
            try
            {
                var input = new PagingRequestBaseDto
                {
                    Keyword = keyword,
                    PageNumber = 1,
                    PageSize = limit
                };

                var newsResult = await _newsService.SearchForClient(input);
                
                return new SearchCategoryResultDto
                {
                    CategoryName = "Tin tức",
                    CategoryNameEn = "News",
                    Count = newsResult.TotalItems,
                    Items = newsResult.Items.Select(n => new SearchItemDto
                    {
                        Id = n.Id,
                        TitleVi = n.TitleVi,
                        TitleEn = n.TitleEn,
                        SlugVi = n.SlugVi,
                        SlugEn = n.SlugEn,
                        DescriptionVi = n.DescriptionVi,
                        DescriptionEn = n.DescriptionEn,
                        ImageUrl = n.ImageUrl ?? "",
                        Type = "news",
                        CreatedDate = n.TimePosted
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching news with keyword: {Keyword}", keyword);
                return new SearchCategoryResultDto
                {
                    CategoryName = "Tin tức",
                    CategoryNameEn = "News",
                    Count = 0,
                    Items = new List<SearchItemDto>()
                };
            }
        }

        private async Task<SearchCategoryResultDto> SearchProductsAsync(string keyword, int limit)
        {
            try
            {
                var input = new PagingRequestBaseDto
                {
                    Keyword = keyword,
                    PageNumber = 1,
                    PageSize = limit
                };

                var productResult = await _productService.SearchForClient(input);
                
                return new SearchCategoryResultDto
                {
                    CategoryName = "Sản phẩm",
                    CategoryNameEn = "Products",
                    Count = productResult.TotalItems,
                    Items = productResult.Items.Select(p => new SearchItemDto
                    {
                        Id = p.Id,
                        TitleVi = p.TitleVi,
                        TitleEn = p.TitleEn,
                        SlugVi = p.SlugVi,
                        SlugEn = p.SlugEn,
                        DescriptionVi = p.DescriptionVi,
                        DescriptionEn = p.DescriptionEn,
                        ImageUrl = p.ImageUrl ?? "",
                        Type = "product",
                        CreatedDate = p.TimePosted
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with keyword: {Keyword}", keyword);
                return new SearchCategoryResultDto
                {
                    CategoryName = "Sản phẩm",
                    CategoryNameEn = "Products",
                    Count = 0,
                    Items = new List<SearchItemDto>()
                };
            }
        }

        private async Task<SearchCategoryResultDto> SearchServicesAsync(string keyword, int limit)
        {
            try
            {
                var input = new PagingRequestBaseDto
                {
                    Keyword = keyword,
                    PageNumber = 1,
                    PageSize = limit
                };

                var serviceResult = await _serviceService.SearchForClient(input);
                
                return new SearchCategoryResultDto
                {
                    CategoryName = "Dịch vụ",
                    CategoryNameEn = "Services",
                    Count = serviceResult.TotalItems,
                    Items = serviceResult.Items.Select(s => new SearchItemDto
                    {
                        Id = s.Id,
                        TitleVi = s.TitleVi,
                        TitleEn = s.TitleEn,
                        SlugVi = s.SlugVi,
                        SlugEn = s.SlugEn,
                        DescriptionVi = s.DescriptionVi,
                        DescriptionEn = s.DescriptionEn,
                        ImageUrl = s.ImageUrl ?? "",
                        Type = "service",
                        CreatedDate = s.TimePosted
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching services with keyword: {Keyword}", keyword);
                return new SearchCategoryResultDto
                {
                    CategoryName = "Dịch vụ",
                    CategoryNameEn = "Services",
                    Count = 0,
                    Items = new List<SearchItemDto>()
                };
            }
        }

        private async Task<SearchCategoryResultDto> SearchNotificationsAsync(string keyword, int limit)
        {
            try
            {
                var input = new PagingRequestBaseDto
                {
                    Keyword = keyword,
                    PageNumber = 1,
                    PageSize = limit
                };

                var notificationResult = await _notificationService.SearchForClient(input);
                
                return new SearchCategoryResultDto
                {
                    CategoryName = "Thông báo",
                    CategoryNameEn = "Notifications",
                    Count = notificationResult.TotalItems,
                    Items = notificationResult.Items.Select(n => new SearchItemDto
                    {
                        Id = n.Id,
                        TitleVi = n.TitleVi,
                        TitleEn = n.TitleEn,
                        SlugVi = n.SlugVi,
                        SlugEn = n.SlugEn,
                        DescriptionVi = n.DescriptionVi,
                        DescriptionEn = n.DescriptionEn,
                        ImageUrl = n.ImageUrl ?? "",
                        Type = "notification",
                        CreatedDate = n.TimePosted
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching notifications with keyword: {Keyword}", keyword);
                return new SearchCategoryResultDto
                {
                    CategoryName = "Thông báo",
                    CategoryNameEn = "Notifications",
                    Count = 0,
                    Items = new List<SearchItemDto>()
                };
            }
        }
    }
}