using AttechServer.Applications.UserModules.Dtos.InternalDocument;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IInternalDocumentService
    {
        /// <summary>
        /// Lấy danh sách tất cả internal documents với phân trang
        /// </summary>
        Task<PagingResult<InternalDocumentDto>> FindAll(InternalDocumentPagingRequestDto input);

        /// <summary>
        /// Lấy danh sách internal documents theo category với phân trang
        /// </summary>
        Task<PagingResult<InternalDocumentDto>> FindAllByCategory(InternalDocumentPagingRequestDto input, string category);

        /// <summary>
        /// Lấy thông tin chi tiết internal document theo Id
        /// </summary>
        Task<DetailInternalDocumentDto> FindById(int id);

        /// <summary>
        /// Thêm mới internal document
        /// </summary>
        Task<InternalDocumentDto> Create(CreateInternalDocumentDto input);

        /// <summary>
        /// Cập nhật internal document
        /// </summary>
        Task<InternalDocumentDto> Update(int id, UpdateInternalDocumentDto input);

        /// <summary>
        /// Xóa internal document
        /// </summary>
        Task Delete(int id);

        /// <summary>
        /// Lấy danh sách internal documents đã publish cho client (status = 1)
        /// </summary>
        Task<PagingResult<InternalDocumentDto>> GetPublishedDocumentsForClient(InternalDocumentPagingRequestDto input);

        /// <summary>
        /// Lấy danh sách internal documents đã publish theo category cho client
        /// </summary>
        Task<PagingResult<InternalDocumentDto>> GetPublishedDocumentsByCategoryForClient(string category, InternalDocumentPagingRequestDto input);

        /// <summary>
        /// Lấy chi tiết internal document đã publish cho client
        /// </summary>
        Task<DetailInternalDocumentDto> GetPublishedDocumentByIdForClient(int id);

        /// <summary>
        /// Lấy danh sách categories
        /// </summary>
        Task<List<string>> GetCategories();
    }
}