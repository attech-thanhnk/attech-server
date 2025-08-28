using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.LanguageContent;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AttechServer.Applications.UserModules.Implements
{
    public class LanguageContentService : ILanguageContentService
    {
        private readonly ILogger<LanguageContentService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LanguageContentService(
            ApplicationDbContext dbContext, 
            ILogger<LanguageContentService> logger, 
            IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private IQueryable<LanguageContent> ApplySorting(IQueryable<LanguageContent> query, PagingRequestBaseDto input)
        {
            if (!string.IsNullOrEmpty(input.SortBy))
            {
                switch (input.SortBy.ToLower())
                {
                    case "id":
                        return input.IsAscending ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id);
                    case "contentkey":
                        return input.IsAscending ? query.OrderBy(x => x.ContentKey) : query.OrderByDescending(x => x.ContentKey);
                    case "category":
                        return input.IsAscending ? query.OrderBy(x => x.Category) : query.OrderByDescending(x => x.Category);
                    case "createddate":
                        return input.IsAscending ? query.OrderBy(x => x.CreatedDate) : query.OrderByDescending(x => x.CreatedDate);
                    default:
                        return query.OrderBy(x => x.ContentKey);
                }
            }
            else
            {
                return query.OrderBy(x => x.ContentKey);
            }
        }

        public async Task<PagingResult<LanguageContentDto>> FindAll(PagingRequestBaseDto input)
        {
            try
            {
                var query = _dbContext.LanguageContents.Where(x => !x.Deleted);

                if (!string.IsNullOrEmpty(input.Keyword))
                {
                    query = query.Where(x => x.ContentKey.Contains(input.Keyword) ||
                                          (x.ValueVi != null && x.ValueVi.Contains(input.Keyword)) ||
                                          (x.ValueEn != null && x.ValueEn.Contains(input.Keyword)));
                }

                if (input.CategoryId.HasValue)
                {
                    var categoryName = GetCategoryNameById(input.CategoryId.Value);
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        query = query.Where(x => x.Category == categoryName);
                    }
                }

                query = ApplySorting(query, input);

                var totalItems = await query.CountAsync();
                
                var items = await query
                    .Skip((input.PageNumber - 1) * input.PageSize)
                    .Take(input.PageSize)
                    .Select(x => new LanguageContentDto
                    {
                        Id = x.Id,
                        ContentKey = x.ContentKey,
                        ValueVi = x.ValueVi,
                        ValueEn = x.ValueEn,
                        Category = x.Category,
                        Description = x.Description,
                        CreatedDate = x.CreatedDate,
                        ModifiedDate = x.ModifiedDate
                    })
                    .ToListAsync();

                return new PagingResult<LanguageContentDto>
                {
                    Items = items,
                    TotalItems = totalItems,
                    Page = input.PageNumber,
                    PageSize = input.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding all language contents");
                throw;
            }
        }

        public async Task<LanguageContentDto> FindById(int id)
        {
            try
            {
                var entity = await _dbContext.LanguageContents
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    throw new UserFriendlyException(ErrorCode.NotFound);
                }

                return new LanguageContentDto
                {
                    Id = entity.Id,
                    ContentKey = entity.ContentKey,
                    ValueVi = entity.ValueVi,
                    ValueEn = entity.ValueEn,
                    Category = entity.Category,
                    Description = entity.Description,
                    CreatedDate = entity.CreatedDate,
                    ModifiedDate = entity.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding language content by id: {Id}", id);
                throw;
            }
        }

        public async Task<LanguageContentDto> Create(CreateLanguageContentDto input)
        {
            try
            {
                var existingEntity = await _dbContext.LanguageContents
                    .Where(x => x.ContentKey == input.ContentKey && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (existingEntity != null)
                {
                    throw new UserFriendlyException(ErrorCode.BadRequest);
                }

                var userId = GetCurrentUserId();
                var now = DateTime.UtcNow;

                var entity = new LanguageContent
                {
                    ContentKey = input.ContentKey,
                    ValueVi = input.ValueVi,
                    ValueEn = input.ValueEn,
                    Category = input.Category,
                    Description = input.Description,
                    CreatedDate = now,
                    CreatedBy = userId,
                    ModifiedDate = now,
                    ModifiedBy = userId,
                    Deleted = false
                };

                _dbContext.LanguageContents.Add(entity);
                await _dbContext.SaveChangesAsync();

                return new LanguageContentDto
                {
                    Id = entity.Id,
                    ContentKey = entity.ContentKey,
                    ValueVi = entity.ValueVi,
                    ValueEn = entity.ValueEn,
                    Category = entity.Category,
                    Description = entity.Description,
                    CreatedDate = entity.CreatedDate,
                    ModifiedDate = entity.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating language content");
                throw;
            }
        }

        public async Task<LanguageContentDto> Update(int id, UpdateLanguageContentDto input)
        {
            try
            {
                var entity = await _dbContext.LanguageContents
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    throw new UserFriendlyException(ErrorCode.NotFound);
                }

                var existingEntity = await _dbContext.LanguageContents
                    .Where(x => x.ContentKey == input.ContentKey && x.Id != id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (existingEntity != null)
                {
                    throw new UserFriendlyException(ErrorCode.BadRequest);
                }

                var userId = GetCurrentUserId();
                var now = DateTime.UtcNow;

                entity.ContentKey = input.ContentKey;
                entity.ValueVi = input.ValueVi;
                entity.ValueEn = input.ValueEn;
                entity.Category = input.Category;
                entity.Description = input.Description;
                entity.ModifiedDate = now;
                entity.ModifiedBy = userId;

                await _dbContext.SaveChangesAsync();

                return new LanguageContentDto
                {
                    Id = entity.Id,
                    ContentKey = entity.ContentKey,
                    ValueVi = entity.ValueVi,
                    ValueEn = entity.ValueEn,
                    Category = entity.Category,
                    Description = entity.Description,
                    CreatedDate = entity.CreatedDate,
                    ModifiedDate = entity.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating language content with id: {Id}", id);
                throw;
            }
        }

        public async Task Delete(int id)
        {
            try
            {
                var entity = await _dbContext.LanguageContents
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    throw new UserFriendlyException(ErrorCode.NotFound);
                }

                entity.Deleted = true;
                entity.ModifiedDate = DateTime.UtcNow;
                entity.ModifiedBy = GetCurrentUserId();

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting language content with id: {Id}", id);
                throw;
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private string? GetCategoryNameById(int categoryId)
        {
            return categoryId switch
            {
                1 => "common",
                2 => "navigation",
                3 => "auth",
                4 => "admin",
                5 => "frontend",
                6 => "validation",
                7 => "errors",
                _ => null
            };
        }
    }
}