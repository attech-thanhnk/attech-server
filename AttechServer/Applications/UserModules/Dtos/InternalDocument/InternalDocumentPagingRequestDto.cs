using AttechServer.Shared.ApplicationBase.Common;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Applications.UserModules.Dtos.InternalDocument
{
    public class InternalDocumentPagingRequestDto : PagingRequestBaseDto
    {
        /// <summary>
        /// Filter by category name/slug
        /// </summary>
        [FromQuery(Name = "category")]
        public string? Category { get; set; }
    }
}