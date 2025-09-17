using AttechServer.Applications.UserModules.Dtos.ApiEndpoint;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IApiEndpointService
    {
        Task<List<ApiEndpointDto>> FindAll();
        Task<ApiEndpointDto> FindById(int id);
        Task Create(CreateApiEndpointDto input);
        Task Update(int id, CreateApiEndpointDto input);
        Task Delete(int id);
    }
}
