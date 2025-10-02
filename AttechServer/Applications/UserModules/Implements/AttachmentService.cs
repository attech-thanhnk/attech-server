using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using Microsoft.EntityFrameworkCore;

namespace AttechServer.Applications.UserModules.Implements
{
    public class AttachmentService : IAttachmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AttachmentService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<TempAttachmentResponseDto> UploadTempAsync(IFormFile file, string? relationType = null)
        {
            // Auto-detect relationType if not provided
            relationType = relationType ?? DetectRelationType(file.ContentType);
            
            // Upload to temp folder
            var tempPath = Path.Combine(_environment.ContentRootPath, "Uploads", "temp");
            Directory.CreateDirectory(tempPath);

            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(tempPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new Attachment
            {
                FilePath = $"temp/{fileName}",
                Url = $"/uploads/temp/{fileName}",
                OriginalFileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                ObjectType = null,
                ObjectId = null,
                RelationType = relationType,
                IsTemporary = true,
                IsPrimary = false,
                CreatedDate = DateTime.Now
            };

            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            return new TempAttachmentResponseDto
            {
                Id = attachment.Id,
                Url = attachment.Url,
                IsTemporary = true,
                FileName = attachment.OriginalFileName,
                FileSize = attachment.FileSize,
                ContentType = attachment.ContentType
            };
        }

        /// <summary>
        /// Auto-detect relation type based on file content type
        /// </summary>
        private static string DetectRelationType(string contentType)
        {
            return contentType switch
            {
                var ct when ct.StartsWith("image/") => "image",
                var ct when ct.StartsWith("video/") => "video", 
                var ct when ct.StartsWith("audio/") => "audio",
                "application/pdf" => "document",
                var ct when ct.StartsWith("application/") => "document",
                var ct when ct.StartsWith("text/") => "document",
                _ => "file" // Generic fallback
            };
        }

        /// <summary>
        /// Xác định loại thư mục dựa trên content type và relation type
        /// </summary>
        private static string DetermineFileCategory(string contentType, string relationType)
        {
            // Ưu tiên dựa trên content type
            if (contentType.StartsWith("image/"))
            {
                return "images";
            }
            
            if (contentType.StartsWith("video/"))
            {
                return "videos";
            }
            
            if (contentType == "application/pdf")
            {
                return "documents";
            }
            
            if (contentType.StartsWith("application/") || contentType.StartsWith("text/"))
            {
                return "documents";
            }
            
            // Fallback dựa trên relation type
            return relationType switch
            {
                "featured" => "images",
                "content" => "images", 
                "document" => "documents",
                "video" => "videos",
                _ => "files"
            };
        }

        public async Task<Attachment?> GetByIdAsync(int id)
        {
            return await _context.Attachments
                .Where(a => !a.Deleted)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        /// <summary>
        /// Move file from temp folder to permanent folder with yyyy/MM structure
        /// </summary>
        private async Task MoveFromTempToPermanentAsync(Attachment attachment)
        {
            if (!attachment.IsTemporary || !attachment.FilePath.StartsWith("temp/"))
                return;

            // Get file category based on content type
            var category = DetermineFileCategory(attachment.ContentType, attachment.RelationType);
            
            // Create permanent path with yyyy/MM structure
            var now = DateTime.Now;
            var permanentDir = Path.Combine(category, now.ToString("yyyy"), now.ToString("MM"));
            var permanentDirPath = Path.Combine(_environment.ContentRootPath, "Uploads", permanentDir);
            
            // Create directory if not exists
            Directory.CreateDirectory(permanentDirPath);
            
            // Generate new filename (keep original extension)
            var extension = Path.GetExtension(attachment.FilePath);
            var newFileName = $"{Guid.NewGuid()}{extension}";
            var newFilePath = Path.Combine(permanentDir, newFileName).Replace("\\", "/");
            
            // Move physical file
            var oldPhysicalPath = Path.Combine(_environment.ContentRootPath, "Uploads", attachment.FilePath);
            var newPhysicalPath = Path.Combine(_environment.ContentRootPath, "Uploads", newFilePath);
            
            if (File.Exists(oldPhysicalPath))
            {
                File.Move(oldPhysicalPath, newPhysicalPath);
            }
            
            // Update attachment record
            attachment.FilePath = newFilePath;
            attachment.Url = $"/uploads/{newFilePath}";
        }

        public async Task<bool> AssociateAttachmentsAsync(List<int> attachmentIds, ObjectType objectType, int objectId, bool isFeaturedImage = false, bool isContentImage = false)
        {
            var attachments = await _context.Attachments
                .Where(a => attachmentIds.Contains(a.Id) && a.IsTemporary && !a.Deleted)
                .ToListAsync();

            // If setting featured image, delete old featured images first (before processing any attachment)
            if (isFeaturedImage)
            {
                await SoftDeleteOldFeaturedImageAsync(objectType, objectId, attachmentIds);
            }

            foreach (var attachment in attachments)
            {
                // Move file from temp to permanent location
                await MoveFromTempToPermanentAsync(attachment);
                
                attachment.ObjectType = objectType;
                attachment.ObjectId = objectId;
                attachment.IsTemporary = false;
                attachment.IsPrimary = isFeaturedImage; // Set IsPrimary based on parameter
                attachment.IsContentImage = isContentImage; // Set IsContentImage based on parameter
                attachment.ModifiedDate = DateTime.Now;

                // Update entity ImageUrl and FeaturedImageId if this is the featured image
                if (isFeaturedImage)
                {
                    var imageUrl = $"/uploads/{attachment.FilePath}";
                    
                    switch (objectType)
                    {
                        case ObjectType.News:
                            var news = await _context.News.FindAsync(objectId);
                            if (news != null)
                            {
                                news.ImageUrl = imageUrl;
                                news.FeaturedImageId = attachment.Id;
                            }
                            break;
                        case ObjectType.Notification:
                            var notification = await _context.Notifications.FindAsync(objectId);
                            if (notification != null)
                            {
                                notification.ImageUrl = imageUrl;
                                notification.FeaturedImageId = attachment.Id;
                            }
                            break;
                        case ObjectType.Product:
                            var product = await _context.Products.FindAsync(objectId);
                            if (product != null)
                            {
                                product.ImageUrl = imageUrl;
                                product.FeaturedImageId = attachment.Id;
                            }
                            break;
                        case ObjectType.Service:
                            var service = await _context.Services.FindAsync(objectId);
                            if (service != null)
                            {
                                service.ImageUrl = imageUrl;
                                service.FeaturedImageId = attachment.Id;
                            }
                            break;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return attachments.Count > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var attachment = await GetByIdAsync(id);
            if (attachment == null) return false;

            var filePath = Path.Combine(_environment.ContentRootPath, "Uploads", attachment.FilePath);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            attachment.Deleted = true;
            attachment.ModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Attachment>> GetByEntityAsync(ObjectType objectType, int objectId)
        {
            return await _context.Attachments
                .AsNoTracking()
                .Where(a => a.ObjectType == objectType && a.ObjectId == objectId && !a.Deleted && !a.IsTemporary)
                .OrderBy(a => a.IsPrimary ? 0 : 1)
                .ThenBy(a => a.CreatedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy primary attachments cho nhiều objectIds cùng lúc (tối ưu cho N+1 query)
        /// </summary>
        public async Task<Dictionary<int, Attachment?>> GetPrimaryAttachmentsByObjectIdsAsync(ObjectType objectType, List<int> objectIds)
        {
            var attachments = await _context.Attachments
                .AsNoTracking()
                .Where(a => a.ObjectType == objectType &&
                           objectIds.Contains(a.ObjectId!.Value) &&
                           a.IsPrimary &&
                           !a.Deleted &&
                           !a.IsTemporary)
                .ToListAsync();

            // Tạo dictionary với key là objectId
            return objectIds.ToDictionary(
                objectId => objectId,
                objectId => attachments.FirstOrDefault(a => a.ObjectId == objectId)
            );
        }


        public async Task<bool> CleanupTempFilesAsync()
        {
            var cutoffDate = DateTime.Now.AddHours(-24); // Files older than 24 hours
            
            var tempAttachments = await _context.Attachments
                .Where(a => a.IsTemporary && a.CreatedDate < cutoffDate)
                .ToListAsync();

            foreach (var attachment in tempAttachments)
            {
                var filePath = Path.Combine(_environment.ContentRootPath, "Uploads", attachment.FilePath);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                attachment.Deleted = true;
                attachment.ModifiedDate = DateTime.Now;
            }

            if (tempAttachments.Any())
            {
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<List<int>> ExtractAttachmentIdsFromContentAsync(string htmlContent)
        {
            var attachmentIds = new List<int>();
            
            if (string.IsNullOrEmpty(htmlContent))
                return attachmentIds;

            // Extract data-attachment-id attributes from img tags
            var regex = new System.Text.RegularExpressions.Regex(@"data-attachment-id=['""](\d+)['""]");
            var matches = regex.Matches(htmlContent);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var attachmentId))
                {
                    attachmentIds.Add(attachmentId);
                }
            }

            return attachmentIds.Distinct().ToList();
        }

        /// <summary>
        /// Soft delete all attachments associated with an entity
        /// </summary>
        public async Task SoftDeleteEntityAttachmentsAsync(ObjectType objectType, int objectId)
        {
            var attachments = await _context.Attachments
                .Where(a => a.ObjectType == objectType && 
                           a.ObjectId == objectId && 
                           !a.Deleted)
                .ToListAsync();

            foreach (var attachment in attachments)
            {
                attachment.Deleted = true;
                attachment.ModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete old featured image (IsPrimary = true) for an entity, excluding specific attachment IDs
        /// </summary>
        private async Task SoftDeleteOldFeaturedImageAsync(ObjectType objectType, int objectId, List<int>? excludeAttachmentIds = null)
        {
            var query = _context.Attachments
                .Where(a => a.ObjectType == objectType && 
                           a.ObjectId == objectId && 
                           a.IsPrimary == true && 
                           !a.Deleted);

            if (excludeAttachmentIds != null && excludeAttachmentIds.Any())
            {
                query = query.Where(a => !excludeAttachmentIds.Contains(a.Id));
            }

            var oldFeaturedImages = await query.ToListAsync();

            foreach (var oldFeaturedImage in oldFeaturedImages)
            {
                oldFeaturedImage.Deleted = true;
                oldFeaturedImage.ModifiedDate = DateTime.Now;
            }
        }

        /// <summary>
        /// Soft delete specific attachments by IDs
        /// </summary>
        public async Task SoftDeleteAttachmentsByIdsAsync(List<int> attachmentIds)
        {
            var attachments = await _context.Attachments
                .Where(a => attachmentIds.Contains(a.Id) && !a.Deleted)
                .ToListAsync();

            foreach (var attachment in attachments)
            {
                attachment.Deleted = true;
                attachment.ModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get current attachment IDs for an entity (excluding featured image)
        /// </summary>
        public async Task<List<int>> GetCurrentGalleryAttachmentIdsAsync(ObjectType objectType, int objectId)
        {
            return await _context.Attachments
                .Where(a => a.ObjectType == objectType && 
                           a.ObjectId == objectId && 
                           !a.Deleted && 
                           !a.IsPrimary) // Gallery attachments only (not featured image)
                .Select(a => a.Id)
                .OrderBy(id => id)
                .ToListAsync();
        }

        /// <summary>
        /// Get current featured image ID for an entity
        /// </summary>
        public async Task<int?> GetCurrentFeaturedImageIdAsync(ObjectType objectType, int objectId)
        {
            return await _context.Attachments
                .Where(a => a.ObjectType == objectType && 
                           a.ObjectId == objectId && 
                           !a.Deleted && 
                           a.IsPrimary) // Featured image only
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();
        }
    }
}