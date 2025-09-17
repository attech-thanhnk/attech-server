namespace AttechServer.Applications.UserModules.Dtos.PhoneBook
{
    public class ImportResultDto
    {
        public int TotalRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<ImportPhoneBookDto> FailedData { get; set; } = new();
    }

    public class ImportErrorDetail
    {
        public int Row { get; set; }
        public string Column { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}