using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Notification;
using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Abstractions;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AttechServer.Applications.UserModules.Implements
{
    public class NotificationService : INotificationService
    {
        private const int MAX_CONTENT_LENGTH = 100000; // 100KB
        private readonly ILogger<NotificationService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWysiwygFileProcessor _wysiwygFileProcessor;
        private readonly IActivityLogService _activityLogService;
        private readonly IAttachmentService _attachmentService;

        public NotificationService(ApplicationDbContext dbContext, ILogger<NotificationService> logger, IHttpContextAccessor httpContextAccessor, IWysiwygFileProcessor wysiwygFileProcessor, IActivityLogService activityLogService, IAttachmentService attachmentService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _wysiwygFileProcessor = wysiwygFileProcessor;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
        }

        private string TruncateDescription(string description, int maxLength = 160)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;
            return description.Length <= maxLength ? description : description.Substring(0, maxLength - 3) + "...";
        }

        private IQueryable<Notification> ApplySorting(IQueryable<Notification> query, PagingRequestBaseDto input)
        {
            if (!string.IsNullOrEmpty(input.SortBy))
            {
                switch (input.SortBy.ToLower())
                {
                    case "id":
                        return input.IsAscending ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id);
                    case "titlevi":
                        return input.IsAscending ? query.OrderBy(x => x.TitleVi) : query.OrderByDescending(x => x.TitleVi);
                    case "titleen":
                        return input.IsAscending ? query.OrderBy(x => x.TitleEn) : query.OrderByDescending(x => x.TitleEn);
                    case "timeposted":
                        return input.IsAscending ? query.OrderBy(x => x.TimePosted) : query.OrderByDescending(x => x.TimePosted);
                    case "status":
                        return input.IsAscending ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status);
                    case "isoutstanding":
                        return input.IsAscending ? query.OrderBy(x => x.IsOutstanding) : query.OrderByDescending(x => x.IsOutstanding);
                    case "category":
                    case "categoryvi":
                        return input.IsAscending ? query.OrderBy(x => x.NotificationCategory.TitleVi) : query.OrderByDescending(x => x.NotificationCategory.TitleVi);
                    case "categoryen":
                        return input.IsAscending ? query.OrderBy(x => x.NotificationCategory.TitleEn) : query.OrderByDescending(x => x.NotificationCategory.TitleEn);
                    default:
                        return query.OrderByDescending(x => x.TimePosted); // default sort
                }
            }
            else
            {
                return query.OrderByDescending(x => x.TimePosted); // default sort
            }
        }

        public async Task<NotificationDto> Create(CreateNotificationDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: Creating notification with all data in one atomic operation");

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Step 1: Validate input
                    if (string.IsNullOrWhiteSpace(input.TitleVi) || string.IsNullOrWhiteSpace(input.ContentVi))
                    {
                        throw new ArgumentException("Tiêu đề và nội dung là bắt buộc.");
                    }

                    // Validate title length
                    if (input.TitleVi.Length > 300)
                    {
                        throw new UserFriendlyException(ErrorCode.TitleTooLong);
                    }
                    if (!string.IsNullOrEmpty(input.TitleEn) && input.TitleEn.Length > 300)
                    {
                        throw new UserFriendlyException(ErrorCode.TitleTooLong);
                    }

                    // Validate description length
                    if (!string.IsNullOrEmpty(input.DescriptionVi) && input.DescriptionVi.Length > 700)
                    {
                        throw new UserFriendlyException(ErrorCode.DescriptionTooLong);
                    }
                    if (!string.IsNullOrEmpty(input.DescriptionEn) && input.DescriptionEn.Length > 700)
                    {
                        throw new UserFriendlyException(ErrorCode.DescriptionTooLong);
                    }

                    if (input.TimePosted > DateTime.Now)
                    {
                        throw new ArgumentException("Thời gian đăng bài không phù hợp.");
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var parseId) ? parseId : 0;

                    // Step 2: Validate category exists
                    var category = await _dbContext.NotificationCategories
                        .Where(c => c.Id == input.NotificationCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
                    }

                    // Step 3: Check for duplicate titles
                    var titleViExists = await _dbContext.Notifications.AnyAsync(n => n.TitleVi == input.TitleVi && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationTitleViExists);
                    }

                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.Notifications.AnyAsync(n => n.TitleEn == input.TitleEn && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.NotificationTitleEnExists);
                        }
                    }

                    // Step 4: Sanitize content
                    var sanitizedContentVi = SanitizeContent(input.ContentVi);
                    var sanitizedContentEn = SanitizeContent(input.ContentEn ?? string.Empty);

                    // Step 5: Create notification entity
                    var newNotification = new Notification
                    {
                        SlugVi = input.SlugVi,
                        SlugEn = input.SlugEn,
                        TitleVi = input.TitleVi.Trim(),
                        TitleEn = input.TitleEn?.Trim() ?? string.Empty,
                        DescriptionVi = input.DescriptionVi?.Trim() ?? string.Empty,
                        DescriptionEn = input.DescriptionEn?.Trim() ?? string.Empty,
                        ContentVi = sanitizedContentVi,
                        ContentEn = sanitizedContentEn,
                        TimePosted = input.TimePosted,
                        Status = input.Status,
                        IsOutstanding = input.IsOutstanding,
                        NotificationCategoryId = input.NotificationCategoryId,
                        CreatedBy = userId,
                        CreatedDate = DateTime.Now,
                        Deleted = false
                    };

                    _dbContext.Notifications.Add(newNotification);
                    await _dbContext.SaveChangesAsync();

                    // Step 6: Smart content processing - extract unique attachment IDs first
                    var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(newNotification.ContentVi, newNotification.ContentEn);
                    
                    // Associate content attachments first
                    if (contentAttachmentIds.Any())
                    {
                        await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.Notification, newNotification.Id, isFeaturedImage: false, isContentImage: true);
                    }
                    
                    // Process both content - now attachments are permanent
                    var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(newNotification.ContentVi, ObjectType.Notification, newNotification.Id);
                    var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(newNotification.ContentEn, ObjectType.Notification, newNotification.Id);
                    
                    // Update content with processed paths
                    newNotification.ContentVi = processedContentVi;
                    newNotification.ContentEn = processedContentEn;

                    // Step 7: Finalize gallery attachments (IsPrimary = false) - exclude content attachments
                    if (input.AttachmentIds != null && input.AttachmentIds.Any())
                    {
                        try
                        {
                            // Gallery attachments = attachmentIds - contentAttachmentIds (to avoid duplicates)
                            var galleryAttachmentIds = input.AttachmentIds.Except(contentAttachmentIds).ToList();
                            if (galleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(galleryAttachmentIds, ObjectType.Notification, newNotification.Id, isFeaturedImage: false, isContentImage: false);
                                _logger.LogInformation($"Finalized {galleryAttachmentIds.Count} gallery attachments for notification ID: {newNotification.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing gallery attachments for notification ID: {newNotification.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 8: Handle featured image (IsPrimary = true)
                    if (input.FeaturedImageId.HasValue)
                    {
                        try
                        {
                            await _attachmentService.AssociateAttachmentsAsync(new List<int> { input.FeaturedImageId.Value }, ObjectType.Notification, newNotification.Id, isFeaturedImage: true, isContentImage: false);
                            
                            // ImageUrl will be set automatically by AttachmentService to /uploads/images/yyyy/MM/filename.ext
                            // No need to override it here
                            
                            _logger.LogInformation($"Finalized featured image {input.FeaturedImageId.Value} for notification ID: {newNotification.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing featured image for notification ID: {newNotification.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 9: Save all changes and commit transaction
                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("NOTIFICATION_CREATE", "Tạo thông báo với file đính kèm", newNotification.TitleVi, "Info");
                    
                    await transaction.CommitAsync();

                    // Step 10: Return NotificationDto
                    var response = new NotificationDto
                    {
                        Id = newNotification.Id,
                        SlugVi = newNotification.SlugVi,
                        SlugEn = newNotification.SlugEn,
                        TitleVi = newNotification.TitleVi,
                        TitleEn = newNotification.TitleEn,
                        DescriptionVi = newNotification.DescriptionVi,
                        DescriptionEn = newNotification.DescriptionEn,
                        NotificationCategoryId = newNotification.NotificationCategoryId,
                        TimePosted = newNotification.TimePosted,
                        Status = newNotification.Status,
                        IsOutstanding = newNotification.IsOutstanding
                    };

                    _logger.LogInformation($"Successfully created notification. NotificationId: {newNotification.Id}");
                    return response;
                }
                catch (UserFriendlyException)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error creating notification with attachments: {ex.Message}");
                    throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
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
                    var notification = await _dbContext.Notifications
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.NotificationNotFound);

                    // Delete all associated files (featured, album, attachments)
                    await DeleteAssociatedFilesAsync(notification.Id);

                    // Delete WYSIWYG files
                    await _wysiwygFileProcessor.DeleteFilesAsync(ObjectType.Notification, notification.Id);

                    notification.Deleted = true;
                    await _dbContext.SaveChangesAsync();

                    // Log activity
                    await _activityLogService.LogAsync("NOTIFICATION_DELETE", "Xóa thông báo", notification.TitleVi, "Info");

                    await transaction.CommitAsync();

                    _logger.LogInformation($"Successfully deleted notification ID: {id} and all associated files");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error deleting notification with id = {id}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Delete all files associated with a notification article
        /// </summary>
        private async Task DeleteAssociatedFilesAsync(int notificationId)
        {
            try
            {
                // Get all files associated with this notification
                var associatedFiles = await _dbContext.Attachments
                    .Where(f => f.ObjectType == ObjectType.Notification && f.ObjectId == notificationId && !f.Deleted)
                    .ToListAsync();

                if (associatedFiles.Any())
                {
                    _logger.LogInformation($"Found {associatedFiles.Count} files to delete for notification ID: {notificationId}");

                    foreach (var file in associatedFiles)
                    {
                        try
                        {
                            // Delete physical file
                            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "AttechServer", "Uploads", file.FilePath);
                            if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                                _logger.LogInformation($"Deleted physical file: {file.FilePath}");
                            }

                            // Mark file as deleted in database
                            file.Deleted = true;
                            file.ModifiedDate = DateTime.Now;
                            file.ModifiedBy = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var userIdValue) ? userIdValue : 0;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete file {file.FilePath}. Continuing with other files.");
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting associated files for notification ID: {notificationId}");
                throw;
            }
        }

        public async Task<PagingResult<NotificationDto>> FindAll(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            var baseQuery = _dbContext.Notifications.AsNoTracking()
                .Where(n => !n.Deleted); 

            // Filter by category ID
            if (input.CategoryId.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.NotificationCategoryId == input.CategoryId.Value);
            }

            // Filter by status
            if (input.Status.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.Status == input.Status.Value);
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.TimePosted <= input.DateTo.Value);
            }

            // Filter by outstanding
            if (input.IsOutstanding.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.IsOutstanding == input.IsOutstanding.Value);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(input.Keyword))
            {
                baseQuery = baseQuery.Where(n =>
                    n.TitleVi.Contains(input.Keyword) ||
                    n.DescriptionVi.Contains(input.Keyword) ||
                    n.ContentVi.Contains(input.Keyword) ||
                    n.TitleEn.Contains(input.Keyword) ||
                    n.DescriptionEn.Contains(input.Keyword) ||
                    n.ContentEn.Contains(input.Keyword));
            }

            var totalItems = await baseQuery.CountAsync();

            // Apply sorting
            var query = ApplySorting(baseQuery, input);

            var pagedItems = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NotificationDto>
            {
                TotalItems = totalItems,
                Items = pagedItems
            };
        }

        public async Task<PagingResult<NotificationDto>> FindAllByCategoryId(PagingRequestBaseDto input, int categoryId)
        {
            _logger.LogInformation($"{nameof(FindAllByCategoryId)}: input = {JsonSerializer.Serialize(input)}, categoryId = {categoryId}");

            // Kiểm tra danh mục tồn tại
            var exists = await _dbContext.NotificationCategories
                .AnyAsync(c => c.Id == categoryId && !c.Deleted);
            if (!exists)
                throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);

            // Build query
            var baseQuery = _dbContext.Notifications.AsNoTracking()
                .Where(n => !n.Deleted && n.NotificationCategoryId == categoryId);

            // Tìm kiếm
            if (!string.IsNullOrEmpty(input.Keyword))
            {
                baseQuery = baseQuery.Where(n =>
                    n.TitleVi.Contains(input.Keyword) ||
                    n.DescriptionVi.Contains(input.Keyword) ||
                    n.ContentVi.Contains(input.Keyword) ||
                    n.TitleEn.Contains(input.Keyword) ||
                    n.DescriptionEn.Contains(input.Keyword) ||
                    n.ContentEn.Contains(input.Keyword));
            }

            var totalItems = await baseQuery.CountAsync();

            // Phân trang và sắp xếp
            var items = await baseQuery
                .OrderByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NotificationDto>
            {
                TotalItems = totalItems,
                Items = items
            };
        }

        public async Task<PagingResult<NotificationDto>> FindAllByCategorySlug(PagingRequestBaseDto input, string slug)
        {
            // 1. Tìm category theo slug
            var category = await _dbContext.NotificationCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SlugVi == slug && !c.Deleted);
            if (category == null)
                throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);

            // 2. Delegate về hàm cũ để xử lý paging
            return await FindAllByCategoryId(input, category.Id);
        }

        public async Task<DetailNotificationDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var notification = await _dbContext.Notifications
                .Where(n => n.Id == id && !n.Deleted)
                .Select(n => new DetailNotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    TitleVi = n.TitleVi,
                    DescriptionVi = n.DescriptionVi,
                    ContentVi = n.ContentVi,
                    SlugEn = n.SlugEn,
                    TitleEn = n.TitleEn,
                    DescriptionEn = n.DescriptionEn,
                    ContentEn = n.ContentEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (notification == null)
            {
                throw new UserFriendlyException(ErrorCode.NotificationNotFound);
            }

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Notification && a.ObjectId == id && !a.Deleted)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FilePath = a.FilePath,
                    Url = a.Url,
                    OriginalFileName = a.OriginalFileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    ObjectType = a.ObjectType,
                    ObjectId = a.ObjectId,
                    RelationType = a.RelationType,
                    IsPrimary = a.IsPrimary,
                    IsContentImage = a.IsContentImage,
                    IsTemporary = a.IsTemporary,
                    CreatedDate = a.CreatedDate
                })
                .ToListAsync();

            // Group attachments by type
            notification.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return notification;
        }

        public async Task<DetailNotificationDto> FindBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlug)}: slug = {slug}");
            var notification = await _dbContext.Notifications
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted) 
                .Select(n => new DetailNotificationDto
                {
                    Id = n.Id,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    ContentVi = n.ContentVi,
                    ContentEn = n.ContentEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (notification == null)
                throw new UserFriendlyException(ErrorCode.NotificationNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Notification && a.ObjectId == notification.Id && !a.Deleted)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FilePath = a.FilePath,
                    Url = a.Url,
                    OriginalFileName = a.OriginalFileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    ObjectType = a.ObjectType,
                    ObjectId = a.ObjectId,
                    RelationType = a.RelationType,
                    IsPrimary = a.IsPrimary,
                    IsContentImage = a.IsContentImage,
                    IsTemporary = a.IsTemporary,
                    CreatedDate = a.CreatedDate
                })
                .ToListAsync();

            // Group attachments by type
            notification.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return notification;
        }

        public async Task<NotificationDto> Update(int id, UpdateNotificationDto input)
        {
            _logger.LogInformation($"{nameof(Update)}: ID = {id}, input = {JsonSerializer.Serialize(input)}");
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Validate input
                    if (string.IsNullOrWhiteSpace(input.TitleVi) || string.IsNullOrWhiteSpace(input.ContentVi))
                    {
                        throw new ArgumentException("Tiêu đề và nội dung là bắt buộc.");
                    }

                    // Validate title length
                    if (input.TitleVi.Length > 300)
                    {
                        throw new UserFriendlyException(ErrorCode.TitleTooLong);
                    }
                    if (!string.IsNullOrEmpty(input.TitleEn) && input.TitleEn.Length > 300)
                    {
                        throw new UserFriendlyException(ErrorCode.TitleTooLong);
                    }

                    // Validate description length
                    if (!string.IsNullOrEmpty(input.DescriptionVi) && input.DescriptionVi.Length > 700)
                    {
                        throw new UserFriendlyException(ErrorCode.DescriptionTooLong);
                    }
                    if (!string.IsNullOrEmpty(input.DescriptionEn) && input.DescriptionEn.Length > 700)
                    {
                        throw new UserFriendlyException(ErrorCode.DescriptionTooLong);
                    }

                    if (input.TimePosted > DateTime.Now)
                    {
                        throw new ArgumentException("Thời gian đăng bài không phù hợp.");
                    }

                    var userId = int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value, out var parseId) ? parseId : 0;

                    // Kiểm tra notification tồn tại
                    var notification = await _dbContext.Notifications
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.NotificationNotFound);

                    // Check for duplicate titles (excluding current record)
                    var titleViExists = await _dbContext.Notifications.AnyAsync(n => n.TitleVi == input.TitleVi && n.Id != id && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationTitleViExists);
                    }

                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.Notifications.AnyAsync(n => n.TitleEn == input.TitleEn && n.Id != id && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.NotificationTitleEnExists);
                        }
                    }

                    // Kiểm tra danh mục
                    var category = await _dbContext.NotificationCategories
                        .Where(c => c.Id == input.NotificationCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NotificationCategoryNotFound);
                    }

                    // Update basic fields
                    notification.TitleVi = input.TitleVi;
                    notification.TitleEn = input.TitleEn;
                    notification.SlugVi = input.SlugVi;
                    notification.SlugEn = input.SlugEn;
                    notification.DescriptionVi = input.DescriptionVi;
                    notification.DescriptionEn = input.DescriptionEn;
                    notification.ContentVi = input.ContentVi;
                    notification.ContentEn = input.ContentEn;
                    notification.NotificationCategoryId = input.NotificationCategoryId;
                    notification.TimePosted = input.TimePosted;
                    notification.Status = input.Status;
                    notification.IsOutstanding = input.IsOutstanding;
                    notification.ModifiedBy = userId;

                    // Simple attachment update logic - compare current vs desired state
                    var currentGalleryAttachmentIds = await _attachmentService.GetCurrentGalleryAttachmentIdsAsync(ObjectType.Notification, notification.Id);
                    var currentFeaturedImageId = await _attachmentService.GetCurrentFeaturedImageIdAsync(ObjectType.Notification, notification.Id);
                    
                    var desiredGalleryAttachmentIds = (input.AttachmentIds ?? new List<int>()).OrderBy(x => x).ToList();
                    var desiredFeaturedImageId = input.FeaturedImageId;
                    
                    // Check if gallery attachments changed
                    bool galleryChanged = !currentGalleryAttachmentIds.SequenceEqual(desiredGalleryAttachmentIds);
                    bool featuredImageChanged = currentFeaturedImageId != desiredFeaturedImageId;
                    
                    // Check if content has new temp files
                    bool hasNewContentFiles = _wysiwygFileProcessor.HasTempFilesInContent(input.ContentVi) || 
                                             _wysiwygFileProcessor.HasTempFilesInContent(input.ContentEn ?? string.Empty);
                    
                    if (galleryChanged || featuredImageChanged || hasNewContentFiles)
                    {
                        // Something changed → Soft delete ALL old attachments and recreate
                        await _attachmentService.SoftDeleteEntityAttachmentsAsync(ObjectType.Notification, notification.Id);
                        
                        // Process content attachments first - extract unique IDs
                        var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(notification.ContentVi, notification.ContentEn);
                        if (contentAttachmentIds.Any())
                        {
                            await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.Notification, notification.Id, isFeaturedImage: false, isContentImage: true);
                        }
                        
                        // Process both content - now attachments are permanent
                        var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(notification.ContentVi, ObjectType.Notification, notification.Id);
                        var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(notification.ContentEn, ObjectType.Notification, notification.Id);
                        
                        // Update content with processed paths
                        notification.ContentVi = processedContentVi;
                        notification.ContentEn = processedContentEn;
                        
                        // Handle gallery attachments - exclude content attachments
                        if (desiredGalleryAttachmentIds.Any())
                        {
                            // Gallery attachments = desiredGalleryAttachmentIds - contentAttachmentIds (to avoid duplicates)
                            var pureGalleryAttachmentIds = desiredGalleryAttachmentIds.Except(contentAttachmentIds).ToList();
                            if (pureGalleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(pureGalleryAttachmentIds, ObjectType.Notification, notification.Id, isFeaturedImage: false, isContentImage: false);
                                _logger.LogInformation($"Updated {pureGalleryAttachmentIds.Count} gallery attachments for notification ID: {notification.Id}");
                            }
                        }
                        
                        // Handle featured image
                        if (desiredFeaturedImageId.HasValue)
                        {
                            try
                            {
                                await _attachmentService.AssociateAttachmentsAsync(new List<int> { desiredFeaturedImageId.Value }, ObjectType.Notification, notification.Id, isFeaturedImage: true, isContentImage: false);
                                _logger.LogInformation($"Updated featured image {desiredFeaturedImageId.Value} for notification ID: {notification.Id}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error finalizing featured image for notification ID: {notification.Id}");
                                throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                            }
                        }
                        else
                        {
                            // Clear featured image if not provided
                            notification.FeaturedImageId = null;
                            notification.ImageUrl = string.Empty;
                        }
                    }
                    // If nothing changed → keep everything as is


                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("NOTIFICATION_UPDATE", "Cập nhật thông báo", notification.TitleVi, "Info");

                    await transaction.CommitAsync();

                    return new NotificationDto
                    {
                        Id = notification.Id,
                        TitleVi = notification.TitleVi,
                        TitleEn = notification.TitleEn,
                        SlugVi = notification.SlugVi,
                        SlugEn = notification.SlugEn,
                        DescriptionVi = notification.DescriptionVi,
                        DescriptionEn = notification.DescriptionEn,
                        TimePosted = notification.TimePosted,
                        Status = notification.Status,
                        NotificationCategoryId = notification.NotificationCategoryId,
                        NotificationCategoryTitleVi = category.TitleVi,
                        NotificationCategoryTitleEn = category.TitleEn,
                        NotificationCategorySlugVi = category.SlugVi,
                        NotificationCategorySlugEn = category.SlugEn,
                        IsOutstanding = notification.IsOutstanding,
                        ImageUrl = notification.ImageUrl
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error updating notification with id = {id}");
                    throw;
                }
                finally
                {
                    // Temp files will be cleaned by background service
                }
            }
        }

        private string SanitizeContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;

            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Add("img");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");
            sanitizer.AllowedAttributes.Add("class");

            return sanitizer.Sanitize(content);
        }

        public async Task<PagingResult<NotificationDto>> FindAllForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAllForClient)}: Getting published notifications for client");
            
            var query = _dbContext.Notifications
                .Include(n => n.NotificationCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE);

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var notifications = await query
                .OrderByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NotificationDto>
            {
                Items = notifications,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<DetailNotificationDto> FindBySlugForClient(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlugForClient)}: slug = {slug}");
            var notification = await _dbContext.Notifications
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted && n.Status == CommonStatus.ACTIVE)
                .Select(n => new DetailNotificationDto
                {
                    Id = n.Id,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    ContentVi = n.ContentVi,
                    ContentEn = n.ContentEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (notification == null)
                throw new UserFriendlyException(ErrorCode.NotificationNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Notification && a.ObjectId == notification.Id && !a.Deleted)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FilePath = a.FilePath,
                    Url = a.Url,
                    OriginalFileName = a.OriginalFileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    ObjectType = a.ObjectType,
                    ObjectId = a.ObjectId,
                    RelationType = a.RelationType,
                    IsPrimary = a.IsPrimary,
                    IsContentImage = a.IsContentImage,
                    IsTemporary = a.IsTemporary,
                    CreatedDate = a.CreatedDate
                })
                .ToListAsync();

            notification.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return notification;
        }

        public async Task<PagingResult<NotificationDto>> FindAllByCategorySlugForClient(PagingRequestBaseDto input, string slug)
        {
            _logger.LogInformation($"{nameof(FindAllByCategorySlugForClient)}: slug = {slug}");
            
            var query = _dbContext.Notifications
                .Include(n => n.NotificationCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE &&
                           (n.NotificationCategory.SlugVi == slug || n.NotificationCategory.SlugEn == slug));

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var notifications = await query
                .OrderByDescending(n => n.IsOutstanding)
                .ThenByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NotificationDto>
            {
                Items = notifications,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<PagingResult<NotificationDto>> SearchForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(SearchForClient)}: keyword = {input.Keyword}");
            
            var query = _dbContext.Notifications
                .Include(n => n.NotificationCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword) ||
                                       n.ContentVi.Contains(input.Keyword) ||
                                       n.ContentEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var notifications = await query
                .OrderByDescending(n => n.IsOutstanding)
                .ThenByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NotificationCategoryId = n.NotificationCategoryId,
                    NotificationCategoryTitleVi = n.NotificationCategory.TitleVi,
                    NotificationCategoryTitleEn = n.NotificationCategory.TitleEn,
                    NotificationCategorySlugVi = n.NotificationCategory.SlugVi,
                    NotificationCategorySlugEn = n.NotificationCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NotificationDto>
            {
                Items = notifications,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }
    }
}
