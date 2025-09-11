namespace AttechServer.Applications.UserModules.Dtos.NewsCategory
{
    public class DetailNewsCategoryDto
    {
        public int Id { get; set; }
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string SlugVi { get; set; } = string.Empty;
        public string SlugEn { get; set; } = string.Empty;
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int Order { get; set; }
        public int Status { get; set; }
        public int NewsCount { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        
        public List<DetailNewsCategoryDto> Children { get; set; } = new List<DetailNewsCategoryDto>();
    }
} 
