using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.User;
using AttechServer.Domains.Entities;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AttechServer.Shared.Consts;

namespace AttechServer.Applications.UserModules.Implements
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContext;

        public UserService(ILogger<UserService> logger, ApplicationDbContext dbContext, IHttpContextAccessor httpContext)
        {
            _logger = logger;
            _dbContext = dbContext;
            _httpContext = httpContext;
        }


        public async Task<PagingResult<UserDto>> FindAll(UserPagingRequestDto input)
        {
            _logger.LogInformation($"{nameof(FindAll)}: input = {JsonSerializer.Serialize(input)}");

            // Get current user's role level for permission checking
            var currentUserLevel = _httpContext.GetCurrentUserLevel();
            
            var baseQuery = _dbContext.Users.AsNoTracking()
                .Join(_dbContext.Roles, u => u.RoleId, r => r.Id, (u, r) => new { User = u, Role = r })
                .Where(ur => !ur.User.Deleted
                    && (string.IsNullOrEmpty(input.Keyword) || ur.User.Username.Contains(input.Keyword))
                    && (input.RoleId == null || ur.User.RoleId == input.RoleId));

            // Apply role-based filtering based on current user's permission
            // SuperAdmin (1) can see all users
            // Admin (2) can only see Editor (3) and themselves
            if (currentUserLevel == 2) // Admin
            {
                baseQuery = baseQuery.Where(ur => ur.User.RoleId >= 2); // Admin and Editor only
            }

            var totalItems = await baseQuery.CountAsync();

            var pagedItems = await baseQuery
                .OrderBy(ur => ur.User.Id)
                .Skip(input.GetSkip())
                .Take(input.PageSize)
                .Select(ur => new UserDto
                {
                    Id = ur.User.Id,
                    Username = ur.User.Username,
                    FullName = ur.User.FullName,
                    Email = ur.User.Email,
                    Phone = ur.User.Phone,
                    LastLogin = ur.User.LastLogin,
                    Status = ur.User.Status == CommonStatus.ACTIVE ? "active" : "inactive",
                    RoleId = ur.User.RoleId,
                    RoleName = ur.Role.Name
                })
                .ToListAsync();

            return new PagingResult<UserDto>
            {
                TotalItems = totalItems,
                Items = pagedItems
            };
        }

        public async Task<UserDto> FindById(int id)
        {
            _logger.LogInformation($"{nameof(FindById)}: id = {id}");

            var result = await _dbContext.Users
                .Join(_dbContext.Roles, u => u.RoleId, r => r.Id, (u, r) => new { User = u, Role = r })
                .FirstOrDefaultAsync(ur => !ur.User.Deleted && ur.User.Id == id)
                ?? throw new UserFriendlyException(ErrorCode.UserNotFound);

            return new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                FullName = result.User.FullName,
                Email = result.User.Email,
                Phone = result.User.Phone,
                Status = result.User.Status == CommonStatus.ACTIVE ? "active" : "inactive",
                RoleId = result.User.RoleId,
                RoleName = result.Role.Name,
                LastLogin = result.User.LastLogin
            };
        }

        public async Task Update(UpdateUserDto input)
        {
            _logger.LogInformation($"{nameof(Update)}: input = {JsonSerializer.Serialize(input)}");

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => !u.Deleted && u.Id == input.Id)
                ?? throw new UserFriendlyException(ErrorCode.UserNotFound);

            var currentUserLevel = _httpContext.GetCurrentUserLevel();
            
            // Role hierarchy validation: 1=superadmin, 2=admin, 3=editor
            // SuperAdmin (1): can modify anyone
            // Admin (2): can only modify editors (3)
            // Editor (3): cannot modify anyone
            
            if (user.RoleId <= currentUserLevel && user.Id != _httpContext.GetCurrentUserId())
            {
                throw new UserFriendlyException(ErrorCode.AccessDenied);
            }

            // Cannot assign higher role than current user
            if (input.RoleId < currentUserLevel)
            {
                throw new UserFriendlyException(ErrorCode.AccessDenied);
            }

            // Check if username is already taken by another user
            if (await _dbContext.Users.AnyAsync(u => !u.Deleted && u.Username == input.Username && u.Id != input.Id))
            {
                throw new UserFriendlyException(ErrorCode.UsernameIsExist);
            }

            // Check if email is already taken by another user
            if (!string.IsNullOrEmpty(input.Email))
            {
                if (await _dbContext.Users.AnyAsync(u => !u.Deleted && u.Email == input.Email && u.Id != input.Id))
                {
                    throw new UserFriendlyException(ErrorCode.EmailIsExist);
                }
            }

            user.Username = input.Username;
            user.FullName = input.FullName;
            user.Email = input.Email;
            user.Phone = input.Phone;
            user.Status = input.Status;
            user.RoleId = input.RoleId;

            await _dbContext.SaveChangesAsync();
        }

        public async Task Delete(int id)
        {
            _logger.LogInformation($"{nameof(Delete)}: id = {id}");

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => !u.Deleted && u.Id == id)
                ?? throw new UserFriendlyException(ErrorCode.UserNotFound);

            // B?o v? theo Strict Hierarchy
            var currentUserLevel = _httpContext.GetCurrentUserLevel();
            var currentUserId = _httpContext.GetCurrentUserId();
            
            // Role hierarchy validation: 1=superadmin, 2=admin, 3=editor
            if (user.RoleId <= currentUserLevel)
            {
                throw new UserFriendlyException(ErrorCode.AccessDenied);
            }

            // Kh�ng cho ph�p t? x�a ch�nh m�nh (t?t c? user levels)
            if (user.Id == currentUserId)
            {
                throw new UserFriendlyException(ErrorCode.AccessDenied);
            }

            user.Deleted = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
