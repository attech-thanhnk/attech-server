using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.News;
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
    public class NewsService : INewsService
    {
        private const int MAX_CONTENT_LENGTH = 100000; // 100KB
        private readonly ILogger<NewsService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWysiwygFileProcessor _wysiwygFileProcessor;
        private readonly IActivityLogService _activityLogService;
        private readonly IAttachmentService _attachmentService;

        public NewsService(ApplicationDbContext dbContext, ILogger<NewsService> logger, IHttpContextAccessor httpContextAccessor, IWysiwygFileProcessor wysiwygFileProcessor, IActivityLogService activityLogService, IAttachmentService attachmentService)
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

        private IQueryable<News> ApplySorting(IQueryable<News> query, PagingRequestBaseDto input)
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
                        return input.IsAscending ? query.OrderBy(x => x.NewsCategory.TitleVi) : query.OrderByDescending(x => x.NewsCategory.TitleVi);
                    case "categoryen":
                        return input.IsAscending ? query.OrderBy(x => x.NewsCategory.TitleEn) : query.OrderByDescending(x => x.NewsCategory.TitleEn);
                    default:
                        return query.OrderByDescending(x => x.TimePosted); // default sort
                }
            }
            else
            {
                return query.OrderByDescending(x => x.TimePosted); // default sort
            }
        }

        public async Task<NewsDto> Create(CreateNewsDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: Creating news with all data in one atomic operation");
            
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
                    var category = await _dbContext.NewsCategories
                        .Where(c => c.Id == input.NewsCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                    }

                    // Step 3: Check for duplicate titles
                    var titleViExists = await _dbContext.News.AnyAsync(n => n.TitleVi == input.TitleVi && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsTitleViExists);
                    }
                    
                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.News.AnyAsync(n => n.TitleEn == input.TitleEn && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.NewsTitleEnExists);
                        }
                    }

                    // Step 4: Sanitize content
                    var sanitizedContentVi = SanitizeContent(input.ContentVi);
                    var sanitizedContentEn = SanitizeContent(input.ContentEn ?? string.Empty);

                    // Step 5: Create news entity
                    var newNews = new News
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
                        NewsCategoryId = input.NewsCategoryId,
                        CreatedBy = userId,
                        CreatedDate = DateTime.Now,
                        Deleted = false
                    };

                    _dbContext.News.Add(newNews);
                    await _dbContext.SaveChangesAsync();

                    // Step 6: Smart content processing - extract unique attachment IDs first
                    var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(newNews.ContentVi, newNews.ContentEn);
                    
                    // Associate content attachments first
                    if (contentAttachmentIds.Any())
                    {
                        await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.News, newNews.Id, isFeaturedImage: false, isContentImage: true);
                    }
                    
                    // Process both content - now attachments are permanent
                    var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(newNews.ContentVi, ObjectType.News, newNews.Id);
                    var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(newNews.ContentEn, ObjectType.News, newNews.Id);
                    
                    // Update content with processed paths
                    newNews.ContentVi = processedContentVi;
                    newNews.ContentEn = processedContentEn;

                    // Step 7: Finalize gallery attachments (IsPrimary = false) - exclude content attachments
                    if (input.AttachmentIds != null && input.AttachmentIds.Any())
                    {
                        try
                        {
                            // Gallery attachments = attachmentIds - contentAttachmentIds (to avoid duplicates)
                            var galleryAttachmentIds = input.AttachmentIds.Except(contentAttachmentIds).ToList();
                            if (galleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(galleryAttachmentIds, ObjectType.News, newNews.Id, isFeaturedImage: false, isContentImage: false);
                                _logger.LogInformation($"Finalized {galleryAttachmentIds.Count} gallery attachments for news ID: {newNews.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing gallery attachments for news ID: {newNews.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 8: Handle featured image (IsPrimary = true)
                    if (input.FeaturedImageId.HasValue)
                    {
                        try
                        {
                            await _attachmentService.AssociateAttachmentsAsync(new List<int> { input.FeaturedImageId.Value }, ObjectType.News, newNews.Id, isFeaturedImage: true, isContentImage: false);
                            
                            // ImageUrl will be set automatically by AttachmentService to /uploads/images/yyyy/MM/filename.ext
                            // No need to override it here
                            
                            _logger.LogInformation($"Finalized featured image {input.FeaturedImageId.Value} for news ID: {newNews.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing featured image for news ID: {newNews.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 9: Save all changes and commit transaction
                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("NEWS_CREATE", "Tạo tin tức với file đính kèm", newNews.TitleVi, "Info");
                    
                    await transaction.CommitAsync();

                    // Step 10: Return NewsDto
                    var response = new NewsDto
                    {
                        Id = newNews.Id,
                        SlugVi = newNews.SlugVi,
                        SlugEn = newNews.SlugEn,
                        TitleVi = newNews.TitleVi,
                        TitleEn = newNews.TitleEn,
                        DescriptionVi = newNews.DescriptionVi,
                        DescriptionEn = newNews.DescriptionEn,
                        NewsCategoryId = newNews.NewsCategoryId,
                        TimePosted = newNews.TimePosted,
                        Status = newNews.Status,
                        IsOutstanding = newNews.IsOutstanding
                    };

                    _logger.LogInformation($"Successfully created news. NewsId: {newNews.Id}");
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
                    _logger.LogError(ex, $"Error creating news with attachments: {ex.Message}");
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
                    var news = await _dbContext.News
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.NewsNotFound);

                    // Delete all associated files (featured, album, attachments)
                    await DeleteAssociatedFilesAsync(news.Id);

                    // Delete WYSIWYG files
                    await _wysiwygFileProcessor.DeleteFilesAsync(ObjectType.News, news.Id);

                    news.Deleted = true;
                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("NEWS_DELETE", "Xóa tin tức", news.TitleVi, "Info");
                    
                    await transaction.CommitAsync();
                    
                    _logger.LogInformation($"Successfully deleted news ID: {id} and all associated files");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error deleting news with id = {id}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Delete all files associated with a news article
        /// </summary>
        private async Task DeleteAssociatedFilesAsync(int newsId)
        {
            try
            {
                // Get all files associated with this news
                var associatedFiles = await _dbContext.Attachments
                    .Where(f => f.ObjectType == ObjectType.News && f.ObjectId == newsId && !f.Deleted)
                    .ToListAsync();

                if (associatedFiles.Any())
                {
                    _logger.LogInformation($"Found {associatedFiles.Count} files to delete for news ID: {newsId}");

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
                _logger.LogError(ex, $"Error deleting associated files for news ID: {newsId}");
                throw;
            }
        }

        public async Task<PagingResult<NewsDto>> FindAll(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            var baseQuery = _dbContext.News.AsNoTracking()
                .Where(n => !n.Deleted && n.IsAlbum == false); // CHỈ lấy News thực, không lấy Albums

            // Filter by category ID
            if (input.CategoryId.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.NewsCategoryId == input.CategoryId.Value);
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
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                TotalItems = totalItems,
                Items = pagedItems
            };
        }

        public async Task<PagingResult<NewsDto>> FindAllByCategoryId(PagingRequestBaseDto input, int categoryId)
        {
            _logger.LogInformation($"{nameof(FindAllByCategoryId)}: input = {JsonSerializer.Serialize(input)}, categoryId = {categoryId}");

            // Kiểm tra danh mục tồn tại
            var exists = await _dbContext.NewsCategories
                .AnyAsync(c => c.Id == categoryId && !c.Deleted);
            if (!exists)
                throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);

            // Build query
            var baseQuery = _dbContext.News.AsNoTracking()
                .Where(n => !n.Deleted && n.IsAlbum == false && n.NewsCategoryId == categoryId); // CHỈ lấy News thực

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
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                TotalItems = totalItems,
                Items = items
            };
        }

        public async Task<PagingResult<NewsDto>> FindAllByCategorySlug(PagingRequestBaseDto input, string slug)
        {
            // 1. Tìm category theo slug
            var category = await _dbContext.NewsCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SlugVi == slug && !c.Deleted);
            if (category == null)
                throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);

            // 2. Delegate về hàm cũ để xử lý paging
            return await FindAllByCategoryId(input, category.Id);
        }

        public async Task<DetailNewsDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var news = await _dbContext.News
                .Where(n => n.Id == id && !n.Deleted)
                .Select(n => new DetailNewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (news == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsNotFound);
            }

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == id && !a.Deleted)
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
            news.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return news;
        }

        public async Task<DetailNewsDto> FindBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlug)}: slug = {slug}");
            var news = await _dbContext.News
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted && n.IsAlbum == false) // CHỈ lấy News thực
                .Select(n => new DetailNewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (news == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == news.Id && !a.Deleted)
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
            news.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return news;
        }

        public async Task<NewsDto> Update(int id, UpdateNewsDto input)
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

                    // Kiểm tra news tồn tại
                    var news = await _dbContext.News
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.NewsNotFound);

                    // Check for duplicate titles (excluding current record)
                    var titleViExists = await _dbContext.News.AnyAsync(n => n.TitleVi == input.TitleVi && n.Id != id && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsTitleViExists);
                    }
                    
                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.News.AnyAsync(n => n.TitleEn == input.TitleEn && n.Id != id && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.NewsTitleEnExists);
                        }
                    }

                    // Kiểm tra danh mục
                    var category = await _dbContext.NewsCategories
                        .Where(c => c.Id == input.NewsCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                    }

                    // Update basic fields
                    news.TitleVi = input.TitleVi;
                    news.TitleEn = input.TitleEn;
                    news.SlugVi = input.SlugVi;
                    news.SlugEn = input.SlugEn;
                    news.DescriptionVi = input.DescriptionVi;
                    news.DescriptionEn = input.DescriptionEn;
                    news.ContentVi = input.ContentVi;
                    news.ContentEn = input.ContentEn;
                    news.NewsCategoryId = input.NewsCategoryId;
                    news.TimePosted = input.TimePosted;
                    news.Status = input.Status;
                    news.IsOutstanding = input.IsOutstanding;
                    news.ModifiedBy = userId;

                    // Simple attachment update logic - compare current vs desired state
                    var currentGalleryAttachmentIds = await _attachmentService.GetCurrentGalleryAttachmentIdsAsync(ObjectType.News, news.Id);
                    var currentFeaturedImageId = await _attachmentService.GetCurrentFeaturedImageIdAsync(ObjectType.News, news.Id);
                    
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
                        await _attachmentService.SoftDeleteEntityAttachmentsAsync(ObjectType.News, news.Id);
                        
                        // Process content attachments first - extract unique IDs
                        var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(news.ContentVi, news.ContentEn);
                        if (contentAttachmentIds.Any())
                        {
                            await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.News, news.Id, isFeaturedImage: false, isContentImage: true);
                        }
                        
                        // Process both content - now attachments are permanent
                        var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(news.ContentVi, ObjectType.News, news.Id);
                        var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(news.ContentEn, ObjectType.News, news.Id);
                        
                        // Update content with processed paths
                        news.ContentVi = processedContentVi;
                        news.ContentEn = processedContentEn;
                        
                        // Handle gallery attachments - exclude content attachments
                        if (desiredGalleryAttachmentIds.Any())
                        {
                            // Gallery attachments = desiredGalleryAttachmentIds - contentAttachmentIds (to avoid duplicates)
                            var pureGalleryAttachmentIds = desiredGalleryAttachmentIds.Except(contentAttachmentIds).ToList();
                            if (pureGalleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(pureGalleryAttachmentIds, ObjectType.News, news.Id, isFeaturedImage: false, isContentImage: false);
                            }
                        }
                        
                        // Handle featured image
                        if (desiredFeaturedImageId.HasValue)
                        {
                            await _attachmentService.AssociateAttachmentsAsync(new List<int> { desiredFeaturedImageId.Value }, ObjectType.News, news.Id, isFeaturedImage: true, isContentImage: false);
                        }
                        else
                        {
                            // Clear featured image if not provided
                            news.FeaturedImageId = null;
                            news.ImageUrl = string.Empty;
                        }
                    }
                    // If nothing changed → keep everything as is


                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("NEWS_UPDATE", "Cập nhật tin tức", news.TitleVi, "Info");
                    
                    await transaction.CommitAsync();

                    return new NewsDto
                    {
                        Id = news.Id,
                        TitleVi = news.TitleVi,
                        TitleEn = news.TitleEn,
                        SlugVi = news.SlugVi,
                        SlugEn = news.SlugEn,
                        DescriptionVi = news.DescriptionVi,
                        DescriptionEn = news.DescriptionEn,
                        TimePosted = news.TimePosted,
                        Status = news.Status,
                        NewsCategoryId = news.NewsCategoryId,
                        NewsCategoryTitleVi = category.TitleVi,
                        NewsCategoryTitleEn = category.TitleEn,
                        NewsCategorySlugVi = category.SlugVi,
                        NewsCategorySlugEn = category.SlugEn,
                        IsOutstanding = news.IsOutstanding,
                        ImageUrl = news.ImageUrl
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error updating news with id = {id}");
                    throw;
                }
                finally
                {
                    // Temp files will be cleaned by background service
                }
            }
        }

        public async Task<NewsDto> CreateAlbum(CreateAlbumDto input)
        {
            _logger.LogInformation($"{nameof(CreateAlbum)}: Creating album {input.TitleVi}");

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("user_id")?.Value;
                var userId = int.TryParse(userIdClaim, out int parsedUserId) ? parsedUserId : 0;
                
                // Validate category exists for album
                var category = await _dbContext.NewsCategories
                    .Where(c => c.Id == input.NewsCategoryId && !c.Deleted)
                    .FirstOrDefaultAsync();
                if (category == null)
                {
                    throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                }
                
                // Create news with IsAlbum = true
                var newNews = new News
                {
                    SlugVi = SlugHelper.GenerateSlug(input.TitleVi),
                    SlugEn = SlugHelper.GenerateSlug(input.TitleEn),
                    TitleVi = input.TitleVi,
                    TitleEn = input.TitleEn,
                    DescriptionVi = "", // Empty for albums
                    DescriptionEn = "", // Empty for albums
                    ContentVi = "", // Empty for albums
                    ContentEn = "", // Empty for albums
                    NewsCategoryId = input.NewsCategoryId,
                    TimePosted = DateTime.Now,
                    Status = 1, // Active
                    IsOutstanding = false,
                    IsAlbum = true, // Mark as album
                    FeaturedImageId = input.FeaturedImageId, // Set featured image directly
                    CreatedDate = DateTime.Now,
                    CreatedBy = userId
                };

                _dbContext.News.Add(newNews);
                await _dbContext.SaveChangesAsync();

                // Associate featured image
                await _attachmentService.AssociateAttachmentsAsync(
                    new List<int> { input.FeaturedImageId }, 
                    ObjectType.News, 
                    newNews.Id, 
                    isFeaturedImage: true, 
                    isContentImage: false
                );
                _logger.LogInformation($"Associated featured image {input.FeaturedImageId} for album ID: {newNews.Id}");

                // Associate gallery images (if any)
                if (input.AttachmentIds != null && input.AttachmentIds.Any())
                {
                    await _attachmentService.AssociateAttachmentsAsync(
                        input.AttachmentIds, 
                        ObjectType.News, 
                        newNews.Id, 
                        isFeaturedImage: false, 
                        isContentImage: false
                    );
                    _logger.LogInformation($"Associated {input.AttachmentIds.Count} gallery images for album ID: {newNews.Id}");
                }

                await transaction.CommitAsync();

                // Return NewsDto
                return new NewsDto
                {
                    Id = newNews.Id,
                    SlugVi = newNews.SlugVi,
                    SlugEn = newNews.SlugEn,
                    TitleVi = newNews.TitleVi,
                    TitleEn = newNews.TitleEn,
                    DescriptionVi = newNews.DescriptionVi,
                    DescriptionEn = newNews.DescriptionEn,
                    NewsCategoryId = newNews.NewsCategoryId,
                    TimePosted = newNews.TimePosted,
                    Status = newNews.Status,
                    IsOutstanding = newNews.IsOutstanding
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating album: {input.TitleVi}");
                throw;
            }
        }

        public async Task<PagingResult<NewsDto>> GetAlbums(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(GetAlbums)}: Getting albums with pagination");

            var query = _dbContext.News
                .Where(n => !n.Deleted && n.IsAlbum == true)
                .Include(n => n.NewsCategory)
                .OrderByDescending(n => n.TimePosted);

            var totalItems = await query.CountAsync();
            
            var albums = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            // Load featured image URLs in batch (avoid N+1)
            var featuredImageIds = albums.Where(a => a.FeaturedImageId.HasValue)
                                         .Select(a => a.FeaturedImageId.Value)
                                         .ToList();
            
            if (featuredImageIds.Any())
            {
                var featuredImages = await _dbContext.Attachments
                    .Where(a => featuredImageIds.Contains(a.Id) && !a.Deleted)
                    .Select(a => new { a.Id, a.Url })
                    .ToListAsync();

                foreach (var album in albums)
                {
                    if (album.FeaturedImageId.HasValue)
                    {
                        var featuredImage = featuredImages.FirstOrDefault(f => f.Id == album.FeaturedImageId.Value);
                        if (featuredImage != null)
                        {
                            album.ImageUrl = featuredImage.Url;
                        }
                    }
                }
            }

            return new PagingResult<NewsDto>
            {
                Items = albums,
                TotalItems = totalItems,
            };
        }

        public async Task<DetailNewsDto> FindAlbumBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindAlbumBySlug)}: slug = {slug}");
            var album = await _dbContext.News
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted && n.IsAlbum == true) // CHỈ lấy Albums
                .Select(n => new DetailNewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (album == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            return album;
        }

        public async Task<NewsGalleryDto> GetGalleryBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(GetGalleryBySlug)}: slug = {slug}");
            
            // Tìm News theo slug
            var news = await _dbContext.News
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted)
                .Select(n => new 
                {
                    n.Id,
                    n.TitleVi,
                    n.TitleEn,
                    n.SlugVi,
                    n.SlugEn
                })
                .FirstOrDefaultAsync();

            if (news == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            // Lấy TẤT CẢ attachments của News này
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News 
                          && a.ObjectId == news.Id 
                          && !a.Deleted 
                          && !a.IsTemporary)
                .OrderBy(a => a.IsPrimary ? 0 : 1)
                .ThenBy(a => a.OrderIndex)
                .ThenBy(a => a.CreatedDate)
                .ToListAsync();

            return new NewsGalleryDto
            {
                Id = news.Id,
                TitleVi = news.TitleVi,
                TitleEn = news.TitleEn,
                SlugVi = news.SlugVi,
                SlugEn = news.SlugEn,
                FeaturedImage = null, // Không phân biệt featured
                GalleryImages = attachments.Select(a => new NewsImageDto
                {
                    Id = a.Id,
                    FilePath = a.Url,
                    OriginalFileName = a.OriginalFileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType
                }).ToList()
            };
        }

        public async Task<PagingResult<NewsDto>> FindAllForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAllForClient)}: Getting published news for client");
            
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE && n.IsAlbum == false);

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var news = await query
                .OrderByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<DetailNewsDto> FindBySlugForClient(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlugForClient)}: slug = {slug}");
            var news = await _dbContext.News
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted && n.Status == CommonStatus.ACTIVE && n.IsAlbum == false)
                .Select(n => new DetailNewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (news == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == news.Id && !a.Deleted)
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

            news.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return news;
        }

        public async Task<PagingResult<NewsDto>> FindAllByCategorySlugForClient(PagingRequestBaseDto input, string slug)
        {
            _logger.LogInformation($"{nameof(FindAllByCategorySlugForClient)}: slug = {slug}");
            
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE && n.IsAlbum == false &&
                           (n.NewsCategory.SlugVi == slug || n.NewsCategory.SlugEn == slug));

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var news = await query
                .OrderByDescending(n => n.IsOutstanding)
                .ThenByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<PagingResult<NewsDto>> SearchForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(SearchForClient)}: keyword = {input.Keyword}");
            
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE && n.IsAlbum == false);

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

            var news = await query
                .OrderByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<PagingResult<NewsDto>> FindOutstandingForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindOutstandingForClient)}: Getting outstanding published news for client");
            
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => !n.Deleted && n.Status == CommonStatus.ACTIVE && n.IsAlbum == false && n.IsOutstanding == true);

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(n => n.TitleVi.Contains(input.Keyword) || 
                                       n.TitleEn.Contains(input.Keyword) ||
                                       n.DescriptionVi.Contains(input.Keyword) ||
                                       n.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var news = await query
                .OrderByDescending(n => n.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
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

        public async Task<AlbumDetailDto> GetAlbumByIdForAdmin(int id)
        {
            var album = await _dbContext.News
                .Include(n => n.NewsCategory)
                .FirstOrDefaultAsync(n => n.Id == id && n.IsAlbum && !n.Deleted);

            if (album == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            // Load attachments (giống News)
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == album.Id && !a.Deleted)
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

            return new AlbumDetailDto
            {
                Id = album.Id,
                TitleVi = album.TitleVi,
                TitleEn = album.TitleEn,
                DescriptionVi = album.DescriptionVi,
                DescriptionEn = album.DescriptionEn,
                SlugVi = album.SlugVi,
                SlugEn = album.SlugEn,
                NewsCategoryId = album.NewsCategoryId,
                NewsCategoryName = album.NewsCategory?.TitleVi,
                Status = album.Status,
                IsOutstanding = album.IsOutstanding,
                FeaturedImageId = album.FeaturedImageId,
                ImageUrl = album.ImageUrl, // Legacy field
                Attachments = new AttachmentsGroupDto
                {
                    Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                    Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
                },
                CreatedDate = album.CreatedDate,
                ModifiedDate = album.ModifiedDate,
                CreatedBy = album.CreatedBy,
                ModifiedBy = album.ModifiedBy
            };
        }

        // Client Album methods
        public async Task<PagingResult<NewsDto>> GetPublishedAlbumsForClient(PagingRequestBaseDto input)
        {
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.Status == 1 && !n.Deleted)
                .OrderByDescending(n => n.TimePosted);

            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<DetailNewsDto> GetPublishedAlbumBySlugForClient(string slug)
        {
            var news = await _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.Status == 1 && !n.Deleted)
                .Where(n => n.SlugVi == slug || n.SlugEn == slug)
                .Select(n => new DetailNewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    ContentVi = n.ContentVi,
                    ContentEn = n.ContentEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (news == null)
                throw new UserFriendlyException(ErrorCode.NewsNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == news.Id && !a.Deleted)
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

            news.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return news;
        }

        public async Task<NewsGalleryDto> GetAlbumGalleryForClient(string slug)
        {
            return await GetGalleryBySlug(slug); // Reuse existing method
        }

        public async Task<PagingResult<NewsDto>> GetPublishedAlbumsByCategoryForClient(string categorySlug, PagingRequestBaseDto input)
        {
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.IsAlbum && n.Status == 1 && !n.Deleted)
                .Where(n => n.NewsCategory.SlugVi == categorySlug || n.NewsCategory.SlugEn == categorySlug)
                .OrderByDescending(n => n.TimePosted);

            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<PagingResult<NewsDto>> GetFeaturedAlbumsForClient(PagingRequestBaseDto input)
        {
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.IsAlbum && n.Status == 1 && n.IsOutstanding && !n.Deleted)
                .OrderByDescending(n => n.TimePosted);

            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
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
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    NewsCategorySlugVi = n.NewsCategory.SlugVi,
                    NewsCategorySlugEn = n.NewsCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<AlbumDetailDto> UpdateAlbum(int id, UpdateAlbumDto input)
        {
            _logger.LogInformation($"{nameof(UpdateAlbum)}: Updating album {id}");

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var album = await _dbContext.News
                        .Include(n => n.NewsCategory)
                        .FirstOrDefaultAsync(n => n.Id == id && n.IsAlbum && !n.Deleted);

                    if (album == null)
                        throw new UserFriendlyException(ErrorCode.NewsNotFound);

                    // Update basic info
                    album.TitleVi = input.TitleVi;
                    album.TitleEn = input.TitleEn;
                    album.DescriptionVi = input.DescriptionVi ?? "";
                    album.DescriptionEn = input.DescriptionEn ?? "";
                    album.ContentVi = input.DescriptionVi ?? ""; // Use description as content for albums
                    album.ContentEn = input.DescriptionEn ?? "";
                    album.NewsCategoryId = input.NewsCategoryId;
                    album.Status = input.Status;
                    album.IsOutstanding = input.IsOutstanding;
                    album.SlugVi = input.SlugVi;
                    album.SlugEn = input.SlugEn;
                    album.ModifiedDate = DateTime.Now;

                    // Update featured image
                    album.FeaturedImageId = input.FeaturedImageId;

                    // Associate featured image
                    await _attachmentService.AssociateAttachmentsAsync(
                        new List<int> { input.FeaturedImageId },
                        ObjectType.News,
                        album.Id,
                        isFeaturedImage: true,
                        isContentImage: false
                    );

                    // Handle gallery attachments if provided
                    if (input.AttachmentIds != null && input.AttachmentIds.Any())
                    {
                        await _attachmentService.AssociateAttachmentsAsync(
                            input.AttachmentIds,
                            ObjectType.News,
                            album.Id,
                            isFeaturedImage: false,
                            isContentImage: false
                        );
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Log activity
                    await _activityLogService.LogAsync("ALBUM_UPDATE", "Cập nhật album", album.TitleVi, "Info");

                    // Load and return updated album
                    // Load attachments (giống News)
                    var attachments = await _dbContext.Attachments
                        .Where(a => a.ObjectType == ObjectType.News && a.ObjectId == album.Id && !a.Deleted)
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

                    return new AlbumDetailDto
                    {
                        Id = album.Id,
                        TitleVi = album.TitleVi,
                        TitleEn = album.TitleEn,
                        DescriptionVi = album.DescriptionVi,
                        DescriptionEn = album.DescriptionEn,
                        SlugVi = album.SlugVi,
                        SlugEn = album.SlugEn,
                        NewsCategoryId = album.NewsCategoryId,
                        NewsCategoryName = album.NewsCategory?.TitleVi,
                        Status = album.Status,
                        IsOutstanding = album.IsOutstanding,
                        FeaturedImageId = album.FeaturedImageId,
                        ImageUrl = album.ImageUrl,
                        Attachments = new AttachmentsGroupDto
                        {
                            Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                            Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
                        },
                        CreatedDate = album.CreatedDate,
                        ModifiedDate = album.ModifiedDate,
                        CreatedBy = album.CreatedBy,
                        ModifiedBy = album.ModifiedBy
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating album {Id}", id);
                    throw;
                }
            }
        }

        #region Document Methods

        public async Task<NewsDto> CreateDocument(CreateDocumentDto input)
        {
            _logger.LogInformation($"{nameof(CreateDocument)}: Creating document {input.TitleVi}");
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("user_id")?.Value;
                var userId = int.TryParse(userIdClaim, out int parsedUserId) ? parsedUserId : 0;
                
                // Validate category exists for document
                var category = await _dbContext.NewsCategories
                    .Where(c => c.Id == input.NewsCategoryId && !c.Deleted)
                    .FirstOrDefaultAsync();
                if (category == null)
                {
                    throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                }
                
                // Create news with IsDocument = true
                var newNews = new News
                {
                    SlugVi = SlugHelper.GenerateSlug(input.TitleVi),
                    SlugEn = SlugHelper.GenerateSlug(!string.IsNullOrEmpty(input.TitleEn) ? input.TitleEn : input.TitleVi),
                    TitleVi = input.TitleVi,
                    TitleEn = !string.IsNullOrEmpty(input.TitleEn) ? input.TitleEn : input.TitleVi,
                    DescriptionVi = TruncateDescription(input.TitleVi),
                    DescriptionEn = TruncateDescription(!string.IsNullOrEmpty(input.TitleEn) ? input.TitleEn : input.TitleVi),
                    ContentVi = input.TitleVi,
                    ContentEn = !string.IsNullOrEmpty(input.TitleEn) ? input.TitleEn : input.TitleVi,
                    TimePosted = DateTime.Now,
                    Status = 0, // Draft by default
                    NewsCategoryId = input.NewsCategoryId,
                    IsOutstanding = false,
                    IsAlbum = false,
                    IsDocument = true,
                    FeaturedImageId = input.FeaturedImageId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                
                _dbContext.News.Add(newNews);
                await _dbContext.SaveChangesAsync();
                
                // Handle document attachments
                if (input.AttachmentIds?.Any() == true)
                {
                    var attachments = await _dbContext.Attachments
                        .Where(a => input.AttachmentIds.Contains(a.Id) && a.IsTemporary == true)
                        .ToListAsync();
                    
                    foreach (var attachment in attachments)
                    {
                        attachment.ObjectType = ObjectType.Document;
                        attachment.ObjectId = newNews.Id;
                        attachment.IsContentImage = false;
                        attachment.IsTemporary = false;
                    }
                }
                
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                
                await _activityLogService.LogAsync("Document", $"Tạo document: {newNews.TitleVi}");
                
                return new NewsDto
                {
                    Id = newNews.Id,
                    SlugVi = newNews.SlugVi,
                    SlugEn = newNews.SlugEn,
                    TitleVi = newNews.TitleVi,
                    TitleEn = newNews.TitleEn,
                    DescriptionVi = newNews.DescriptionVi,
                    DescriptionEn = newNews.DescriptionEn,
                    NewsCategoryId = newNews.NewsCategoryId,
                    NewsCategoryTitleVi = category.TitleVi,
                    NewsCategoryTitleEn = category.TitleEn,
                    TimePosted = newNews.TimePosted,
                    Status = newNews.Status,
                    IsOutstanding = newNews.IsOutstanding,
                    ImageUrl = newNews.ImageUrl,
                    FeaturedImageId = newNews.FeaturedImageId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating document: {input.TitleVi}");
                throw;
            }
        }

        public async Task<PagingResult<NewsDto>> GetDocuments(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(GetDocuments)}: Getting documents with pagination");
            var query = _dbContext.News
                .Where(n => !n.Deleted && n.IsDocument == true)
                .Include(n => n.NewsCategory)
                .OrderByDescending(n => n.TimePosted);
            var totalItems = await query.CountAsync();
            
            var documents = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();
            
            return new PagingResult<NewsDto>
            {
                Items = documents,
                TotalItems = totalItems,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<DocumentDetailDto> GetDocumentByIdForAdmin(int id)
        {
            _logger.LogInformation($"{nameof(GetDocumentByIdForAdmin)}: Getting document {id}");
            
            var document = await _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.Id == id && n.IsDocument && !n.Deleted)
                .FirstOrDefaultAsync();
            
            if (document == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsNotFound);
            }
            
            // Get document attachments
            var documents = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Document && a.ObjectId == id && !a.Deleted)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    OriginalFileName = a.OriginalFileName,
                    FilePath = a.FilePath,
                    Url = a.Url,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    ObjectType = a.ObjectType,
                    ObjectId = a.ObjectId,
                    CreatedDate = a.CreatedDate
                })
                .OrderBy(a => a.CreatedDate)
                .ToListAsync();
            
            // Get featured image if exists
            AttachmentDto? featuredImage = null;
            if (document.FeaturedImageId.HasValue)
            {
                featuredImage = await _dbContext.Attachments
                    .Where(a => a.Id == document.FeaturedImageId.Value && !a.Deleted)
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        OriginalFileName = a.OriginalFileName,
                        FilePath = a.FilePath,
                        Url = a.Url,
                        FileSize = a.FileSize,
                        ContentType = a.ContentType,
                        ObjectType = a.ObjectType,
                        ObjectId = a.ObjectId,
                        CreatedDate = a.CreatedDate
                    })
                    .FirstOrDefaultAsync();
            }
            
            return new DocumentDetailDto
            {
                Id = document.Id,
                SlugVi = document.SlugVi,
                SlugEn = document.SlugEn,
                TitleVi = document.TitleVi,
                TitleEn = document.TitleEn,
                DescriptionVi = document.DescriptionVi,
                DescriptionEn = document.DescriptionEn,
                TimePosted = document.TimePosted,
                Status = document.Status,
                NewsCategoryId = document.NewsCategoryId,
                NewsCategoryNameVi = document.NewsCategory.TitleVi,
                NewsCategoryNameEn = document.NewsCategory.TitleEn,
                ImageUrl = document.ImageUrl,
                FeaturedImageId = document.FeaturedImageId,
                FeaturedImage = featuredImage,
                Documents = documents,
                CreatedDate = document.CreatedDate,
                ModifiedDate = document.ModifiedDate
            };
        }

        public async Task<DocumentDetailDto> UpdateDocument(int id, UpdateDocumentDto input)
        {
            _logger.LogInformation($"{nameof(UpdateDocument)}: Updating document {id}");
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("user_id")?.Value;
                var userId = int.TryParse(userIdClaim, out int parsedUserId) ? parsedUserId : 0;
                
                var document = await _dbContext.News
                    .Where(n => n.Id == id && n.IsDocument && !n.Deleted)
                    .FirstOrDefaultAsync();
                
                if (document == null)
                {
                    throw new UserFriendlyException(ErrorCode.NewsNotFound);
                }
                
                // Validate category
                var category = await _dbContext.NewsCategories
                    .Where(c => c.Id == input.NewsCategoryId && !c.Deleted)
                    .FirstOrDefaultAsync();
                if (category == null)
                {
                    throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
                }
                
                // Update document fields
                document.TitleVi = input.TitleVi;
                document.TitleEn = input.TitleEn;
                document.SlugVi = SlugHelper.GenerateSlug(input.TitleVi);
                document.SlugEn = SlugHelper.GenerateSlug(input.TitleEn);
                document.DescriptionVi = TruncateDescription(input.TitleVi);
                document.DescriptionEn = TruncateDescription(input.TitleEn);
                document.ContentVi = input.TitleVi;
                document.ContentEn = input.TitleEn;
                document.NewsCategoryId = input.NewsCategoryId;
                document.Status = input.Status;
                document.FeaturedImageId = input.FeaturedImageId;
                document.ModifiedBy = userId;
                document.ModifiedDate = DateTime.Now;
                
                // Handle document attachments
                if (input.AttachmentIds != null)
                {
                    // Remove existing document attachments
                    var existingAttachments = await _dbContext.Attachments
                        .Where(a => a.ObjectType == ObjectType.Document && a.ObjectId == id)
                        .ToListAsync();
                    
                    foreach (var att in existingAttachments)
                    {
                        att.ObjectType = ObjectType.Temp;
                        att.ObjectId = null;
                        att.IsTemporary = true;
                    }
                    
                    // Add new attachments
                    if (input.AttachmentIds.Any())
                    {
                        var newAttachments = await _dbContext.Attachments
                            .Where(a => input.AttachmentIds.Contains(a.Id) && a.IsTemporary == true)
                            .ToListAsync();
                        
                        foreach (var attachment in newAttachments)
                        {
                            attachment.ObjectType = ObjectType.Document;
                            attachment.ObjectId = id;
                            attachment.IsContentImage = false;
                            attachment.IsTemporary = false;
                        }
                    }
                }
                
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                
                await _activityLogService.LogAsync("Document", $"Cập nhật document: {document.TitleVi}");
                
                return await GetDocumentByIdForAdmin(id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating document {id}");
                throw;
            }
        }

        public async Task<PagingResult<NewsDto>> GetPublishedDocumentsForClient(PagingRequestBaseDto input)
        {
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.IsDocument && n.Status == 1 && !n.Deleted)
                .OrderByDescending(n => n.TimePosted);
            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();
            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<DocumentDetailDto> GetPublishedDocumentBySlugForClient(string slug)
        {
            var document = await _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && n.IsDocument && n.Status == 1 && !n.Deleted)
                .FirstOrDefaultAsync();
            
            if (document == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsNotFound);
            }
            
            // Get document attachments
            var documents = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Document && a.ObjectId == document.Id && !a.Deleted)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    OriginalFileName = a.OriginalFileName,
                    FilePath = a.FilePath,
                    Url = a.Url,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    ObjectType = a.ObjectType,
                    ObjectId = a.ObjectId,
                    CreatedDate = a.CreatedDate
                })
                .OrderBy(a => a.CreatedDate)
                .ToListAsync();
            
            // Get featured image if exists
            AttachmentDto? featuredImage = null;
            if (document.FeaturedImageId.HasValue)
            {
                featuredImage = await _dbContext.Attachments
                    .Where(a => a.Id == document.FeaturedImageId.Value && !a.Deleted)
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        OriginalFileName = a.OriginalFileName,
                        FilePath = a.FilePath,
                        Url = a.Url,
                        FileSize = a.FileSize,
                        ContentType = a.ContentType,
                        ObjectType = a.ObjectType,
                        ObjectId = a.ObjectId,
                        CreatedDate = a.CreatedDate
                    })
                    .FirstOrDefaultAsync();
            }
            
            return new DocumentDetailDto
            {
                Id = document.Id,
                SlugVi = document.SlugVi,
                SlugEn = document.SlugEn,
                TitleVi = document.TitleVi,
                TitleEn = document.TitleEn,
                DescriptionVi = document.DescriptionVi,
                DescriptionEn = document.DescriptionEn,
                TimePosted = document.TimePosted,
                Status = document.Status,
                NewsCategoryId = document.NewsCategoryId,
                NewsCategoryNameVi = document.NewsCategory.TitleVi,
                NewsCategoryNameEn = document.NewsCategory.TitleEn,
                ImageUrl = document.ImageUrl,
                FeaturedImageId = document.FeaturedImageId,
                FeaturedImage = featuredImage,
                Documents = documents,
                CreatedDate = document.CreatedDate,
                ModifiedDate = document.ModifiedDate
            };
        }

        public async Task<PagingResult<NewsDto>> GetPublishedDocumentsByCategoryForClient(string categorySlug, PagingRequestBaseDto input)
        {
            var category = await _dbContext.NewsCategories
                .Where(c => (c.SlugVi == categorySlug || c.SlugEn == categorySlug) && !c.Deleted)
                .FirstOrDefaultAsync();
            
            if (category == null)
            {
                throw new UserFriendlyException(ErrorCode.NewsCategoryNotFound);
            }
            
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.NewsCategoryId == category.Id && n.IsDocument && n.Status == 1 && !n.Deleted)
                .OrderByDescending(n => n.TimePosted);
            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();
            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        public async Task<PagingResult<NewsDto>> GetFeaturedDocumentsForClient(PagingRequestBaseDto input)
        {
            var query = _dbContext.News
                .Include(n => n.NewsCategory)
                .Where(n => n.IsDocument && n.Status == 1 && n.IsOutstanding && !n.Deleted)
                .OrderByDescending(n => n.TimePosted);
            var totalCount = await query.CountAsync();
            var news = await query
                .Skip((input.Page - 1) * input.PageSize)
                .Take(input.PageSize)
                .Select(n => new NewsDto
                {
                    Id = n.Id,
                    SlugVi = n.SlugVi,
                    SlugEn = n.SlugEn,
                    TitleVi = n.TitleVi,
                    TitleEn = n.TitleEn,
                    DescriptionVi = n.DescriptionVi,
                    DescriptionEn = n.DescriptionEn,
                    NewsCategoryId = n.NewsCategoryId,
                    NewsCategoryTitleVi = n.NewsCategory.TitleVi,
                    NewsCategoryTitleEn = n.NewsCategory.TitleEn,
                    TimePosted = n.TimePosted,
                    Status = n.Status,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .ToListAsync();
            return new PagingResult<NewsDto>
            {
                Items = news,
                TotalItems = totalCount,
                Page = input.Page,
                PageSize = input.PageSize
            };
        }

        #endregion
    }
} 
