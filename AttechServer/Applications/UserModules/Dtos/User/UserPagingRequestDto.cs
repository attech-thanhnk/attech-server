using AttechServer.Shared.ApplicationBase.Common;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Applications.UserModules.Dtos.User
{
    /// <summary>
    /// User pagination request with role filter
    /// </summary>
    public class UserPagingRequestDto : PagingRequestBaseDto
    {
        /// <summary>
        /// Filter by role ID
        /// </summary>
        [FromQuery(Name = "roleId")]
        public int? RoleId { get; set; }
    }
}