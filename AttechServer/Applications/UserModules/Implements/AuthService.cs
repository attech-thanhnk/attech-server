using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos;
using AttechServer.Applications.UserModules.Dtos.User;
using AttechServer.Domains.Entities;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Exceptions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using AttechServer.Shared.Utils;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using AttechServer.Shared.Consts;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Implements
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContext;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthService> logger, IHttpContextAccessor httpContext)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpContext = httpContext;
        }
        public async Task<LoginResponseDto> Login(UserLoginDto userInput)
        {
            try
            {
                _logger.LogInformation($"{nameof(Login)}: input = {JsonSerializer.Serialize(userInput)}");
                var user = _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(x => x.Username == userInput.Username);
                if (user == null)
                {
                    throw new UserFriendlyException(ErrorCode.UserNotFound);
                }
                if (!PasswordHasher.VerifyPassword(userInput.Password, user.Password))
                {
                    throw new UserFriendlyException(ErrorCode.PasswordWrong);
                }

                _logger.LogInformation("Creating JWT token...");
                var newAccessToken = await CreateJwt(userInput);
                _logger.LogInformation("JWT token created successfully");

                _logger.LogInformation("Creating refresh token...");
                var newRefreshToken = CreateRefreshToken();
                _logger.LogInformation("Refresh token created successfully");

                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
                user.LastLogin = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Getting user permissions...");
                var permissions = await GetUserPermissions(user.Id);
                _logger.LogInformation($"Found {permissions?.Count ?? 0} permissions for user");

                return new LoginResponseDto
                {
                    success = true,
                    token = newAccessToken,
                    user = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName,
                        Email = user.Email,
                        Phone = user.Phone,
                        RoleId = user.RoleId,
                        RoleName = user.RoleId switch
                        {
                            1 => "superadmin",
                            2 => "admin",
                            3 => "editor",
                            4 => "user",
                            _ => "unknown"
                        },
                        Status = user.Status == CommonStatus.ACTIVE ? "active" : "inactive",
                        LastLogin = user.LastLogin
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login process");
                throw;
            }
        }
        public void RegisterUser(CreateUserDto user)
        {
            _logger.LogInformation($"{nameof(RegisterUser)}: input = {JsonSerializer.Serialize(user)}");

            var currentUserLevel = _httpContext.GetCurrentUserLevel();
            
            // Role hierarchy validation: 1=superadmin, 2=admin, 3=editor
            if (user.RoleId <= currentUserLevel)
            {
                throw new UserFriendlyException(ErrorCode.AccessDenied);
            }

            var check = _context.Users.FirstOrDefault(x => x.Username == user.Username);
            if (check != null)
            {
                throw new UserFriendlyException(ErrorCode.UsernameIsExist);
            }
            
            // Check if email is already taken
            if (!string.IsNullOrEmpty(user.Email))
            {
                var emailCheck = _context.Users.FirstOrDefault(x => x.Email == user.Email);
                if (emailCheck != null)
                {
                    throw new UserFriendlyException(ErrorCode.EmailIsExist);
                }
            }
            if (user.Password.Length < 6)
            {
                throw new UserFriendlyException(ErrorCode.PasswordMustBeLongerThanSixCharacter);
            }
            if (!(Regex.IsMatch(user.Password, "[a-z]") && Regex.IsMatch(user.Password, "[A-Z]") && Regex.IsMatch(user.Password, "[0-9]")))
            {
                throw new UserFriendlyException(ErrorCode.TypeofPasswordMustBeNumberOrString);
            }
            if (!Regex.IsMatch(user.Password, "[<,>,@,!,#,$,%,^,&,*,(,),_,+,\\[,\\],{,},?,:,;,|,',\\,.,/,~,`,-,=]"))
                throw new UserFriendlyException(ErrorCode.PasswordMustBeContainsSpecifyCharacter);

            var newUser = new User
            {
                Username = user.Username,
                Password = PasswordHasher.HashPassword(user.Password),
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                Status = user.Status
            };
            
            _context.Users.Add(newUser);
            _context.SaveChanges();
            
        }
        public async Task<TokenApiDto> RefreshToken(TokenApiDto input)
        {
            if (input is null)
                throw new UserFriendlyException(ErrorCode.InvalidClientRequest);

            string accessToken = input.AccessToken;
            string refreshToken = input.RefreshToken;

            var principal = GetPrincipleFromExpiredToken(accessToken);
            var username = principal.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user is null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.Now)
                throw new UserFriendlyException(ErrorCode.InvalidRefreshToken);

            var newAccessToken = await CreateJwt(new UserLoginDto { Username = user.Username, Password = user.Password });
            var newRefreshToken = CreateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            _context.SaveChangesAsync();

            return new TokenApiDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }
        private async Task<string> CreateJwt(UserLoginDto user)
        {
            try
            {
                _logger.LogInformation("Starting JWT token creation...");
                var jwtToken = new JwtSecurityTokenHandler();
                var userId = _context.Users.FirstOrDefault(u => u.Username == user.Username) ?? throw new UserFriendlyException(ErrorCode.UserNotFound);

                _logger.LogInformation("Getting JWT key...");
                var key = Encoding.UTF8.GetBytes(_configuration.GetSection("JWT")["Key"]!);
                _logger.LogInformation("JWT key retrieved successfully");

                _logger.LogInformation("Getting user permissions for JWT...");
                var permissions = await GetUserPermissions(userId.Id);
                _logger.LogInformation($"Found {permissions.Count} permissions for JWT");

                var claims = new List<Claim> {
                new Claim(JwtRegisteredClaimNames.Sub, $"{userId.Id}"),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("user_level", userId.RoleId.ToString()),
                new Claim("user_id", userId.Id.ToString())
            };

                // Add permissions to JWT token
                foreach (var permission in permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }

                _logger.LogInformation($"Claims created successfully with {claims.Count} total claims");

                var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
                _logger.LogInformation("Signing credentials created successfully");

                var token = new JwtSecurityToken(
                    issuer: _configuration.GetSection("JWT")["Issuer"] ?? "AttechServer",
                    audience: _configuration.GetSection("JWT")["Audience"] ?? "AttechServer",
                    expires: DateTime.Now.AddHours(1),
                    claims: claims,
                    signingCredentials: credentials
                );
                _logger.LogInformation("JWT token object created successfully");

                var tokenString = jwtToken.WriteToken(token);
                _logger.LogInformation("JWT token string created successfully");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during JWT token creation");
                throw;
            }
        }
        private string CreateRefreshToken()
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var refreshToken = Convert.ToBase64String(tokenBytes);
            var tokenInUser = _context.Users.Any(a => a.RefreshToken == refreshToken);
            if (tokenInUser)
            {
                return CreateRefreshToken();
            }
            return refreshToken;
        }
        private ClaimsPrincipal GetPrincipleFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("JWT")["Key"]!)),
                ValidateLifetime = false
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken securityToken;
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new UserFriendlyException(ErrorCode.LoginExpired);
            return principal;
        }

        public async Task<UserDto?> GetUserWithPermissionsAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Id == userId && !u.Deleted)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return null;

                var permissions = await GetUserPermissions(userId);

                return new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    RoleId = user.RoleId,
                    RoleName = user.Role.Name,
                    Status = user.Status == CommonStatus.ACTIVE ? "active" : "inactive",
                    LastLogin = user.LastLogin
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user with permissions for user {userId}");
                return null;
            }
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            try
            {
                _logger.LogInformation($"{nameof(ChangePasswordAsync)}: Changing password for user {userId}");
                
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);

                if (user == null)
                {
                    throw new UserFriendlyException(ErrorCode.UserNotFound);
                }

                if (!PasswordHasher.VerifyPassword(changePasswordDto.CurrentPassword, user.Password))
                {
                    throw new UserFriendlyException(ErrorCode.PasswordWrong);
                }

                user.Password = PasswordHasher.HashPassword(changePasswordDto.NewPassword);
                user.ModifiedDate = DateTime.Now;
                user.ModifiedBy = userId;

                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Password changed successfully for user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                throw;
            }
        }

        private async Task<List<string>> GetUserPermissions(int userId)
        {
            try
            {
                // Với hệ thống mới chỉ dùng Role, trả về danh sách role names thay vì permissions
                var roles = await _context.Users
                    .Where(u => u.Id == userId && !u.Deleted)
                    .Select(u => u.Role.Name)
                    .ToListAsync();

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting roles for user {userId}");
                return new List<string>();
            }
        }
    }
}
