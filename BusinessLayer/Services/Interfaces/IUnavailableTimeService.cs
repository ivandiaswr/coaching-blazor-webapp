namespace BusinessLayer.Services.Interfaces 
{
    public interface IUnavailableTimeService
    {
        Task<UnavailableTime> CreateUnavailableTimeAsync(UnavailableTime unavailableTime);
        Task<UnavailableTime> GetUnavailableTimeByIdAsync(int id);
        Task<IEnumerable<UnavailableTime>> GetAllUnavailableTimesAsync();
        Task<UnavailableTime> UpdateUnavailableTimeAsync(UnavailableTime unavailableTime);
        Task<bool> DeleteUnavailableTimeAsync(int id);
    }
}

