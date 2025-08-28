using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.LanguageContentCategory;

namespace AttechServer.Applications.UserModules.Implements
{
    public class LanguageContentCategoryService : ILanguageContentCategoryService
    {
        public Task<List<LanguageContentCategoryDto>> GetAllCategories()
        {
            var categories = new List<LanguageContentCategoryDto>
            {
                new() { Id = 1, Name = "common", DisplayName = "Common" },
                new() { Id = 2, Name = "navigation", DisplayName = "Navigation" },
                new() { Id = 3, Name = "auth", DisplayName = "Authentication" },
                new() { Id = 4, Name = "admin", DisplayName = "Admin" },
                new() { Id = 5, Name = "frontend", DisplayName = "Frontend" },
                new() { Id = 6, Name = "validation", DisplayName = "Validation" },
                new() { Id = 7, Name = "errors", DisplayName = "Errors" }
            };

            return Task.FromResult(categories);
        }
    }
}