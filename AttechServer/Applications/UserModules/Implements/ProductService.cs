using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Product;
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
    public class ProductService : IProductService
    {
        private const int MAX_CONTENT_LENGTH = 100000; // 100KB
        private readonly ILogger<ProductService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWysiwygFileProcessor _wysiwygFileProcessor;
        private readonly IActivityLogService _activityLogService;
        private readonly IAttachmentService _attachmentService;

        public ProductService(ApplicationDbContext dbContext, ILogger<ProductService> logger, IHttpContextAccessor httpContextAccessor, IWysiwygFileProcessor wysiwygFileProcessor, IActivityLogService activityLogService, IAttachmentService attachmentService)
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

        public async Task<ProductDto> Create(CreateProductDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: Creating product with all data in one atomic operation");

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
                    var category = await _dbContext.ProductCategories
                        .Where(c => c.Id == input.ProductCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.ProductCategoryNotFound);
                    }

                    // Step 3: Check for duplicate titles
                    var titleViExists = await _dbContext.Products.AnyAsync(n => n.TitleVi == input.TitleVi && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.ProductTitleViExists);
                    }

                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.Products.AnyAsync(n => n.TitleEn == input.TitleEn && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.ProductTitleEnExists);
                        }
                    }

                    // Step 4: Sanitize content
                    var sanitizedContentVi = SanitizeContent(input.ContentVi);
                    var sanitizedContentEn = SanitizeContent(input.ContentEn ?? string.Empty);

                    // Step 5: Create product entity
                    var newProduct = new Product
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
                        ProductCategoryId = input.ProductCategoryId,
                        CreatedBy = userId,
                        CreatedDate = DateTime.Now,
                        Deleted = false
                    };

                    _dbContext.Products.Add(newProduct);
                    await _dbContext.SaveChangesAsync();

                    // Step 6: Smart content processing - extract unique attachment IDs first
                    var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(newProduct.ContentVi, newProduct.ContentEn);
                    
                    // Associate content attachments first
                    if (contentAttachmentIds.Any())
                    {
                        await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.Product, newProduct.Id, isFeaturedImage: false, isContentImage: true);
                    }
                    
                    // Process both content - now attachments are permanent
                    var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(newProduct.ContentVi, ObjectType.Product, newProduct.Id);
                    var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(newProduct.ContentEn, ObjectType.Product, newProduct.Id);
                    
                    // Update content with processed paths
                    newProduct.ContentVi = processedContentVi;
                    newProduct.ContentEn = processedContentEn;

                    // Step 7: Finalize gallery attachments (IsPrimary = false) - exclude content attachments
                    if (input.AttachmentIds != null && input.AttachmentIds.Any())
                    {
                        try
                        {
                            
                            // Gallery attachments = attachmentIds - contentAttachmentIds (to avoid duplicates)
                            var galleryAttachmentIds = input.AttachmentIds.Except(contentAttachmentIds).ToList();
                            if (galleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(galleryAttachmentIds, ObjectType.Product, newProduct.Id, isFeaturedImage: false, isContentImage: false);
                                _logger.LogInformation($"Finalized {galleryAttachmentIds.Count} gallery attachments for product ID: {newProduct.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing gallery attachments for product ID: {newProduct.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 7.5: Handle featured image (IsPrimary = true)
                    if (input.FeaturedImageId.HasValue)
                    {
                        try
                        {
                            await _attachmentService.AssociateAttachmentsAsync(new List<int> { input.FeaturedImageId.Value }, ObjectType.Product, newProduct.Id, isFeaturedImage: true, isContentImage: false);
                            
                            // ImageUrl will be set automatically by AttachmentService to /uploads/images/yyyy/MM/filename.ext
                            // No need to override it here
                            
                            _logger.LogInformation($"Finalized featured image {input.FeaturedImageId.Value} for product ID: {newProduct.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error finalizing featured image for product ID: {newProduct.Id}");
                            throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                        }
                    }

                    // Step 9: Save all changes and commit transaction
                    await _dbContext.SaveChangesAsync();
                    
                    // Log activity
                    await _activityLogService.LogAsync("PRODUCT_CREATE", "Tạo sản phẩm với file đính kèm", newProduct.TitleVi, "Info");

                    await transaction.CommitAsync();

                    // Step 10: Return ProductDto
                    var response = new ProductDto
                    {
                        Id = newProduct.Id,
                        SlugVi = newProduct.SlugVi,
                        SlugEn = newProduct.SlugEn,
                        TitleVi = newProduct.TitleVi,
                        TitleEn = newProduct.TitleEn,
                        DescriptionVi = newProduct.DescriptionVi,
                        DescriptionEn = newProduct.DescriptionEn,
                        ProductCategoryId = newProduct.ProductCategoryId,
                        TimePosted = newProduct.TimePosted,
                        Status = newProduct.Status,
                        IsOutstanding = newProduct.IsOutstanding
                    };

                    _logger.LogInformation($"Successfully created product. ProductId: {newProduct.Id}");
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
                    _logger.LogError(ex, $"Error creating product with attachments: {ex.Message}");
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
                    var product = await _dbContext.Products
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.ProductNotFound);

                    // Delete all associated files (featured, album, attachments)
                    await DeleteAssociatedFilesAsync(product.Id);

                    // Delete WYSIWYG files
                    await _wysiwygFileProcessor.DeleteFilesAsync(ObjectType.Product, product.Id);

                    product.Deleted = true;
                    await _dbContext.SaveChangesAsync();

                    // Log activity
                    await _activityLogService.LogAsync("PRODUCT_DELETE", "Xóa sản phẩm", product.TitleVi, "Info");

                    await transaction.CommitAsync();

                    _logger.LogInformation($"Successfully deleted product ID: {id} and all associated files");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error deleting product with id = {id}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Delete all files associated with a product article
        /// </summary>
        private async Task DeleteAssociatedFilesAsync(int productId)
        {
            try
            {
                // Get all files associated with this product
                var associatedFiles = await _dbContext.Attachments
                    .Where(f => f.ObjectType == ObjectType.Product && f.ObjectId == productId && !f.Deleted)
                    .ToListAsync();

                if (associatedFiles.Any())
                {
                    _logger.LogInformation($"Found {associatedFiles.Count} files to delete for product ID: {productId}");

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
                _logger.LogError(ex, $"Error deleting associated files for product ID: {productId}");
                throw;
            }
        }

        public async Task<PagingResult<ProductDto>> FindAll(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            var baseQuery = _dbContext.Products.AsNoTracking()
                .Where(n => !n.Deleted);

            // Filter by category ID
            if (input.CategoryId.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.ProductCategoryId == input.CategoryId.Value);
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
                .Select(n => new ProductDto
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
                    ProductCategoryId = n.ProductCategoryId,
                    ProductCategoryTitleVi = n.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = n.ProductCategory.TitleEn,
                    ProductCategorySlugVi = n.ProductCategory.SlugVi,
                    ProductCategorySlugEn = n.ProductCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<ProductDto>
            {
                TotalItems = totalItems,
                Items = pagedItems
            };
        }

        public async Task<PagingResult<ProductDto>> FindAllByCategoryId(PagingRequestBaseDto input, int categoryId)
        {
            _logger.LogInformation($"{nameof(FindAllByCategoryId)}: input = {JsonSerializer.Serialize(input)}, categoryId = {categoryId}");

            // Kiểm tra danh mục tồn tại
            var exists = await _dbContext.ProductCategories
                .AnyAsync(c => c.Id == categoryId && !c.Deleted);
            if (!exists)
                throw new UserFriendlyException(ErrorCode.ProductCategoryNotFound);

            // Build query
            var baseQuery = _dbContext.Products.AsNoTracking()
                .Where(n => !n.Deleted && n.ProductCategoryId == categoryId);

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
                .Select(n => new ProductDto
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
                    ProductCategoryId = n.ProductCategoryId,
                    ProductCategoryTitleVi = n.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = n.ProductCategory.TitleEn,
                    ProductCategorySlugVi = n.ProductCategory.SlugVi,
                    ProductCategorySlugEn = n.ProductCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl
                })
                .ToListAsync();

            return new PagingResult<ProductDto>
            {
                TotalItems = totalItems,
                Items = items
            };
        }

        public async Task<PagingResult<ProductDto>> FindAllByCategorySlug(PagingRequestBaseDto input, string slug)
        {
            // 1. Tìm category theo slug
            var category = await _dbContext.ProductCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SlugVi == slug && !c.Deleted);
            if (category == null)
                throw new UserFriendlyException(ErrorCode.ProductCategoryNotFound);

            // 2. Delegate về hàm cũ để xử lý paging
            return await FindAllByCategoryId(input, category.Id);
        }

        public async Task<DetailProductDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var product = await _dbContext.Products
                .Where(n => n.Id == id && !n.Deleted)
                .Select(n => new DetailProductDto
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
                    ProductCategoryId = n.ProductCategoryId,
                    ProductCategoryTitleVi = n.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = n.ProductCategory.TitleEn,
                    ProductCategorySlugVi = n.ProductCategory.SlugVi,
                    ProductCategorySlugEn = n.ProductCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                throw new UserFriendlyException(ErrorCode.ProductNotFound);
            }

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Product && a.ObjectId == id && !a.Deleted)
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
            product.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return product;
        }

        public async Task<DetailProductDto> FindBySlug(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlug)}: slug = {slug}");
            var product = await _dbContext.Products
                .Where(n => (n.SlugVi == slug || n.SlugEn == slug) && !n.Deleted)
                .Select(n => new DetailProductDto
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
                    ProductCategoryId = n.ProductCategoryId,
                    ProductCategoryTitleVi = n.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = n.ProductCategory.TitleEn,
                    ProductCategorySlugVi = n.ProductCategory.SlugVi,
                    ProductCategorySlugEn = n.ProductCategory.SlugEn,
                    IsOutstanding = n.IsOutstanding,
                    ImageUrl = n.ImageUrl,
                    FeaturedImageId = n.FeaturedImageId
                })
                .FirstOrDefaultAsync();

            if (product == null)
                throw new UserFriendlyException(ErrorCode.ProductNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Product && a.ObjectId == product.Id && !a.Deleted)
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
            product.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return product;
        }

        public async Task<ProductDto> Update(int id, UpdateProductDto input)
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

                    // Kiểm tra product tồn tại
                    var product = await _dbContext.Products
                        .FirstOrDefaultAsync(n => n.Id == id && !n.Deleted)
                        ?? throw new UserFriendlyException(ErrorCode.ProductNotFound);

                    // Check for duplicate titles (excluding current record)
                    var titleViExists = await _dbContext.Products.AnyAsync(n => n.TitleVi == input.TitleVi && n.Id != id && !n.Deleted);
                    if (titleViExists)
                    {
                        throw new UserFriendlyException(ErrorCode.ProductTitleViExists);
                    }

                    if (!string.IsNullOrEmpty(input.TitleEn))
                    {
                        var titleEnExists = await _dbContext.Products.AnyAsync(n => n.TitleEn == input.TitleEn && n.Id != id && !n.Deleted);
                        if (titleEnExists)
                        {
                            throw new UserFriendlyException(ErrorCode.ProductTitleEnExists);
                        }
                    }

                    // Kiểm tra danh mục
                    var category = await _dbContext.ProductCategories
                        .Where(c => c.Id == input.ProductCategoryId && !c.Deleted)
                        .Select(c => new { c.Id, c.TitleVi, c.TitleEn, c.SlugVi, c.SlugEn })
                        .FirstOrDefaultAsync();
                    if (category == null)
                    {
                        throw new UserFriendlyException(ErrorCode.ProductCategoryNotFound);
                    }

                    // Xóa các file cũ trong content nếu content thay đổi
                    if (product.ContentVi != input.ContentVi)
                    {
                        await _wysiwygFileProcessor.DeleteFilesAsync(ObjectType.Product, product.Id);
                    }

                    product.TitleVi = input.TitleVi;
                    product.TitleEn = input.TitleEn;
                    product.SlugVi = input.SlugVi;
                    product.SlugEn = input.SlugEn;
                    product.DescriptionVi = input.DescriptionVi;
                    product.DescriptionEn = input.DescriptionEn;
                    product.ContentVi = input.ContentVi;
                    product.ContentEn = input.ContentEn;
                    product.ProductCategoryId = input.ProductCategoryId;
                    product.TimePosted = input.TimePosted;
                    product.Status = input.Status;
                    product.IsOutstanding = input.IsOutstanding;
                    product.ModifiedBy = userId;

                    // Simple attachment update logic - compare current vs desired state
                    var currentGalleryAttachmentIds = await _attachmentService.GetCurrentGalleryAttachmentIdsAsync(ObjectType.Product, product.Id);
                    var currentFeaturedImageId = await _attachmentService.GetCurrentFeaturedImageIdAsync(ObjectType.Product, product.Id);
                    
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
                        await _attachmentService.SoftDeleteEntityAttachmentsAsync(ObjectType.Product, product.Id);
                        
                        // Process content attachments first - extract unique IDs
                        var contentAttachmentIds = await _wysiwygFileProcessor.ExtractUniqueAttachmentIdsAsync(product.ContentVi, product.ContentEn);
                        if (contentAttachmentIds.Any())
                        {
                            await _attachmentService.AssociateAttachmentsAsync(contentAttachmentIds, ObjectType.Product, product.Id, isFeaturedImage: false, isContentImage: true);
                        }
                        
                        // Process both content - now attachments are permanent
                        var (processedContentVi, fileEntriesVi) = await _wysiwygFileProcessor.ProcessContentAsync(product.ContentVi, ObjectType.Product, product.Id);
                        var (processedContentEn, fileEntriesEn) = await _wysiwygFileProcessor.ProcessContentAsync(product.ContentEn, ObjectType.Product, product.Id);
                        
                        // Update content with processed paths
                        product.ContentVi = processedContentVi;
                        product.ContentEn = processedContentEn;
                        
                        // Handle gallery attachments - exclude content attachments
                        if (desiredGalleryAttachmentIds.Any())
                        {
                            // Gallery attachments = desiredGalleryAttachmentIds - contentAttachmentIds (to avoid duplicates)
                            var pureGalleryAttachmentIds = desiredGalleryAttachmentIds.Except(contentAttachmentIds).ToList();
                            if (pureGalleryAttachmentIds.Any())
                            {
                                await _attachmentService.AssociateAttachmentsAsync(pureGalleryAttachmentIds, ObjectType.Product, product.Id, isFeaturedImage: false, isContentImage: false);
                                _logger.LogInformation($"Updated {pureGalleryAttachmentIds.Count} gallery attachments for product ID: {product.Id}");
                            }
                        }
                        
                        // Handle featured image
                        if (desiredFeaturedImageId.HasValue)
                        {
                            try
                            {
                                await _attachmentService.AssociateAttachmentsAsync(new List<int> { desiredFeaturedImageId.Value }, ObjectType.Product, product.Id, isFeaturedImage: true, isContentImage: false);
                                _logger.LogInformation($"Updated featured image {desiredFeaturedImageId.Value} for product ID: {product.Id}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error finalizing featured image for product ID: {product.Id}");
                                throw new UserFriendlyException(ErrorCode.InvalidClientRequest);
                            }
                        }
                        else
                        {
                            // Clear featured image if not provided
                            product.FeaturedImageId = null;
                            product.ImageUrl = string.Empty;
                        }
                    }
                    // If nothing changed → keep everything as is

                    await _dbContext.SaveChangesAsync();

                    // Log activity
                    await _activityLogService.LogAsync("PRODUCT_UPDATE", "Cập nhật sản phẩm", product.TitleVi, "Info");

                    await transaction.CommitAsync();

                    return new ProductDto
                    {
                        Id = product.Id,
                        TitleVi = product.TitleVi,
                        TitleEn = product.TitleEn,
                        SlugVi = product.SlugVi,
                        SlugEn = product.SlugEn,
                        DescriptionVi = product.DescriptionVi,
                        DescriptionEn = product.DescriptionEn,
                        TimePosted = product.TimePosted,
                        Status = product.Status,
                        ProductCategoryId = product.ProductCategoryId,
                        ProductCategoryTitleVi = category.TitleVi,
                        ProductCategoryTitleEn = category.TitleEn,
                        ProductCategorySlugVi = category.SlugVi,
                        ProductCategorySlugEn = category.SlugEn,
                        IsOutstanding = product.IsOutstanding,
                        ImageUrl = product.ImageUrl
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error updating product with id = {id}");
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

        private IQueryable<Product> ApplySorting(IQueryable<Product> query, PagingRequestBaseDto input)
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
                        return input.IsAscending ? query.OrderBy(x => x.ProductCategory.TitleVi) : query.OrderByDescending(x => x.ProductCategory.TitleVi);
                    case "categoryen":
                        return input.IsAscending ? query.OrderBy(x => x.ProductCategory.TitleEn) : query.OrderByDescending(x => x.ProductCategory.TitleEn);
                    default:
                        return query.OrderByDescending(x => x.TimePosted); // default sort
                }
            }
            else
            {
                return query.OrderByDescending(x => x.TimePosted); // default sort
            }
        }

        private IQueryable<Product> ApplyClientSorting(IQueryable<Product> query, PagingRequestBaseDto input)
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
                    case "isoutstanding":
                        return input.IsAscending ? query.OrderBy(x => x.IsOutstanding) : query.OrderByDescending(x => x.IsOutstanding);
                    case "category":
                    case "categoryvi":
                        return input.IsAscending ? query.OrderBy(x => x.ProductCategory.TitleVi) : query.OrderByDescending(x => x.ProductCategory.TitleVi);
                    case "categoryen":
                        return input.IsAscending ? query.OrderBy(x => x.ProductCategory.TitleEn) : query.OrderByDescending(x => x.ProductCategory.TitleEn);
                    default:
                        return query.OrderByDescending(x => x.TimePosted); // default client sort
                }
            }
            else
            {
                return query.OrderByDescending(x => x.TimePosted); // default client sort
            }
        }

        public async Task<PagingResult<ProductDto>> FindAllForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(FindAllForClient)}: Getting published products for client");
            
            var query = _dbContext.Products
                .Include(p => p.ProductCategory)
                .Where(p => !p.Deleted && p.Status == CommonStatus.ACTIVE);

            // Filter by category ID
            if (input.CategoryId.HasValue)
            {
                query = query.Where(p => p.ProductCategoryId == input.CategoryId.Value);
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                query = query.Where(p => p.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                query = query.Where(p => p.TimePosted <= input.DateTo.Value);
            }

            // Filter by outstanding
            if (input.IsOutstanding.HasValue)
            {
                query = query.Where(p => p.IsOutstanding == input.IsOutstanding.Value);
            }

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(p => p.TitleVi.Contains(input.Keyword) || 
                                       p.TitleEn.Contains(input.Keyword) ||
                                       p.DescriptionVi.Contains(input.Keyword) ||
                                       p.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            // Apply sorting for client (default: outstanding first, then by time)
            var sortedQuery = ApplyClientSorting(query, input);

            var products = await sortedQuery
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    SlugVi = p.SlugVi,
                    SlugEn = p.SlugEn,
                    TitleVi = p.TitleVi,
                    TitleEn = p.TitleEn,
                    DescriptionVi = p.DescriptionVi,
                    DescriptionEn = p.DescriptionEn,
                    TimePosted = p.TimePosted,
                    Status = p.Status,
                    ProductCategoryId = p.ProductCategoryId,
                    ProductCategoryTitleVi = p.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = p.ProductCategory.TitleEn,
                    ProductCategorySlugVi = p.ProductCategory.SlugVi,
                    ProductCategorySlugEn = p.ProductCategory.SlugEn,
                    IsOutstanding = p.IsOutstanding,
                    ImageUrl = p.ImageUrl,
                    FeaturedImageId = p.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<ProductDto>
            {
                Items = products,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<DetailProductDto> FindBySlugForClient(string slug)
        {
            _logger.LogInformation($"{nameof(FindBySlugForClient)}: slug = {slug}");
            var product = await _dbContext.Products
                .Where(p => (p.SlugVi == slug || p.SlugEn == slug) && !p.Deleted && p.Status == CommonStatus.ACTIVE)
                .Select(p => new DetailProductDto
                {
                    Id = p.Id,
                    TitleVi = p.TitleVi,
                    TitleEn = p.TitleEn,
                    SlugVi = p.SlugVi,
                    SlugEn = p.SlugEn,
                    DescriptionVi = p.DescriptionVi,
                    DescriptionEn = p.DescriptionEn,
                    ContentVi = p.ContentVi,
                    ContentEn = p.ContentEn,
                    TimePosted = p.TimePosted,
                    Status = p.Status,
                    ProductCategoryId = p.ProductCategoryId,
                    ProductCategoryTitleVi = p.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = p.ProductCategory.TitleEn,
                    ProductCategorySlugVi = p.ProductCategory.SlugVi,
                    ProductCategorySlugEn = p.ProductCategory.SlugEn,
                    IsOutstanding = p.IsOutstanding,
                    ImageUrl = p.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (product == null)
                throw new UserFriendlyException(ErrorCode.ProductNotFound);

            // Load attachments
            var attachments = await _dbContext.Attachments
                .Where(a => a.ObjectType == ObjectType.Product && a.ObjectId == product.Id && !a.Deleted)
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

            product.Attachments = new AttachmentsGroupDto
            {
                Images = attachments.Where(a => a.ContentType.StartsWith("image/") && !a.IsPrimary && !a.IsContentImage).ToList(),
                Documents = attachments.Where(a => !a.ContentType.StartsWith("image/")).ToList()
            };

            return product;
        }

        public async Task<PagingResult<ProductDto>> FindAllByCategorySlugForClient(PagingRequestBaseDto input, string slug)
        {
            _logger.LogInformation($"{nameof(FindAllByCategorySlugForClient)}: slug = {slug}");
            
            var query = _dbContext.Products
                .Include(p => p.ProductCategory)
                .Where(p => !p.Deleted && p.Status == CommonStatus.ACTIVE &&
                           (p.ProductCategory.SlugVi == slug || p.ProductCategory.SlugEn == slug));

            // Search by keyword if provided
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(p => p.TitleVi.Contains(input.Keyword) || 
                                       p.TitleEn.Contains(input.Keyword) ||
                                       p.DescriptionVi.Contains(input.Keyword) ||
                                       p.DescriptionEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.IsOutstanding)
                .ThenBy(p => p.Id)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    SlugVi = p.SlugVi,
                    SlugEn = p.SlugEn,
                    TitleVi = p.TitleVi,
                    TitleEn = p.TitleEn,
                    DescriptionVi = p.DescriptionVi,
                    DescriptionEn = p.DescriptionEn,
                    TimePosted = p.TimePosted,
                    Status = p.Status,
                    ProductCategoryId = p.ProductCategoryId,
                    ProductCategoryTitleVi = p.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = p.ProductCategory.TitleEn,
                    ProductCategorySlugVi = p.ProductCategory.SlugVi,
                    ProductCategorySlugEn = p.ProductCategory.SlugEn,
                    IsOutstanding = p.IsOutstanding,
                    ImageUrl = p.ImageUrl,
                    FeaturedImageId = p.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<ProductDto>
            {
                Items = products,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }

        public async Task<PagingResult<ProductDto>> SearchForClient(PagingRequestBaseDto input)
        {
            _logger.LogInformation($"{nameof(SearchForClient)}: keyword = {input.Keyword}");
            
            var query = _dbContext.Products
                .Include(p => p.ProductCategory)
                .Where(p => !p.Deleted && p.Status == CommonStatus.ACTIVE);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                query = query.Where(p => p.TitleVi.Contains(input.Keyword) || 
                                       p.TitleEn.Contains(input.Keyword) ||
                                       p.DescriptionVi.Contains(input.Keyword) ||
                                       p.DescriptionEn.Contains(input.Keyword) ||
                                       p.ContentVi.Contains(input.Keyword) ||
                                       p.ContentEn.Contains(input.Keyword));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.IsOutstanding)
                .ThenByDescending(p => p.TimePosted)
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalCount : input.PageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    SlugVi = p.SlugVi,
                    SlugEn = p.SlugEn,
                    TitleVi = p.TitleVi,
                    TitleEn = p.TitleEn,
                    DescriptionVi = p.DescriptionVi,
                    DescriptionEn = p.DescriptionEn,
                    TimePosted = p.TimePosted,
                    Status = p.Status,
                    ProductCategoryId = p.ProductCategoryId,
                    ProductCategoryTitleVi = p.ProductCategory.TitleVi,
                    ProductCategoryTitleEn = p.ProductCategory.TitleEn,
                    ProductCategorySlugVi = p.ProductCategory.SlugVi,
                    ProductCategorySlugEn = p.ProductCategory.SlugEn,
                    IsOutstanding = p.IsOutstanding,
                    ImageUrl = p.ImageUrl,
                    FeaturedImageId = p.FeaturedImageId
                })
                .ToListAsync();

            return new PagingResult<ProductDto>
            {
                Items = products,
                TotalItems = totalCount,
                PageSize = input.PageSize == -1 ? totalCount : input.PageSize,
                Page = input.PageNumber
            };
        }
    }
}
