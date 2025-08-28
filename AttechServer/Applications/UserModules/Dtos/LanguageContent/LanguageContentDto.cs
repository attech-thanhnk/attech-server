namespace AttechServer.Applications.UserModules.Dtos.LanguageContent
{
    public class LanguageContentDto
    {
        public int Id { get; set; }
        public string ContentKey { get; set; } = null!;
        public string? ValueVi { get; set; }
        public string? ValueEn { get; set; }
        public string Category { get; set; } = "common";
        public string? Description { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}