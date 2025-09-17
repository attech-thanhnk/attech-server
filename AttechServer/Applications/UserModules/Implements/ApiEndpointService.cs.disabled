using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.ApiEndpoint;
using AttechServer.Domains.Entities;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.Consts;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AttechServer.Applications.UserModules.Implements
{
    public class ApiEndpointService : IApiEndpointService
    {
        private readonly ILogger<ApiEndpointService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private const string API_ENDPOINTS_CACHE_KEY = "api_endpoints";

        public ApiEndpointService(
            ILogger<ApiEndpointService> logger,
            ApplicationDbContext dbContext,
            IMemoryCache cache)
        {
            _logger = logger;
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task<List<ApiEndpointDto>> FindAll()
        {
            _logger.LogInformation($"{nameof(FindAll)}");

            return await _cache.GetOrCreateAsync(API_ENDPOINTS_CACHE_KEY, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);

                var endpoints = await _dbContext.ApiEndpoints
                    .Where(a => !a.Deleted)
                    .OrderBy(a => a.Path)
                    .ThenBy(a => a.HttpMethod)
                    .ToListAsync();

                return endpoints.Select(e => new ApiEndpointDto
                {
                    Id = e.Id,
                    Path = e.Path,
                    HttpMethod = e.HttpMethod,
                    Description = e.Description,
                    RequireAuthentication = e.RequireAuthentication,
                    CreatedAt = e.CreatedDate ?? DateTime.MinValue,
                    UpdatedAt = e.ModifiedDate
                }).ToList();
            }) ?? new List<ApiEndpointDto>();
        }

        public async Task<ApiEndpointDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var endpoint = await _dbContext.ApiEndpoints
                .Where(a => !a.Deleted && a.Id == id)
                .FirstOrDefaultAsync()
                ?? throw new UserFriendlyException(ErrorCode.ApiEndpointNotFound);

            return new ApiEndpointDto
            {
                Id = endpoint.Id,
                Path = endpoint.Path,
                HttpMethod = endpoint.HttpMethod,
                Description = endpoint.Description,
                RequireAuthentication = endpoint.RequireAuthentication,
                CreatedAt = endpoint.CreatedDate ?? DateTime.MinValue,
                UpdatedAt = endpoint.ModifiedDate
            };
        }

        public async Task Create(CreateApiEndpointDto input)
        {
            _logger.LogInformation($"{nameof(Create)}: Creating endpoint {input.HttpMethod} {input.Path}");

            var existingEndpoint = await _dbContext.ApiEndpoints
                .Where(a => !a.Deleted && 
                           a.Path.ToLower() == input.Path.ToLower() && 
                           a.HttpMethod.ToUpper() == input.HttpMethod.ToUpper())
                .FirstOrDefaultAsync();

            if (existingEndpoint != null)
            {
                throw new UserFriendlyException(ErrorCode.ApiEndpointAlreadyExists);
            }

            var endpoint = new ApiEndpoint
            {
                Path = input.Path,
                HttpMethod = input.HttpMethod.ToUpper(),
                Description = input.Description,
                RequireAuthentication = input.RequireAuthentication
            };

            _dbContext.ApiEndpoints.Add(endpoint);
            await _dbContext.SaveChangesAsync();
            
            // Clear cache
            _cache.Remove(API_ENDPOINTS_CACHE_KEY);
        }

        public async Task Update(int id, CreateApiEndpointDto input)
        {
            _logger.LogInformation($"{nameof(Update)}: id = {id}");

            var endpoint = await _dbContext.ApiEndpoints
                .Where(a => !a.Deleted && a.Id == id)
                .FirstOrDefaultAsync()
                ?? throw new UserFriendlyException(ErrorCode.ApiEndpointNotFound);

            // Check for conflicts with other endpoints
            var existingEndpoint = await _dbContext.ApiEndpoints
                .Where(a => !a.Deleted && 
                           a.Id != id &&
                           a.Path.ToLower() == input.Path.ToLower() && 
                           a.HttpMethod.ToUpper() == input.HttpMethod.ToUpper())
                .FirstOrDefaultAsync();

            if (existingEndpoint != null)
            {
                throw new UserFriendlyException(ErrorCode.ApiEndpointAlreadyExists);
            }

            endpoint.Path = input.Path;
            endpoint.HttpMethod = input.HttpMethod.ToUpper();
            endpoint.Description = input.Description;
            endpoint.RequireAuthentication = input.RequireAuthentication;

            await _dbContext.SaveChangesAsync();
            
            // Clear cache
            _cache.Remove(API_ENDPOINTS_CACHE_KEY);
        }

        public async Task Delete(int id)
        {
            _logger.LogInformation($"{nameof(Delete)}: id = {id}");

            var endpoint = await _dbContext.ApiEndpoints
                .Where(a => !a.Deleted && a.Id == id)
                .FirstOrDefaultAsync()
                ?? throw new UserFriendlyException(ErrorCode.ApiEndpointNotFound);

            endpoint.Deleted = true;
            await _dbContext.SaveChangesAsync();
            
            // Clear cache
            _cache.Remove(API_ENDPOINTS_CACHE_KEY);
        }
    }
}