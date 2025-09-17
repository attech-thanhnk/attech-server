namespace AttechServer.Applications.UserModules.Dtos.PhoneBook
{
    public class PhoneBookDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Extension { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? Notes { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}