using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Domains.Entities.Main;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IAttachmentService
    {
        // Core methods for new flow
        Task<TempAttachmentResponseDto> UploadTempAsync(IFormFile file, string relationType = "image");
        Task<bool> AssociateAttachmentsAsync(List<int> attachmentIds, ObjectType objectType, int objectId, bool isFeaturedImage = false, bool isContentImage = false);
        Task<Attachment?> GetByIdAsync(int id);
        Task<bool> DeleteAsync(int id);
        Task<List<Attachment>> GetByEntityAsync(ObjectType objectType, int objectId);
        Task<Dictionary<int, Attachment?>> GetPrimaryAttachmentsByObjectIdsAsync(ObjectType objectType, List<int> objectIds);

        // Utility methods
        Task<bool> CleanupTempFilesAsync();
        Task<List<int>> ExtractAttachmentIdsFromContentAsync(string htmlContent);
        Task SoftDeleteEntityAttachmentsAsync(ObjectType objectType, int objectId);
        Task SoftDeleteAttachmentsByIdsAsync(List<int> attachmentIds);
        Task<List<int>> GetCurrentGalleryAttachmentIdsAsync(ObjectType objectType, int objectId);
        Task<int?> GetCurrentFeaturedImageIdAsync(ObjectType objectType, int objectId);
    }
}