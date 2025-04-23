using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISessionPackDefinitionService
    {
        Task<List<SessionPackDefinition>> GetAllAsync();
        Task CreateAsync(SessionPackDefinition definition);
        Task UpdateAsync(SessionPackDefinition definition);
        Task DeleteAsync(int id);
    }
}