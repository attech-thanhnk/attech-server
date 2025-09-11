using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.NewsCategory;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AttechServer.Applications.UserModules.Implements
{
    public class NewsCategoryService : INewsCategoryService
    {
        private readonly ILogger<NewsCategoryService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;

        public NewsCategoryService(ApplicationDbContext dbContext, ILogger<NewsCategoryService> logger, IHttpContextAccessor httpContextAccessor, IActivityLogService activityLogService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
        }

        public async Task<NewsCategoryDto> Create(CreateNewsCategoryDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: input = {JsonSerializer.Serialize(input)}");
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Validate input
                    if (string.IsNullOrWhiteSpace(input.TitleVi) || string.IsNullOrWhiteSpace(input.SlugVi))
                    {
                        throw new ArgumentException("Tên danh mục và Slug (VI) là bắt buộc.");
                    }
                    if (string.IsNullOrWhiteSpace(input.TitleEn) || string.IsNullOrWhiteSpace(input.SlugEn))
                    {
                        throw new ArgumentException("Tên danh mục và Slug (EN) là bắt buộc.");
                    }
                    if (input.DescriptionVi.Length > 160)
                    {
                        input.DescriptionVi = input.DescriptionVi.Substring(0, 157) + "...";
                    }
                    if (input.DescriptionEn.Length > 160)
                    {
                        input.DescriptionEn = input.DescriptionEn.Substring(0, 157) + "...";
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var id) ? id : 0;

                    // Kiểm tra trùng tên danh mục
                    var nameViExists = await _dbContext.NewsCategories.AnyAsync(c => c.TitleVi == input.TitleVi && !c.Deleted);
                    if (nameViExists)
                    {
                        throw new ArgumentException("Tên danh mục (VI) đã tồn tại.");
                    }
                    var nameEnExists = await _dbContext.NewsCategories.AnyAsync(c => c.TitleEn == input.TitleEn && !c.Deleted);
                    if (nameEnExists)
                    {
                        throw new ArgumentException("Tên danh mục (EN) đã tồn tại.");
                    }

                    // Kiểm tra trùng slug
                    var slugViExists = await _dbContext.NewsCategories.AnyAsync(c => c.SlugVi == input.SlugVi && !c.Deleted);
                    if (slugViExists)
                    {
                        throw new ArgumentException("Slug (VI) đã tồn tại.");
                    }
                    var slugEnExists = await _dbContext.NewsCategories.AnyAsync(c => c.SlugEn == input.SlugEn && !c.Deleted);
                    if (slugEnExists)
                    {
                        throw new ArgumentException("Slug (EN) đã tồn tại.");
                    }

                    var newNewsCategory = new NewsCategory
                    {
                        TitleVi = input.TitleVi,
                        TitleEn = input.TitleEn,
                        SlugVi = input.SlugVi,
                        SlugEn = input.SlugEn,
                        DescriptionVi = input.DescriptionVi,
                        DescriptionEn = input.DescriptionEn,
                        ParentId = input.ParentId,
                        Order = input.Order,
                        Status = input.Status,
                        CreatedBy = userId,
                        Deleted = false
                    };

                    _dbContext.NewsCategories.Add(newNewsCategory);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogUserActionAsync(
                        "CREATE_NEWS_CATEGORY",
                        $"Đã tạo danh mục tin tức mới: {input.TitleVi}",
                        userId,
                        JsonSerializer.Serialize(new
                        {
                            categoryId = newNewsCategory.Id,
                            categoryName = input.TitleVi
                        })
                    );

                    return new NewsCategoryDto
                    {
                        Id = newNewsCategory.Id,
                        TitleVi = newNewsCategory.TitleVi,
                        TitleEn = newNewsCategory.TitleEn,
                        SlugVi = newNewsCategory.SlugVi,
                        SlugEn = newNewsCategory.SlugEn,
                        DescriptionVi = newNewsCategory.DescriptionVi,
                        DescriptionEn = newNewsCategory.DescriptionEn,
                        ParentId = newNewsCategory.ParentId,
                        Order = newNewsCategory.Order,
                        Status = newNewsCategory.Status,
                        CreatedDate = newNewsCategory.CreatedDate,
                        ModifiedDate = newNewsCategory.ModifiedDate
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating news category");
                    throw;
                }
            }
        }

        public async Task Delete(int id)
        {
            _logger.LogInformation($"{nameof(Delete)}: id = {id}");
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var userIdValue) ? userIdValue : 0;

                    var category = await _dbContext.NewsCategories
                        .Include(c => c.News)
                        .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                    }

                    // Kiểm tra xem có tin tức nào trong danh mục không
                    if (category.News.Any(n => !n.Deleted))
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryContainsNews);
                    }

                    category.Deleted = true;
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity - temporarily commented out
                    // await _activityLogService.LogUserActionAsync(
                    //     "DELETE_NEWS_CATEGORY",
                    //     $"Đã xóa danh mục tin tức: {category.TitleVi}",
                    //     null,
                    //     JsonSerializer.Serialize(new
                    //     {
                    //         categoryId = category.Id,
                    //         categoryName = category.TitleVi
                    //     })
                    // );
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error deleting news category with id = {id}");
                    throw;
                }
            }
        }

        public async Task<PagingResult<NewsCategoryDto>> FindAll(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            var query = _dbContext.NewsCategories
                .Where(c => !c.Deleted)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var searchTerm = input.Keyword.ToLower();
                query = query.Where(c =>
                    c.TitleVi.ToLower().Contains(searchTerm) ||
                    c.TitleEn.ToLower().Contains(searchTerm) ||
                    c.DescriptionVi.ToLower().Contains(searchTerm) ||
                    c.DescriptionEn.ToLower().Contains(searchTerm)
                );
            }

            // Apply sorting
            if (input.Sort.Any())
            {
                // TODO: Implement dynamic sorting based on input.Sort
            }

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize)
                .Select(c => new NewsCategoryDto
                {
                    Id = c.Id,
                    TitleVi = c.TitleVi,
                    TitleEn = c.TitleEn,
                    SlugVi = c.SlugVi,
                    SlugEn = c.SlugEn,
                    DescriptionVi = c.DescriptionVi,
                    DescriptionEn = c.DescriptionEn,
                    ParentId = c.ParentId,
                    Order = c.Order,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    ModifiedDate = c.ModifiedDate
                })
                .ToListAsync();

            return new PagingResult<NewsCategoryDto>
            {
                Items = items,
                TotalItems = totalItems
            };
        }

        public async Task<DetailNewsCategoryDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var category = await _dbContext.NewsCategories
                .Include(c => c.News.Where(n => !n.Deleted))
                .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

            if (category == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
            }

            return new DetailNewsCategoryDto
            {
                Id = category.Id,
                TitleVi = category.TitleVi,
                TitleEn = category.TitleEn,
                SlugVi = category.SlugVi,
                SlugEn = category.SlugEn,
                DescriptionVi = category.DescriptionVi,
                DescriptionEn = category.DescriptionEn,
                ParentId = category.ParentId,
                Order = category.Order,
                Status = category.Status,
                NewsCount = category.News.Count,
                CreatedDate = category.CreatedDate,
                ModifiedDate = category.ModifiedDate,
                CreatedBy = category.CreatedBy,
                ModifiedBy = category.ModifiedBy
            };
        }

        public async Task<DetailNewsCategoryDto> FindBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlug)}: slug = {slug}");

            var category = await _dbContext.NewsCategories
                .Include(c => c.News.Where(n => !n.Deleted))
                .FirstOrDefaultAsync(c => (c.SlugVi == slug || c.SlugEn == slug) && !c.Deleted);

            if (category == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
            }

            return new DetailNewsCategoryDto
            {
                Id = category.Id,
                TitleVi = category.TitleVi,
                TitleEn = category.TitleEn,
                SlugVi = category.SlugVi,
                SlugEn = category.SlugEn,
                DescriptionVi = category.DescriptionVi,
                DescriptionEn = category.DescriptionEn,
                ParentId = category.ParentId,
                Order = category.Order,
                Status = category.Status,
                NewsCount = category.News.Count,
                CreatedDate = category.CreatedDate,
                ModifiedDate = category.ModifiedDate,
                CreatedBy = category.CreatedBy,
                ModifiedBy = category.ModifiedBy
            };
        }

        public async Task<NewsCategoryDto> Update(UpdateNewsCategoryDto input)
        {
            _logger.LogInformation($"{nameof(Update)}: input = {JsonSerializer.Serialize(input)}");
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Validate input
                    if (string.IsNullOrWhiteSpace(input.TitleVi) || string.IsNullOrWhiteSpace(input.SlugVi))
                    {
                        throw new ArgumentException("Tên danh mục và Slug (VI) là bắt buộc.");
                    }
                    if (string.IsNullOrWhiteSpace(input.TitleEn) || string.IsNullOrWhiteSpace(input.SlugEn))
                    {
                        throw new ArgumentException("Tên danh mục và Slug (EN) là bắt buộc.");
                    }
                    if (input.DescriptionVi.Length > 160)
                    {
                        input.DescriptionVi = input.DescriptionVi.Substring(0, 157) + "...";
                    }
                    if (input.DescriptionEn.Length > 160)
                    {
                        input.DescriptionEn = input.DescriptionEn.Substring(0, 157) + "...";
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var id) ? id : 0;

                    var category = await _dbContext.NewsCategories
                        .FirstOrDefaultAsync(c => c.Id == input.Id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                    }

                    // Kiểm tra trùng tên danh mục (trừ chính nó)
                    var nameViExists = await _dbContext.NewsCategories.AnyAsync(c => c.TitleVi == input.TitleVi && c.Id != input.Id && !c.Deleted);
                    if (nameViExists)
                    {
                        throw new ArgumentException("Tên danh mục (VI) đã tồn tại.");
                    }
                    var nameEnExists = await _dbContext.NewsCategories.AnyAsync(c => c.TitleEn == input.TitleEn && c.Id != input.Id && !c.Deleted);
                    if (nameEnExists)
                    {
                        throw new ArgumentException("Tên danh mục (EN) đã tồn tại.");
                    }

                    // Kiểm tra trùng slug (trừ chính nó)
                    var slugViExists = await _dbContext.NewsCategories.AnyAsync(c => c.SlugVi == input.SlugVi && c.Id != input.Id && !c.Deleted);
                    if (slugViExists)
                    {
                        throw new ArgumentException("Slug (VI) đã tồn tại.");
                    }
                    var slugEnExists = await _dbContext.NewsCategories.AnyAsync(c => c.SlugEn == input.SlugEn && c.Id != input.Id && !c.Deleted);
                    if (slugEnExists)
                    {
                        throw new ArgumentException("Slug (EN) đã tồn tại.");
                    }

                    // Update category
                    category.TitleVi = input.TitleVi;
                    category.TitleEn = input.TitleEn;
                    category.SlugVi = input.SlugVi;
                    category.SlugEn = input.SlugEn;
                    category.DescriptionVi = input.DescriptionVi;
                    category.DescriptionEn = input.DescriptionEn;
                    category.ParentId = input.ParentId;
                    category.Order = input.Order;
                    category.Status = input.Status;
                    category.ModifiedBy = userId;
                    category.ModifiedDate = DateTime.Now;

                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogUserActionAsync(
                        "UPDATE_NEWS_CATEGORY",
                        $"Đã cập nhật danh mục tin tức: {input.TitleVi}",
                        userId,
                        JsonSerializer.Serialize(new
                        {
                            categoryId = category.Id,
                            categoryName = input.TitleVi
                        })
                    );

                    return new NewsCategoryDto
                    {
                        Id = category.Id,
                        TitleVi = category.TitleVi,
                        TitleEn = category.TitleEn,
                        SlugVi = category.SlugVi,
                        SlugEn = category.SlugEn,
                        DescriptionVi = category.DescriptionVi,
                        DescriptionEn = category.DescriptionEn,
                        ParentId = category.ParentId,
                        Order = category.Order,
                        Status = category.Status,
                        CreatedDate = category.CreatedDate,
                        ModifiedDate = category.ModifiedDate
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating news category");
                    throw;
                }
            }
        }

        public async Task UpdateStatus(int id, int status)
        {
            _logger.LogInformation($"{nameof(UpdateStatus)}: id = {id}, status = {status}");
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var category = await _dbContext.NewsCategories
                        .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var userIdValue) ? userIdValue : 0;

                    category.Status = status;
                    category.ModifiedBy = userId;
                    category.ModifiedDate = DateTime.Now;

                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogUserActionAsync(
                        "UPDATE_NEWS_CATEGORY_STATUS",
                        $"Đã cập nhật trạng thái danh mục tin tức: {category.TitleVi}",
                        userId,
                        JsonSerializer.Serialize(new
                        {
                            categoryId = category.Id,
                            categoryName = category.TitleVi,
                            status = status
                        })
                    );
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error updating news category status with id = {id}, status = {status}");
                    throw;
                }
            }
        }

        #region Hierarchy Methods

        /// <summary>
        /// Check if category has children
        /// </summary>
        public async Task<bool> HasChildrenAsync(int categoryId)
        {
            return await _dbContext.NewsCategories
                .AnyAsync(c => c.ParentId == categoryId && !c.Deleted);
        }

        /// <summary>
        /// Get all descendant category IDs (recursive)
        /// </summary>
        public async Task<List<int>> GetDescendantIdsAsync(int parentId)
        {
            var result = new List<int>();
            var directChildren = await _dbContext.NewsCategories
                .Where(c => c.ParentId == parentId && !c.Deleted)
                .Select(c => c.Id)
                .ToListAsync();

            result.AddRange(directChildren);

            foreach (var childId in directChildren)
            {
                var grandChildren = await GetDescendantIdsAsync(childId);
                result.AddRange(grandChildren);
            }

            return result;
        }

        /// <summary>
        /// Get breadcrumb path for category
        /// </summary>
        public async Task<List<NewsCategoryDto>> GetBreadcrumbAsync(int categoryId)
        {
            var result = new List<NewsCategoryDto>();
            var category = await _dbContext.NewsCategories
                .Where(c => c.Id == categoryId && !c.Deleted)
                .Select(c => new NewsCategoryDto
                {
                    Id = c.Id,
                    TitleVi = c.TitleVi,
                    TitleEn = c.TitleEn,
                    SlugVi = c.SlugVi,
                    SlugEn = c.SlugEn
                })
                .FirstOrDefaultAsync();

            if (category == null) return result;

            // Get parent chain
            int? currentId = categoryId;
            while (currentId != null)
            {
                var current = await _dbContext.NewsCategories
                    .Where(c => c.Id == currentId && !c.Deleted)
                    .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn, c.ParentId })
                    .FirstOrDefaultAsync();

                if (current != null)
                {
                    result.Insert(0, new NewsCategoryDto
                    {
                        Id = current.Id,
                        TitleVi = current.TitleVi,
                        TitleEn = current.TitleEn,
                        SlugVi = current.SlugVi,
                        SlugEn = current.SlugEn
                    });
                    currentId = current.ParentId;
                }
                else break;
            }

            return result;
        }

        #endregion
    }
} 
