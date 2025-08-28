using AttechServer.Applications.UserModules.Dtos.News;
using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface INewsService
    {
        /// <summary>
        /// Lấy danh sách tất cả tin tức với phân trang
        /// </summary>
        Task<PagingResult<NewsDto>> FindAll(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy danh sách tin tức theo slug danh mục, bao gồm cả category chính và sub-categories
        /// </summary>
        Task<PagingResult<NewsDto>> FindAllByCategorySlug(PagingRequestBaseDto input, string slug);

        /// <summary>
        /// Lấy thông tin chi tiết tin tức theo Id
        /// </summary>
        Task<DetailNewsDto> FindById(int id);

        /// <summary>
        /// Lấy thông tin chi tiết tin tức theo slug (song ngữ)
        /// </summary>
        Task<DetailNewsDto> FindBySlug(string slug);

        /// <summary>
        /// Thêm mới tin tức với tất cả dữ liệu (text + files) trong một request
        /// </summary>
        Task<NewsDto> Create(CreateNewsDto input);


        /// <summary>
        /// Cập nhật tin tức
        /// ID được truyền riêng, không cần trong DTO
        /// </summary>
        Task<NewsDto> Update(int id, UpdateNewsDto input);


        /// <summary>
        /// Xóa tin tức
        /// </summary>
        Task Delete(int id);

        /// <summary>
        /// Tạo album (news với IsAlbum = true)
        /// </summary>
        Task<NewsDto> CreateAlbum(CreateAlbumDto input);

        /// <summary>
        /// Lấy danh sách albums (news với IsAlbum = true)
        /// </summary>
        Task<PagingResult<NewsDto>> GetAlbums(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy chi tiết album theo ID cho admin
        /// </summary>
        Task<AlbumDetailDto> GetAlbumByIdForAdmin(int id);

        /// <summary>
        /// Lấy chi tiết album theo slug
        /// </summary>
        Task<DetailNewsDto> FindAlbumBySlug(string slug);

        /// <summary>
        /// Lấy thông tin gallery của tin tức theo slug
        /// </summary>
        Task<NewsGalleryDto> GetGalleryBySlug(string slug);

        /// <summary>
        /// Cập nhật album (simplified version for albums only)
        /// </summary>
        Task<AlbumDetailDto> UpdateAlbum(int id, UpdateAlbumDto input);

        /// <summary>
        /// Lấy danh sách tin tức đã xuất bản (status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> FindAllForClient(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy thông tin chi tiết tin tức theo slug (chỉ status = 1) cho client
        /// </summary>
        Task<DetailNewsDto> FindBySlugForClient(string slug);

        /// <summary>
        /// Lấy danh sách tin tức theo slug danh mục (chỉ status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> FindAllByCategorySlugForClient(PagingRequestBaseDto input, string slug);

        /// <summary>
        /// Tìm kiếm tin tức đã xuất bản (chỉ status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> SearchForClient(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy danh sách tin tức nổi bật đã xuất bản (isOutstanding = true, status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> FindOutstandingForClient(PagingRequestBaseDto input);

        // Client Album methods
        /// <summary>
        /// Lấy danh sách albums đã xuất bản (chỉ status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetPublishedAlbumsForClient(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy chi tiết album đã xuất bản theo slug cho client
        /// </summary>
        Task<DetailNewsDto> GetPublishedAlbumBySlugForClient(string slug);

        /// <summary>
        /// Lấy gallery album cho client
        /// </summary>
        Task<NewsGalleryDto> GetAlbumGalleryForClient(string slug);

        /// <summary>
        /// Lấy albums theo category đã xuất bản cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetPublishedAlbumsByCategoryForClient(string categorySlug, PagingRequestBaseDto input);

        /// <summary>
        /// Lấy albums nổi bật đã xuất bản cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetFeaturedAlbumsForClient(PagingRequestBaseDto input);

        // Document methods (similar to Album methods)
        /// <summary>
        /// Tạo document (news với IsDocument = true)
        /// </summary>
        Task<NewsDto> CreateDocument(CreateDocumentDto input);

        /// <summary>
        /// Lấy danh sách documents (news với IsDocument = true)
        /// </summary>
        Task<PagingResult<NewsDto>> GetDocuments(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy chi tiết document theo ID cho admin
        /// </summary>
        Task<DocumentDetailDto> GetDocumentByIdForAdmin(int id);

        /// <summary>
        /// Cập nhật document (simplified version for documents only)
        /// </summary>
        Task<DocumentDetailDto> UpdateDocument(int id, UpdateDocumentDto input);

        /// <summary>
        /// Lấy danh sách documents đã xuất bản (chỉ status = 1) cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetPublishedDocumentsForClient(PagingRequestBaseDto input);

        /// <summary>
        /// Lấy chi tiết document đã xuất bản theo slug cho client
        /// </summary>
        Task<DocumentDetailDto> GetPublishedDocumentBySlugForClient(string slug);

        /// <summary>
        /// Lấy documents theo category đã xuất bản cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetPublishedDocumentsByCategoryForClient(string categorySlug, PagingRequestBaseDto input);

        /// <summary>
        /// Lấy documents nổi bật đã xuất bản cho client
        /// </summary>
        Task<PagingResult<NewsDto>> GetFeaturedDocumentsForClient(PagingRequestBaseDto input);
    }
}
