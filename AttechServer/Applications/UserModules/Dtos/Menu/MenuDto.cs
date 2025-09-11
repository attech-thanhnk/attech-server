namespace AttechServer.Applications.UserModules.Dtos.Menu
{
    public class MenuDto
    {
        public int Id { get; set; }
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public bool IsExternal { get; set; }
        public string Target { get; set; } = string.Empty;
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string MenuType { get; set; } = string.Empty;
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        public List<MenuDto> Children { get; set; } = new();
        public DateTime? CreatedDate { get; set; }
    }

    public class CreateMenuDto
    {
        public string Key { get; set; } = string.Empty;
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UrlEn { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsExternal { get; set; } = false;
        public string Target { get; set; } = "_self";
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
    }

    public class UpdateMenuDto
    {
        public string Key { get; set; } = string.Empty;
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UrlEn { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsExternal { get; set; } = false;
        public string Target { get; set; } = "_self";
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
    }

    public class MenuTreeDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string UrlVi { get; set; } = string.Empty;
        public string UrlEn { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Order { get; set; }
        public int? ParentId { get; set; }
        public bool IsActive { get; set; }
        public bool IsExternal { get; set; }
        public string Target { get; set; } = string.Empty;
        public string MenuType { get; set; } = string.Empty;
        public List<MenuTreeDto> Children { get; set; } = new();
    }
}