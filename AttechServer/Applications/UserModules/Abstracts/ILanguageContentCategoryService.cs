using AttechServer.Applications.UserModules.Dtos.LanguageContentCategory;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface ILanguageContentCategoryService
    {
        Task<List<LanguageContentCategoryDto>> GetAllCategories();
    }
}