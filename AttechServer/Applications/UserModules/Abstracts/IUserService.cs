using AttechServer.Applications.UserModules.Dtos.User;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IUserService
    {

        /// <summary>
        /// L?y danh s�ch ngu?i d�ng
        /// </summary>
        Task<PagingResult<UserDto>> FindAll(UserPagingRequestDto input);

        /// <summary>
        /// L?y th�ng tin ngu?i d�ng theo ID
        /// </summary>
        Task<UserDto> FindById(int id);

        /// <summary>
        /// C?p nh?t th�ng tin ngu?i d�ng
        /// </summary>
        Task Update(UpdateUserDto input);

        /// <summary>
        /// X�a ngu?i d�ng
        /// </summary>
        Task Delete(int id);
    }
}
