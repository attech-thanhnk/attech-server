using AttechServer.Applications.UserModules.Dtos.PhoneBook;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IPhoneBookService
    {
        Task<PagingResult<PhoneBookDto>> FindAll(PagingRequestBaseDto input);
        Task<DetailPhoneBookDto> FindById(int id);
        Task<int> Create(CreatePhoneBookDto input);
        Task<bool> Update(int id, UpdatePhoneBookDto input);
        Task<bool> Delete(int id);
        Task<List<PhoneBookDto>> GetByDepartment(string department);
        Task<List<PhoneBookDto>> GetActiveContacts();
        Task<bool> UpdateOrder(int id, int newOrder);
        Task<bool> ToggleActive(int id);
    }
}