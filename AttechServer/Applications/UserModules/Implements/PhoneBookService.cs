using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.PhoneBook;
using AttechServer.Domains.Entities.Main;
using AttechServer.Infrastructures.Persistances;
using AttechServer.Shared.ApplicationBase.Common;
using Microsoft.EntityFrameworkCore;

namespace AttechServer.Applications.UserModules.Implements
{
    public partial class PhoneBookService : IPhoneBookService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PhoneBookService> _logger;

        public PhoneBookService(
            ApplicationDbContext context,
            ILogger<PhoneBookService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagingResult<PhoneBookDto>> FindAll(PagingRequestBaseDto input)
        {
            try
            {
                var query = _context.PhoneBooks
                    .Where(x => !x.Deleted)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(input.Keyword))
                {
                    var keyword = input.Keyword.ToLower();
                    query = query.Where(x =>
                        (x.FullName != null && x.FullName.ToLower().Contains(keyword)) ||
                        (x.Position != null && x.Position.ToLower().Contains(keyword)) ||
                        (x.Organization != null && x.Organization.ToLower().Contains(keyword)) ||
                        (x.Department != null && x.Department.ToLower().Contains(keyword)) ||
                        (x.Email != null && x.Email.ToLower().Contains(keyword)) ||
                        (x.Phone != null && x.Phone.Contains(keyword)) ||
                        (x.Mobile != null && x.Mobile.Contains(keyword)));
                }

                query = input.SortBy?.ToLower() switch
                {
                    "fullname" => input.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(x => x.FullName)
                        : query.OrderBy(x => x.FullName),
                    "position" => input.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(x => x.Position)
                        : query.OrderBy(x => x.Position),
                    "organization" => input.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(x => x.Organization)
                        : query.OrderBy(x => x.Organization),
                    "department" => input.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(x => x.Department)
                        : query.OrderBy(x => x.Department),
                    "createdate" => input.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(x => x.CreatedDate)
                        : query.OrderBy(x => x.CreatedDate),
                    _ => query.OrderBy(x => x.Order).ThenBy(x => x.Organization).ThenBy(x => x.FullName)
                };

                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((input.PageNumber - 1) * input.PageSize)
                    .Take(input.PageSize)
                    .ToListAsync();

                var result = items.Select(x => new PhoneBookDto
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Position = x.Position,
                    Organization = x.Organization,
                    Department = x.Department,
                    Phone = x.Phone,
                    Extension = x.Extension,
                    Email = x.Email,
                    Mobile = x.Mobile,
                    Notes = x.Notes,
                    Order = x.Order,
                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate ?? DateTime.Now,
                    ModifiedDate = x.ModifiedDate
                }).ToList();

                return new PagingResult<PhoneBookDto>
                {
                    Items = result,
                    TotalItems = totalCount,
                    Page = input.PageNumber,
                    PageSize = input.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.FindAll");
                throw;
            }
        }

        public async Task<DetailPhoneBookDto> FindById(int id)
        {
            try
            {
                var phoneBook = await _context.PhoneBooks
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (phoneBook == null)
                {
                    throw new Exception("Không tìm thấy thông tin danh bạ");
                }

                var result = new DetailPhoneBookDto
                {
                    Id = phoneBook.Id,
                    FullName = phoneBook.FullName,
                    Position = phoneBook.Position,
                    Organization = phoneBook.Organization,
                    Department = phoneBook.Department,
                    Phone = phoneBook.Phone,
                    Extension = phoneBook.Extension,
                    Email = phoneBook.Email,
                    Mobile = phoneBook.Mobile,
                    Notes = phoneBook.Notes,
                    Order = phoneBook.Order,
                    IsActive = phoneBook.IsActive,
                    CreatedDate = phoneBook.CreatedDate ?? DateTime.Now,
                    ModifiedDate = phoneBook.ModifiedDate,
                    CreatedBy = phoneBook.CreatedBy,
                    ModifiedBy = phoneBook.ModifiedBy
                };

                if (phoneBook.CreatedBy.HasValue)
                {
                    var createdUser = await _context.Users
                        .Where(u => u.Id == phoneBook.CreatedBy.Value)
                        .FirstOrDefaultAsync();
                    result.CreatedByName = createdUser?.FullName;
                }

                if (phoneBook.ModifiedBy.HasValue)
                {
                    var modifiedUser = await _context.Users
                        .Where(u => u.Id == phoneBook.ModifiedBy.Value)
                        .FirstOrDefaultAsync();
                    result.ModifiedByName = modifiedUser?.FullName;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.FindById with id: {Id}", id);
                throw;
            }
        }

        public async Task<int> Create(CreatePhoneBookDto input)
        {
            try
            {
                var phoneBook = new PhoneBook
                {
                    FullName = input.FullName,
                    Position = input.Position,
                    Organization = input.Organization,
                    Department = input.Department,
                    Phone = input.Phone,
                    Extension = input.Extension,
                    Email = input.Email,
                    Mobile = input.Mobile,
                    Notes = input.Notes,
                    Order = input.Order,
                    IsActive = input.IsActive
                };
                phoneBook.CreatedDate = DateTime.Now;

                _context.PhoneBooks.Add(phoneBook);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new phone book entry with id: {Id}", phoneBook.Id);
                return phoneBook.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.Create");
                throw;
            }
        }

        public async Task<bool> Update(int id, UpdatePhoneBookDto input)
        {
            try
            {
                var phoneBook = await _context.PhoneBooks
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (phoneBook == null)
                {
                    throw new Exception("Không tìm thấy thông tin danh bạ");
                }

                phoneBook.FullName = input.FullName;
                phoneBook.Position = input.Position;
                phoneBook.Organization = input.Organization;
                phoneBook.Department = input.Department;
                phoneBook.Phone = input.Phone;
                phoneBook.Extension = input.Extension;
                phoneBook.Email = input.Email;
                phoneBook.Mobile = input.Mobile;
                phoneBook.Notes = input.Notes;
                phoneBook.Order = input.Order;
                phoneBook.IsActive = input.IsActive;
                phoneBook.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated phone book entry with id: {Id}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.Update with id: {Id}", id);
                throw;
            }
        }

        public async Task<bool> Delete(int id)
        {
            try
            {
                var phoneBook = await _context.PhoneBooks
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (phoneBook == null)
                {
                    throw new Exception("Không tìm thấy thông tin danh bạ");
                }

                phoneBook.Deleted = true;
                phoneBook.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted phone book entry with id: {Id}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.Delete with id: {Id}", id);
                throw;
            }
        }

        public async Task<List<PhoneBookDto>> GetByDepartment(string department)
        {
            try
            {
                var phoneBooks = await _context.PhoneBooks
                    .Where(x => !x.Deleted && x.IsActive && x.Department.ToLower() == department.ToLower())
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.FullName)
                    .ToListAsync();

                return phoneBooks.Select(x => new PhoneBookDto
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Position = x.Position,
                    Organization = x.Organization,
                    Department = x.Department,
                    Phone = x.Phone,
                    Extension = x.Extension,
                    Email = x.Email,
                    Mobile = x.Mobile,
                    Notes = x.Notes,
                    Order = x.Order,
                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate ?? DateTime.Now,
                    ModifiedDate = x.ModifiedDate
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.GetByDepartment with department: {Department}", department);
                throw;
            }
        }

        public async Task<List<PhoneBookDto>> GetActiveContacts()
        {
            try
            {
                var phoneBooks = await _context.PhoneBooks
                    .Where(x => !x.Deleted && x.IsActive)
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.Department)
                    .ThenBy(x => x.FullName)
                    .ToListAsync();

                return phoneBooks.Select(x => new PhoneBookDto
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Position = x.Position,
                    Organization = x.Organization,
                    Department = x.Department,
                    Phone = x.Phone,
                    Extension = x.Extension,
                    Email = x.Email,
                    Mobile = x.Mobile,
                    Notes = x.Notes,
                    Order = x.Order,
                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate ?? DateTime.Now,
                    ModifiedDate = x.ModifiedDate
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.GetActiveContacts");
                throw;
            }
        }

        public async Task<bool> UpdateOrder(int id, int newOrder)
        {
            try
            {
                var phoneBook = await _context.PhoneBooks
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (phoneBook == null)
                {
                    throw new Exception("Không tìm thấy thông tin danh bạ");
                }

                phoneBook.Order = newOrder;
                phoneBook.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated order for phone book entry with id: {Id} to order: {Order}", id, newOrder);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.UpdateOrder with id: {Id}", id);
                throw;
            }
        }

        public async Task<bool> ToggleActive(int id)
        {
            try
            {
                var phoneBook = await _context.PhoneBooks
                    .Where(x => x.Id == id && !x.Deleted)
                    .FirstOrDefaultAsync();

                if (phoneBook == null)
                {
                    throw new Exception("Không tìm thấy thông tin danh bạ");
                }

                phoneBook.IsActive = !phoneBook.IsActive;
                phoneBook.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Toggled active status for phone book entry with id: {Id} to: {IsActive}", id, phoneBook.IsActive);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PhoneBookService.ToggleActive with id: {Id}", id);
                throw;
            }
        }
    }
}