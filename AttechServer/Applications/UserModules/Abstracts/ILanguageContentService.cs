using AttechServer.Applications.UserModules.Dtos.LanguageContent;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface ILanguageContentService
    {
        Task<PagingResult<LanguageContentDto>> FindAll(PagingRequestBaseDto input);
        Task<LanguageContentDto> FindById(int id);
        Task<LanguageContentDto> Create(CreateLanguageContentDto input);
        Task<LanguageContentDto> Update(int id, UpdateLanguageContentDto input);
        Task Delete(int id);
    }
}