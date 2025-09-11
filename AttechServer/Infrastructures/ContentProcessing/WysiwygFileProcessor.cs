using AngleSharp;
using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Abstractions;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using Microsoft.EntityFrameworkCore;

namespace AttechServer.Infrastructures.ContentProcessing
{
    public class WysiwygFileProcessor : IWysiwygFileProcessor
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WysiwygFileProcessor> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IAttachmentService _attachmentService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public WysiwygFileProcessor(
            ApplicationDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<WysiwygFileProcessor> logger,
            IWebHostEnvironment env,
            IAttachmentService attachmentService,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<(string ProcessedContent, List<FileEntry> FileEntries)> ProcessContentAsync(string content, ObjectType objectType, int objectId)
        {
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Content is null or empty.");
                return (content, new List<FileEntry>());
            }

            // Validate content size (max 10MB processed content)
            if (content.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning($"Content too large: {content.Length} bytes");
                throw new InvalidOperationException("Content size exceeds 10MB limit");
            }

            var fileEntries = new List<FileEntry>();

            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(content));

            var elementsWithSrc = document.QuerySelectorAll("img[src], video[src], source[src], a[href], embed[src], iframe[src]")
                .Where(e => e.GetAttribute("src")?.Contains("/uploads/temp/") == true ||
                            e.GetAttribute("href")?.Contains("/uploads/temp/") == true ||
                            e.HasAttribute("data-attachment-id"));

            foreach (var element in elementsWithSrc)
            {
                int attachmentId = 0;
                
                // Check if element has data-attachment-id attribute (from TinyMCE)
                var dataAttachmentId = element.GetAttribute("data-attachment-id");
                if (!string.IsNullOrEmpty(dataAttachmentId))
                {
                    if (!int.TryParse(dataAttachmentId, out attachmentId))
                    {
                        _logger.LogWarning($"Invalid data-attachment-id: {dataAttachmentId}");
                        continue;
                    }
                }
                else
                {
                    // Extract from temp URL: /uploads/temp/{filename}
                    var url = element.GetAttribute("src") ?? element.GetAttribute("href");
                    if (string.IsNullOrEmpty(url) || !url.Contains("/uploads/temp/")) continue;

                    var fileName = url.Split('/').LastOrDefault();
                    if (string.IsNullOrEmpty(fileName))
                    {
                        _logger.LogWarning($"Cannot extract filename from temp URL: {url}");
                        continue;
                    }

                    var tempAttachment = await _dbContext.Attachments
                        .FirstOrDefaultAsync(f => f.FilePath == $"temp/{fileName}" && f.IsTemporary && !f.Deleted);
                    
                    if (tempAttachment == null)
                    {
                        _logger.LogWarning($"Temp attachment not found for filename: {fileName}");
                        continue;
                    }
                    
                    // Use AttachmentService to move temp file to permanent location - mark as content image
                    await _attachmentService.AssociateAttachmentsAsync(new List<int> { tempAttachment.Id }, objectType, objectId, isFeaturedImage: false, isContentImage: true);
                    
                    // Reload the attachment to get updated FilePath after move
                    await _dbContext.Entry(tempAttachment).ReloadAsync();
                    
                    attachmentId = tempAttachment.Id;
                }

                var fileEntryInDb = await _dbContext.Attachments
                    .FirstOrDefaultAsync(f => f.Id == attachmentId && !f.Deleted);
                
                if (fileEntryInDb == null)
                {
                    _logger.LogWarning($"Attachment not found for ID: {attachmentId}");
                    continue;
                }

                var relativePath = fileEntryInDb.FilePath;
                var subFolder = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "files";
                fileEntries.Add(new FileEntry { SubFolder = subFolder, FilePath = relativePath });

                // Check storage mode configuration - default to relative
                var storageMode = _configuration["AppSettings:StorageMode"] ?? "relative";
                var staticUrl = $"/uploads/{relativePath}";
                
                // Only add full domain if StorageMode is set to "absolute"
                if (storageMode.Equals("absolute", StringComparison.OrdinalIgnoreCase))
                {
                    var originalUrl = element.GetAttribute("src") ?? element.GetAttribute("href");
                    if (!string.IsNullOrEmpty(originalUrl) && (originalUrl.StartsWith("http://") || originalUrl.StartsWith("https://")))
                    {
                        var uri = new Uri(originalUrl);
                        staticUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/uploads/{relativePath}";
                    }
                }
                
                if (element.TagName == "IMG" || element.TagName == "VIDEO")
                {
                    element.SetAttribute("src", staticUrl);
                    // Remove data-src and placeholder logic for public display
                    element.RemoveAttribute("data-src");
                    // Remove data-attachment-id after processing
                    element.RemoveAttribute("data-attachment-id");
                }
                else if (element.TagName == "A")
                {
                    element.SetAttribute("href", staticUrl);
                    element.RemoveAttribute("data-attachment-id");
                }
                else
                {
                    element.SetAttribute("src", staticUrl);
                    element.RemoveAttribute("data-attachment-id");
                }

                // Update file with entity information if not already set
                if (fileEntryInDb.ObjectType == null || fileEntryInDb.ObjectId == null)
                {
                    fileEntryInDb.ObjectType = objectType;
                    fileEntryInDb.ObjectId = objectId;
                    fileEntryInDb.RelationType = "content";
                    fileEntryInDb.IsPrimary = false;
                    fileEntryInDb.IsTemporary = false;
                }
            }

            await _dbContext.SaveChangesAsync();

            return (document.DocumentElement.OuterHtml, fileEntries);
        }

        public async Task<string> ReconstructContentAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Content is null or empty.");
                return content;
            }

            var domain = $"{_httpContextAccessor.HttpContext.Request.Scheme}://{_httpContextAccessor.HttpContext.Request.Host}";
            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(content));

            var elementsWithSrc = document.QuerySelectorAll("img[data-src], video[data-src], source[src], a[href], embed[src], iframe[src]");
            foreach (var element in elementsWithSrc)
            {
                var attrName = element.TagName == "A" ? "href" : element.HasAttribute("data-src") ? "data-src" : "src";
                var relativePath = element.GetAttribute(attrName);
                
                // Skip if already has full URL
                if (relativePath?.Contains("/api/attachments/") == true) 
                    continue;

                if (!string.IsNullOrEmpty(relativePath))
                {
                    // Find attachment by file path to get ID
                    var attachment = await _dbContext.Attachments
                        .FirstOrDefaultAsync(f => f.FilePath == relativePath && !f.Deleted);
                    
                    if (attachment == null)
                    {
                        _logger.LogWarning($"Attachment not found for file path: {relativePath}");
                        continue;
                    }

                    var fullUrl = $"{domain}/api/attachments/{attachment.Id}";

                    if (element.HasAttribute("data-src"))
                    {
                        element.SetAttribute("data-src", fullUrl);
                        element.SetAttribute("src", "placeholder.jpg");
                    }
                    else if (element.TagName == "A")
                    {
                        element.SetAttribute("href", fullUrl);
                    }
                    else
                    {
                        element.SetAttribute("src", fullUrl);
                    }
                }
            }

            return document.DocumentElement.OuterHtml;
        }

        public async Task DeleteFilesAsync(ObjectType objectType, int objectId)
        {
            // Get files directly by entity
            var files = await _dbContext.Attachments
                .Where(f => f.ObjectType == objectType && f.ObjectId == objectId && !f.Deleted)
                .ToListAsync();

            foreach (var file in files)
            {
                var filePath = Path.Combine(_env.ContentRootPath, "Uploads", file.FilePath);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation($"Deleted file: {filePath}");
                }
                
                // Soft delete
                file.Deleted = true;
                file.ModifiedDate = DateTime.Now;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Deleted {files.Count} files for {objectType} ID {objectId}");
        }

        public async Task ProcessAttachmentsAsync(string content, ObjectType objectType, int objectId)
        {
            if (string.IsNullOrEmpty(content))
                return;

            // Extract attachment IDs from content
            var attachmentIds = await _attachmentService.ExtractAttachmentIdsFromContentAsync(content);
            
            if (attachmentIds.Any())
            {
                // Associate attachments with the entity (these are content attachments)
                await _attachmentService.AssociateAttachmentsAsync(attachmentIds, objectType, objectId, isFeaturedImage: false, isContentImage: true);
            }
        }

        /// <summary>
        /// Extract attachment IDs from HTML content
        /// </summary>
        public async Task<List<int>> ExtractAttachmentIdsAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new List<int>();

            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(content));

            var attachmentIds = new List<int>();

            // Find all elements with data-attachment-id
            var elementsWithAttachmentId = document.QuerySelectorAll("[data-attachment-id]");
            foreach (var element in elementsWithAttachmentId)
            {
                var attachmentIdStr = element.GetAttribute("data-attachment-id");
                if (int.TryParse(attachmentIdStr, out var attachmentId))
                {
                    attachmentIds.Add(attachmentId);
                }
            }

            return attachmentIds.Distinct().ToList();
        }

        /// <summary>
        /// Extract unique attachment IDs from both ContentVi and ContentEn
        /// </summary>
        public async Task<List<int>> ExtractUniqueAttachmentIdsAsync(string contentVi, string contentEn)
        {
            var idsVi = await ExtractAttachmentIdsAsync(contentVi);
            var idsEn = await ExtractAttachmentIdsAsync(contentEn);
            
            return idsVi.Union(idsEn).Distinct().ToList();
        }

        /// <summary>
        /// Check if content contains temp files
        /// </summary>
        public bool HasTempFilesInContent(string content)
        {
            return !string.IsNullOrWhiteSpace(content) && content.Contains("/uploads/temp/");
        }
    }
}
