using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.NotificationCategory;
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
    public class NotificationCategoryService : INotificationCategoryService
    {
        private readonly ILogger<NotificationCategoryService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;

        public NotificationCategoryService(ApplicationDbContext dbContext, ILogger<NotificationCategoryService> logger, IHttpContextAccessor httpContextAccessor, IActivityLogService activityLogService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
        }

        public async Task<NotificationCategoryDto> Create(CreateNotificationCategoryDto input)
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
                    var nameViExists = await _dbContext.NotificationCategories.AnyAsync(c => c.TitleVi == input.TitleVi && !c.Deleted);
                    if (nameViExists)
                    {
                        throw new ArgumentException("Tên danh mục (VI) đã tồn tại.");
                    }
                    var nameEnExists = await _dbContext.NotificationCategories.AnyAsync(c => c.TitleEn == input.TitleEn && !c.Deleted);
                    if (nameEnExists)
                    {
                        throw new ArgumentException("Tên danh mục (EN) đã tồn tại.");
                    }

                    // Kiểm tra trùng slug
                    var slugViExists = await _dbContext.NotificationCategories.AnyAsync(c => c.SlugVi == input.SlugVi && !c.Deleted);
                    if (slugViExists)
                    {
                        throw new ArgumentException("Slug (VI) đã tồn tại.");
                    }
                    var slugEnExists = await _dbContext.NotificationCategories.AnyAsync(c => c.SlugEn == input.SlugEn && !c.Deleted);
                    if (slugEnExists)
                    {
                        throw new ArgumentException("Slug (EN) đã tồn tại.");
                    }

                    var newNotificationCategory = new NotificationCategory
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

                    _dbContext.NotificationCategories.Add(newNotificationCategory);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogUserActionAsync(
                        "CREATE_NOTIFICATION_CATEGORY",
                        $"Đã tạo danh mục thông báo mới: {input.TitleVi}",
                        userId,
                        JsonSerializer.Serialize(new
                        {
                            categoryId = newNotificationCategory.Id,
                            categoryName = input.TitleVi
                        })
                    );

                    return new NotificationCategoryDto
                    {
                        Id = newNotificationCategory.Id,
                        TitleVi = newNotificationCategory.TitleVi,
                        TitleEn = newNotificationCategory.TitleEn,
                        SlugVi = newNotificationCategory.SlugVi,
                        SlugEn = newNotificationCategory.SlugEn,
                        DescriptionVi = newNotificationCategory.DescriptionVi,
                        DescriptionEn = newNotificationCategory.DescriptionEn,
                        ParentId = newNotificationCategory.ParentId,
                        Order = newNotificationCategory.Order,
                        Status = newNotificationCategory.Status,
                        CreatedDate = newNotificationCategory.CreatedDate,
                        ModifiedDate = newNotificationCategory.ModifiedDate
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating notification category");
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

                    var category = await _dbContext.NotificationCategories
                        .Include(c => c.Notifications)
                        .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
                    }

                    // Kiểm tra xem có thông báo nào trong danh mục không
                    if (category.Notifications.Any(n => !n.Deleted))
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryContainsNotifications);
                    }

                    category.Deleted = true;
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity - temporarily commented out
                    // await _activityLogService.LogUserActionAsync(
                    //     "DELETE_NOTIFICATION_CATEGORY",
                    //     $"Đã xóa danh mục thông báo: {category.TitleVi}",
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
                    _logger.LogError(ex, $"Error deleting notification category with id = {id}");
                    throw;
                }
            }
        }

        public async Task<PagingResult<NotificationCategoryDto>> FindAll(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            var query = _dbContext.NotificationCategories
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
                .Select(c => new NotificationCategoryDto
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

            return new PagingResult<NotificationCategoryDto>
            {
                Items = items,
                TotalItems = totalItems
            };
        }

        public async Task<DetailNotificationCategoryDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var category = await _dbContext.NotificationCategories
                .Include(c => c.Notifications.Where(n => !n.Deleted))
                .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

            if (category == null)
            {
                throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
            }

            return new DetailNotificationCategoryDto
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
                NotificationCount = category.Notifications.Count,
                CreatedDate = category.CreatedDate,
                ModifiedDate = category.ModifiedDate,
                CreatedBy = category.CreatedBy,
                ModifiedBy = category.ModifiedBy
            };
        }

        public async Task<DetailNotificationCategoryDto> FindBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlug)}: slug = {slug}");

            var category = await _dbContext.NotificationCategories
                .Include(c => c.Notifications.Where(n => !n.Deleted))
                .FirstOrDefaultAsync(c => (c.SlugVi == slug || c.SlugEn == slug) && !c.Deleted);

            if (category == null)
            {
                throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
            }

            return new DetailNotificationCategoryDto
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
                NotificationCount = category.Notifications.Count,
                CreatedDate = category.CreatedDate,
                ModifiedDate = category.ModifiedDate,
                CreatedBy = category.CreatedBy,
                ModifiedBy = category.ModifiedBy
            };
        }

        public async Task<NotificationCategoryDto> Update(UpdateNotificationCategoryDto input)
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

                    var category = await _dbContext.NotificationCategories
                        .FirstOrDefaultAsync(c => c.Id == input.Id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
                    }

                    // Kiểm tra trùng tên danh mục (trừ chính nó)
                    var nameViExists = await _dbContext.NotificationCategories.AnyAsync(c => c.TitleVi == input.TitleVi && c.Id != input.Id && !c.Deleted);
                    if (nameViExists)
                    {
                        throw new ArgumentException("Tên danh mục (VI) đã tồn tại.");
                    }
                    var nameEnExists = await _dbContext.NotificationCategories.AnyAsync(c => c.TitleEn == input.TitleEn && c.Id != input.Id && !c.Deleted);
                    if (nameEnExists)
                    {
                        throw new ArgumentException("Tên danh mục (EN) đã tồn tại.");
                    }

                    // Kiểm tra trùng slug (trừ chính nó)
                    var slugViExists = await _dbContext.NotificationCategories.AnyAsync(c => c.SlugVi == input.SlugVi && c.Id != input.Id && !c.Deleted);
                    if (slugViExists)
                    {
                        throw new ArgumentException("Slug (VI) đã tồn tại.");
                    }
                    var slugEnExists = await _dbContext.NotificationCategories.AnyAsync(c => c.SlugEn == input.SlugEn && c.Id != input.Id && !c.Deleted);
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
                        "UPDATE_NOTIFICATION_CATEGORY",
                        $"Đã cập nhật danh mục thông báo: {input.TitleVi}",
                        userId,
                        JsonSerializer.Serialize(new
                        {
                            categoryId = category.Id,
                            categoryName = input.TitleVi
                        })
                    );

                    return new NotificationCategoryDto
                    {
                        Id = category.Id,
                        TitleVi = category.TitleVi,
                        TitleEn = category.TitleEn,
                        SlugVi = category.SlugVi,
                        SlugEn = category.SlugEn,
                        DescriptionVi = category.DescriptionVi,
                        DescriptionEn = category.DescriptionEn,
                        Status = category.Status
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating notification category");
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
                    var category = await _dbContext.NotificationCategories
                        .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);

                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var userIdValue) ? userIdValue : 0;

                    category.Status = status;
                    category.ModifiedBy = userId;
                    category.ModifiedDate = DateTime.Now;

                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogUserActionAsync(
                        "UPDATE_NOTIFICATION_CATEGORY_STATUS",
                        $"Đã cập nhật trạng thái danh mục thông báo: {category.TitleVi}",
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
                    _logger.LogError(ex, $"Error updating notification category status with id = {id}, status = {status}");
                    throw;
                }
            }
        }

        public async Task<bool> HasChildrenAsync(int categoryId)
        {
            return await _dbContext.NotificationCategories
                .AnyAsync(c => c.ParentId == categoryId && !c.Deleted);
        }

        public async Task<List<int>> GetDescendantIdsAsync(int parentId)
        {
            // Load tất cả categories một lần để tránh N+1 queries
            var allCategories = await _dbContext.NotificationCategories
                .Where(c => !c.Deleted)
                .Select(c => new { c.Id, c.ParentId })
                .ToListAsync();

            var result = new List<int>();
            var toProcess = new Queue<int>();
            toProcess.Enqueue(parentId);

            while (toProcess.Count > 0)
            {
                var currentId = toProcess.Dequeue();
                var children = allCategories
                    .Where(c => c.ParentId == currentId)
                    .Select(c => c.Id)
                    .ToList();

                foreach (var childId in children)
                {
                    result.Add(childId);
                    toProcess.Enqueue(childId);
                }
            }

            return result;
        }

        public async Task<List<NotificationCategoryDto>> GetBreadcrumbAsync(int categoryId)
        {
            var breadcrumb = new List<NotificationCategoryDto>();
            var currentCategory = await _dbContext.NotificationCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId && !c.Deleted);

            while (currentCategory != null)
            {
                breadcrumb.Insert(0, new NotificationCategoryDto
                {
                    Id = currentCategory.Id,
                    TitleVi = currentCategory.TitleVi,
                    TitleEn = currentCategory.TitleEn,
                    SlugVi = currentCategory.SlugVi,
                    SlugEn = currentCategory.SlugEn,
                    DescriptionVi = currentCategory.DescriptionVi,
                    DescriptionEn = currentCategory.DescriptionEn,
                    ParentId = currentCategory.ParentId,
                    Order = currentCategory.Order,
                    Status = currentCategory.Status
                });

                if (currentCategory.ParentId.HasValue)
                {
                    currentCategory = await _dbContext.NotificationCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == currentCategory.ParentId && !c.Deleted);
                }
                else
                {
                    currentCategory = null;
                }
            }

            return breadcrumb;
        }
    }
} 
