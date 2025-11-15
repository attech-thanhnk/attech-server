using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Setting;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.WebAPIBase;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Memory;

namespace AttechServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingController : ApiControllerBase
    {
        private readonly IAttachmentService _attachmentService;
        private readonly IMemoryCache _cache;

        public SettingController(IAttachmentService attachmentService, IMemoryCache cache, ILogger<SettingController> logger)
            : base(logger)
        {
            _attachmentService = attachmentService;
            _cache = cache;
        }

        /// <summary>
        /// Upload file cho bất kỳ setting nào (banner1, banner2, logo, etc.)
        /// </summary>
        [HttpPost("{settingKey}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadSetting(
            [FromRoute] string settingKey,
            [FromForm] UploadSettingDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(settingKey))
                {
                    return BadRequest("Setting key không hợp lệ");
                }

                if (dto?.File == null || dto.File.Length == 0)
                {
                    return BadRequest("File không hợp lệ");
                }

                if (!dto.File.ContentType.StartsWith("image/"))
                {
                    return BadRequest("Chỉ chấp nhận file ảnh");
                }

                // Chuyển setting key thành objectId
                var objectId = settingKey.ToObjectId();

                _logger.LogInformation($"Uploading {settingKey} file: {dto.File.FileName}, objectId: {objectId}");

                // Upload temp file
                var tempAttachment = await _attachmentService.UploadTempAsync(dto.File, "image");

                // Associate với setting
                await _attachmentService.AssociateAttachmentsAsync(
                    new List<int> { tempAttachment.Id },
                    ObjectType.Setting,
                    objectId: objectId,
                    isFeaturedImage: true,
                    isContentImage: false
                );

                // Xóa cache public settings khi có thay đổi
                _cache.Remove("public_settings");
                _logger.LogInformation("Public settings cache invalidated after upload");

                _logger.LogInformation($"{settingKey} uploaded successfully: {tempAttachment.Url}");

                return Ok(new {
                    success = true,
                    message = $"{settingKey} đã được cập nhật thành công",
                    settingKey = settingKey,
                    url = tempAttachment.Url,
                    id = tempAttachment.Id,
                    fileName = tempAttachment.FileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading {settingKey}");
                return StatusCode(500, $"Có lỗi xảy ra khi upload {settingKey}");
            }
        }

        /// <summary>
        /// Lấy file của một setting cụ thể
        /// </summary>
        [HttpGet("{settingKey}")]
        public async Task<IActionResult> GetSetting(string settingKey)
        {
            try
            {
                if (string.IsNullOrEmpty(settingKey))
                {
                    return BadRequest("Setting key không hợp lệ");
                }

                var objectId = settingKey.ToObjectId();
                var attachments = await _attachmentService.GetByEntityAsync(ObjectType.Setting, objectId);
                var attachment = attachments.FirstOrDefault(a => a.IsPrimary);
                
                if (attachment == null)
                {
                    return Ok(new { 
                        settingKey = settingKey,
                        url = (string?)null, 
                        message = $"Chưa có {settingKey}" 
                    });
                }
                
                return Ok(new { 
                    settingKey = settingKey,
                    url = attachment.Url,
                    id = attachment.Id,
                    fileName = attachment.OriginalFileName,
                    fileSize = attachment.FileSize,
                    uploadDate = attachment.CreatedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {settingKey}");
                return StatusCode(500, $"Có lỗi xảy ra khi lấy {settingKey}");
            }
        }

        /// <summary>
        /// Lấy tất cả settings đã được định nghĩa (cần đăng nhập)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            try
            {
                var settings = new Dictionary<string, object?>();

                // Lấy tất cả enum values
                var settingTypes = Enum.GetValues<SettingType>().Where(s => s != SettingType.Custom);

                foreach (var settingType in settingTypes)
                {
                    var settingKey = settingType.ToString();
                    var objectId = (int)settingType;

                    var attachments = await _attachmentService.GetByEntityAsync(ObjectType.Setting, objectId);
                    var attachment = attachments.FirstOrDefault(a => a.IsPrimary);

                    if (attachment != null)
                    {
                        settings[settingKey] = new
                        {
                            url = attachment.Url,
                            id = attachment.Id,
                            fileName = attachment.OriginalFileName,
                            uploadDate = attachment.CreatedDate,
                            description = settingType.GetDescription()
                        };
                    }
                    else
                    {
                        settings[settingKey] = null;
                    }
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all settings");
                return StatusCode(500, "Có lỗi xảy ra khi lấy settings");
            }
        }

        /// <summary>
        /// Lấy tất cả settings public (không cần đăng nhập) - cho Frontend
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicSettings()
        {
            try
            {
                // Cache key cho public settings
                var cacheKey = "public_settings";

                // Kiểm tra cache trước
                if (_cache.TryGetValue(cacheKey, out Dictionary<string, object?>? cachedSettings))
                {
                    _logger.LogInformation("Public settings loaded from cache");
                    return Ok(cachedSettings);
                }

                _logger.LogInformation("Public settings cache miss - loading from database");

                var settings = new Dictionary<string, object?>();

                // Chỉ lấy những setting public cần thiết cho Frontend
                var publicSettingTypes = new[]
                {
                    // Main Banners (Carousel)
                    SettingType.Banner1, SettingType.Banner2, SettingType.Banner3,

                    // Logo
                    SettingType.Logo, SettingType.Favicon,

                    // Home Page
                    SettingType.HomeHeroBackground,

                    // Feature Backgrounds
                    SettingType.HomeFeatCns, SettingType.HomeFeatBhc, SettingType.HomeFeatCnhk,

                    // Fact Event Image
                    SettingType.HomeFactEvent,

                    // About CNS/ATM Gallery
                    SettingType.AboutCns1, SettingType.AboutCns2, SettingType.AboutCns3,
                    SettingType.AboutCns4, SettingType.AboutCns5, SettingType.AboutCns6,

                    // About BHC Gallery
                    SettingType.AboutBhc1, SettingType.AboutBhc2, SettingType.AboutBhc3,
                    SettingType.AboutBhc4, SettingType.AboutBhc5,

                    // About CNHK Gallery
                    SettingType.AboutCnhk1, SettingType.AboutCnhk2, SettingType.AboutCnhk3,
                    SettingType.AboutCnhk4, SettingType.AboutCnhk5, SettingType.AboutCnhk6,
                    SettingType.AboutCnhk7, SettingType.AboutCnhk8,

                    // Structure/Organization
                    SettingType.StructureChart,

                    // Leadership
                    SettingType.LeaderChairman, SettingType.LeaderDirector,
                    SettingType.LeaderViceDirector1, SettingType.LeaderViceDirector2,
                    SettingType.LeaderViceDirector3
                };

                // Tối ưu: Load tất cả attachments 1 lần thay vì N lần
                var objectIds = publicSettingTypes.Select(s => (int)s).ToList();
                var attachmentsDict = await _attachmentService.GetPrimaryAttachmentsByObjectIdsAsync(ObjectType.Setting, objectIds);

                foreach (var settingType in publicSettingTypes)
                {
                    var settingKey = settingType.ToString();
                    var objectId = (int)settingType;

                    var attachment = attachmentsDict.GetValueOrDefault(objectId);

                    if (attachment != null)
                    {
                        settings[settingKey] = new
                        {
                            url = attachment.Url,
                            description = settingType.GetDescription()
                        };
                    }
                    else
                    {
                        settings[settingKey] = null;
                    }
                }

                // Cache kết quả 30 phút
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, settings, cacheOptions);
                _logger.LogInformation("Public settings cached for 30 minutes");

                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public settings");
                return StatusCode(500, "Có lỗi xảy ra khi lấy settings");
            }
        }

        /// <summary>
        /// Lấy nhiều settings cùng lúc
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> GetMultipleSettings([FromBody] List<string> settingKeys)
        {
            try
            {
                var result = new Dictionary<string, object?>();

                foreach (var settingKey in settingKeys)
                {
                    var objectId = settingKey.ToObjectId();
                    var attachments = await _attachmentService.GetByEntityAsync(ObjectType.Setting, objectId);
                    var attachment = attachments.FirstOrDefault(a => a.IsPrimary);
                    
                    if (attachment != null)
                    {
                        result[settingKey] = new
                        {
                            url = attachment.Url,
                            id = attachment.Id,
                            fileName = attachment.OriginalFileName,
                            uploadDate = attachment.CreatedDate
                        };
                    }
                    else
                    {
                        result[settingKey] = null;
                    }
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple settings");
                return StatusCode(500, "Có lỗi xảy ra khi lấy settings");
            }
        }

        /// <summary>
        /// Xóa một setting
        /// </summary>
        [HttpDelete("{settingKey}")]
        public async Task<IActionResult> DeleteSetting(string settingKey)
        {
            try
            {
                if (string.IsNullOrEmpty(settingKey))
                {
                    return BadRequest("Setting key không hợp lệ");
                }

                var objectId = settingKey.ToObjectId();
                await _attachmentService.SoftDeleteEntityAttachmentsAsync(ObjectType.Setting, objectId);

                // Xóa cache public settings khi có thay đổi
                _cache.Remove("public_settings");
                _logger.LogInformation("Public settings cache invalidated after delete");

                return Ok(new {
                    success = true,
                    message = $"{settingKey} đã được xóa",
                    settingKey = settingKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting {settingKey}");
                return StatusCode(500, $"Có lỗi xảy ra khi xóa {settingKey}");
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả setting types có thể dùng
        /// </summary>
        [HttpGet("types")]
        public IActionResult GetSettingTypes()
        {
            var settingTypes = Enum.GetValues<SettingType>()
                .Where(s => s != SettingType.Custom)
                .Select(s => new
                {
                    key = s.ToString(),
                    value = (int)s,
                    description = s.GetDescription()
                })
                .ToList();

            return Ok(settingTypes);
        }
    }
}