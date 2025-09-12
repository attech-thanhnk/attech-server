using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.InternalDocument;
using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AttechServer.Applications.UserModules.Implements
{
    public class InternalDocumentService : IInternalDocumentService
    {
        private readonly ILogger<InternalDocumentService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;
        private readonly IAttachmentService _attachmentService;
        private readonly IWebHostEnvironment _environment;

        public InternalDocumentService(
            ApplicationDbContext dbContext,
            ILogger<InternalDocumentService> logger,
            IHttpContextAccessor httpContextAccessor,
            IActivityLogService activityLogService,
            IAttachmentService attachmentService,
            IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
            _environment = environment;
        }

        private IQueryable<InternalDocument> ApplySorting(IQueryable<InternalDocument> query, InternalDocumentPagingRequestDto input)
        {
            if (!string.IsNullOrEmpty(input.SortBy))
            {
                switch (input.SortBy.ToLower())
                {
                    case "id":
                        return input.IsAscending ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id);
                    case "title":
                        return input.IsAscending ? query.OrderBy(x => x.Title) : query.OrderByDescending(x => x.Title);
                    case "category":
                        return input.IsAscending ? query.OrderBy(x => x.Category) : query.OrderByDescending(x => x.Category);
                    case "status":
                        return input.IsAscending ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status);
                    case "createddate":
                        return input.IsAscending ? query.OrderBy(x => x.CreatedDate) : query.OrderByDescending(x => x.CreatedDate);
                    case "timeposted":
                        return input.IsAscending ? query.OrderBy(x => x.TimePosted) : query.OrderByDescending(x => x.TimePosted);
                    default:
                        return query.OrderByDescending(x => x.TimePosted);
                }
            }
            return query.OrderByDescending(x => x.TimePosted);
        }

        public async Task<PagingResult<InternalDocumentDto>> FindAll(InternalDocumentPagingRequestDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: Getting internal documents with paging");

            IQueryable<InternalDocument> query = _dbContext.InternalDocuments
                .Where(x => !x.Deleted);

            if (!string.IsNullOrEmpty(input.Keyword))
            {
                query = query.Where(x => x.Title.Contains(input.Keyword) ||
                                        x.Description.Contains(input.Keyword) ||
                                        x.Category.Contains(input.Keyword));
            }

            // Filter by status
            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            // Filter by category
            if (!string.IsNullOrEmpty(input.Category))
            {
                query = query.Where(x => x.Category == input.Category);
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                query = query.Where(x => x.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                query = query.Where(x => x.TimePosted <= input.DateTo.Value);
            }

            query = ApplySorting(query, input);
            query = query.Include(x => x.Attachment);

            var totalRecords = await query.CountAsync();
            var items = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalRecords : input.PageSize)
                .Select(x => new InternalDocumentDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    AttachmentId = x.AttachmentId,
                    Status = x.Status,
                    TimePosted = x.TimePosted,
                    Attachment = x.Attachment != null ? new AttachmentDto
                    {
                        Id = x.Attachment.Id,
                        FilePath = x.Attachment.FilePath,
                        Url = x.Attachment.Url,
                        OriginalFileName = x.Attachment.OriginalFileName,
                        FileSize = x.Attachment.FileSize,
                        ContentType = x.Attachment.ContentType
                    } : null,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate
                })
                .ToListAsync();

            return new PagingResult<InternalDocumentDto>
            {
                Items = items,
                TotalItems = totalRecords,
                Page = input.PageNumber,
                PageSize = input.PageSize
            };
        }

        public async Task<PagingResult<InternalDocumentDto>> FindAllByCategory(InternalDocumentPagingRequestDto input, string category)
        {
            _logger.LogInformation($"{nameof(FindAllByCategory)}: Getting internal documents by category: {category}");

            IQueryable<InternalDocument> query = _dbContext.InternalDocuments
                .Where(x => !x.Deleted && x.Category == category);

            if (!string.IsNullOrEmpty(input.Keyword))
            {
                query = query.Where(x => x.Title.Contains(input.Keyword) ||
                                        x.Description.Contains(input.Keyword));
            }

            // Filter by status
            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                query = query.Where(x => x.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                query = query.Where(x => x.TimePosted <= input.DateTo.Value);
            }

            query = ApplySorting(query, input);
            query = query.Include(x => x.Attachment);

            var totalRecords = await query.CountAsync();
            var items = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalRecords : input.PageSize)
                .Select(x => new InternalDocumentDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    AttachmentId = x.AttachmentId,
                    Status = x.Status,
                    TimePosted = x.TimePosted,
                    Attachment = x.Attachment != null ? new AttachmentDto
                    {
                        Id = x.Attachment.Id,
                        FilePath = x.Attachment.FilePath,
                        Url = x.Attachment.Url,
                        OriginalFileName = x.Attachment.OriginalFileName,
                        FileSize = x.Attachment.FileSize,
                        ContentType = x.Attachment.ContentType
                    } : null,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate
                })
                .ToListAsync();

            return new PagingResult<InternalDocumentDto>
            {
                Items = items,
                TotalItems = totalRecords,
                Page = input.PageNumber,
                PageSize = input.PageSize
            };
        }

        public async Task<DetailInternalDocumentDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: Getting internal document by ID: {id}");

            var document = await _dbContext.InternalDocuments
                .Include(x => x.Attachment)
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (document == null)
            {
                throw new UserFriendlyException(ErrorCode.NotFound);
            }

            return new DetailInternalDocumentDto
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                Category = document.Category,
                AttachmentId = document.AttachmentId,
                Status = document.Status,
                TimePosted = document.TimePosted,
                Attachment = document.Attachment != null ? new AttachmentDto
                {
                    Id = document.Attachment.Id,
                    FilePath = document.Attachment.FilePath,
                    Url = document.Attachment.Url,
                    OriginalFileName = document.Attachment.OriginalFileName,
                    FileSize = document.Attachment.FileSize,
                    ContentType = document.Attachment.ContentType
                } : null,
                CreatedDate = document.CreatedDate,
                ModifiedDate = document.ModifiedDate,
                CreatedBy = document.CreatedBy,
                ModifiedBy = document.ModifiedBy
            };
        }

        public async Task<InternalDocumentDto> Create(CreateInternalDocumentDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: Creating internal document");

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(input.Title))
                {
                    throw new ArgumentException("Title is required.");
                }

                if (input.Title.Length > 500)
                {
                    throw new UserFriendlyException(ErrorCode.TitleTooLong);
                }

                var document = new InternalDocument
                {
                    Title = input.Title.Trim(),
                    Description = input.Description?.Trim() ?? string.Empty,
                    Category = input.Category.Trim(),
                    AttachmentId = input.AttachmentId,
                    Status = input.Status,
                    TimePosted = input.TimePosted ?? DateTime.UtcNow
                };

                _dbContext.InternalDocuments.Add(document);
                await _dbContext.SaveChangesAsync();

                // Associate attachment if provided
                if (input.AttachmentId.HasValue)
                {
                    await _attachmentService.AssociateAttachmentsAsync(
                        new List<int> { input.AttachmentId.Value }, 
                        ObjectType.Document, 
                        document.Id, 
                        isFeaturedImage: false, 
                        isContentImage: false
                    );
                }

                await transaction.CommitAsync();

                await _activityLogService.LogAsync("CREATE", $"Created internal document: {document.Title}", "Information");

                return new InternalDocumentDto
                {
                    Id = document.Id,
                    Title = document.Title,
                    Description = document.Description,
                    Category = document.Category,
                    AttachmentId = document.AttachmentId,
                    Status = document.Status,
                    TimePosted = document.TimePosted,
                    CreatedDate = document.CreatedDate,
                    ModifiedDate = document.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating internal document");
                throw;
            }
        }

        public async Task<InternalDocumentDto> Update(int id, UpdateInternalDocumentDto input)
        {
            _logger.LogInformation($"{nameof(Update)}: Updating internal document ID: {id}");

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var document = await _dbContext.InternalDocuments
                    .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

                if (document == null)
                {
                    throw new UserFriendlyException(ErrorCode.NotFound);
                }

                if (string.IsNullOrWhiteSpace(input.Title))
                {
                    throw new ArgumentException("Title is required.");
                }

                if (input.Title.Length > 500)
                {
                    throw new UserFriendlyException(ErrorCode.TitleTooLong);
                }

                var oldAttachmentId = document.AttachmentId;
                
                document.Title = input.Title.Trim();
                document.Description = input.Description?.Trim() ?? string.Empty;
                document.Category = input.Category.Trim();
                document.AttachmentId = input.AttachmentId;
                document.Status = input.Status;

                await _dbContext.SaveChangesAsync();

                // Handle attachment changes
                if (oldAttachmentId != input.AttachmentId)
                {
                    // Soft delete old attachment if exists
                    if (oldAttachmentId.HasValue)
                    {
                        await _attachmentService.SoftDeleteEntityAttachmentsAsync(ObjectType.Document, document.Id);
                    }

                    // Associate new attachment if provided
                    if (input.AttachmentId.HasValue)
                    {
                        await _attachmentService.AssociateAttachmentsAsync(
                            new List<int> { input.AttachmentId.Value }, 
                            ObjectType.Document, 
                            document.Id, 
                            isFeaturedImage: false, 
                            isContentImage: false
                        );
                    }
                }

                await transaction.CommitAsync();

                await _activityLogService.LogAsync("UPDATE", $"Updated internal document: {document.Title}", "Information");

                return new InternalDocumentDto
                {
                    Id = document.Id,
                    Title = document.Title,
                    Description = document.Description,
                    Category = document.Category,
                    AttachmentId = document.AttachmentId,
                    Status = document.Status,
                    TimePosted = document.TimePosted,
                    CreatedDate = document.CreatedDate,
                    ModifiedDate = document.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating internal document");
                throw;
            }
        }

        public async Task Delete(int id)
        {
            _logger.LogInformation($"{nameof(Delete)}: Deleting internal document ID: {id}");

            var document = await _dbContext.InternalDocuments
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (document == null)
            {
                throw new UserFriendlyException(ErrorCode.NotFound);
            }

            document.Deleted = true;
            await _dbContext.SaveChangesAsync();

            await _activityLogService.LogAsync("DELETE", $"Deleted internal document: {document.Title}", "Warning");
        }

        public async Task<PagingResult<InternalDocumentDto>> GetPublishedDocumentsForClient(InternalDocumentPagingRequestDto input)
        {
            _logger.LogInformation($"{nameof(GetPublishedDocumentsForClient)}: Getting published internal documents");

            IQueryable<InternalDocument> query = _dbContext.InternalDocuments
                .Where(x => !x.Deleted && x.Status == 1);

            if (!string.IsNullOrEmpty(input.Keyword))
            {
                query = query.Where(x => x.Title.Contains(input.Keyword) ||
                                        x.Description.Contains(input.Keyword) ||
                                        x.Category.Contains(input.Keyword));
            }

            // Filter by category (additional filter on top of Status == 1)
            if (!string.IsNullOrEmpty(input.Category))
            {
                query = query.Where(x => x.Category == input.Category);
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                query = query.Where(x => x.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                query = query.Where(x => x.TimePosted <= input.DateTo.Value);
            }

            query = ApplySorting(query, input);
            query = query.Include(x => x.Attachment);

            var totalRecords = await query.CountAsync();
            var items = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalRecords : input.PageSize)
                .Select(x => new InternalDocumentDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    AttachmentId = x.AttachmentId,
                    Status = x.Status,
                    TimePosted = x.TimePosted,
                    Attachment = x.Attachment != null ? new AttachmentDto
                    {
                        Id = x.Attachment.Id,
                        FilePath = x.Attachment.FilePath,
                        Url = x.Attachment.Url,
                        OriginalFileName = x.Attachment.OriginalFileName,
                        FileSize = x.Attachment.FileSize,
                        ContentType = x.Attachment.ContentType
                    } : null,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate
                })
                .ToListAsync();

            return new PagingResult<InternalDocumentDto>
            {
                Items = items,
                TotalItems = totalRecords,
                Page = input.PageNumber,
                PageSize = input.PageSize
            };
        }

        public async Task<PagingResult<InternalDocumentDto>> GetPublishedDocumentsByCategoryForClient(string category, InternalDocumentPagingRequestDto input)
        {
            _logger.LogInformation($"{nameof(GetPublishedDocumentsByCategoryForClient)}: Getting published documents by category: {category}");

            IQueryable<InternalDocument> query = _dbContext.InternalDocuments
                .Where(x => !x.Deleted && x.Status == 1 && x.Category == category);

            if (!string.IsNullOrEmpty(input.Keyword))
            {
                query = query.Where(x => x.Title.Contains(input.Keyword) ||
                                        x.Description.Contains(input.Keyword));
            }

            // Filter by date range
            if (input.DateFrom.HasValue)
            {
                query = query.Where(x => x.TimePosted >= input.DateFrom.Value);
            }
            if (input.DateTo.HasValue)
            {
                query = query.Where(x => x.TimePosted <= input.DateTo.Value);
            }

            query = ApplySorting(query, input);
            query = query.Include(x => x.Attachment);

            var totalRecords = await query.CountAsync();
            var items = await query
                .Skip(input.GetSkip())
                .Take(input.PageSize == -1 ? totalRecords : input.PageSize)
                .Select(x => new InternalDocumentDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    AttachmentId = x.AttachmentId,
                    Status = x.Status,
                    TimePosted = x.TimePosted,
                    Attachment = x.Attachment != null ? new AttachmentDto
                    {
                        Id = x.Attachment.Id,
                        FilePath = x.Attachment.FilePath,
                        Url = x.Attachment.Url,
                        OriginalFileName = x.Attachment.OriginalFileName,
                        FileSize = x.Attachment.FileSize,
                        ContentType = x.Attachment.ContentType
                    } : null,
                    CreatedDate = x.CreatedDate,
                    ModifiedDate = x.ModifiedDate
                })
                .ToListAsync();

            return new PagingResult<InternalDocumentDto>
            {
                Items = items,
                TotalItems = totalRecords,
                Page = input.PageNumber,
                PageSize = input.PageSize
            };
        }

        public async Task<DetailInternalDocumentDto> GetPublishedDocumentByIdForClient(int id)
        {
            _logger.LogInformation($"{nameof(GetPublishedDocumentByIdForClient)}: Getting published document by ID: {id}");

            var document = await _dbContext.InternalDocuments
                .Include(x => x.Attachment)
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted && x.Status == 1);

            if (document == null)
            {
                throw new UserFriendlyException(ErrorCode.NotFound);
            }

            return new DetailInternalDocumentDto
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                Category = document.Category,
                AttachmentId = document.AttachmentId,
                Status = document.Status,
                Attachment = document.Attachment != null ? new AttachmentDto
                {
                    Id = document.Attachment.Id,
                    FilePath = document.Attachment.FilePath,
                    Url = document.Attachment.Url,
                    OriginalFileName = document.Attachment.OriginalFileName,
                    FileSize = document.Attachment.FileSize,
                    ContentType = document.Attachment.ContentType
                } : null,
                CreatedDate = document.CreatedDate,
                ModifiedDate = document.ModifiedDate
            };
        }


        public async Task<List<string>> GetCategories()
        {
            _logger.LogInformation($"{nameof(GetCategories)}: Getting all categories");

            var categories = await _dbContext.InternalDocuments
                .Where(x => !x.Deleted && x.Status == 1) // Only published documents
                .Select(x => x.Category)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return categories;
        }
    }
}