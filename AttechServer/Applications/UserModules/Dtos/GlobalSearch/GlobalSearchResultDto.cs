namespace AttechServer.Applications.UserModules.Dtos.GlobalSearch
{
    public class GlobalSearchResultDto
    {
        public string Keyword { get; set; } = string.Empty;
        public List<SearchCategoryResultDto> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public DateTime SearchTime { get; set; }
    }

    public class SearchCategoryResultDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryNameEn { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<SearchItemDto> Items { get; set; } = new();
    }

    public class SearchItemDto
    {
        public int Id { get; set; }
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string SlugVi { get; set; } = string.Empty;
        public string SlugEn { get; set; } = string.Empty;
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "product", "news", "service"
        public DateTime? CreatedDate { get; set; }
    }
}