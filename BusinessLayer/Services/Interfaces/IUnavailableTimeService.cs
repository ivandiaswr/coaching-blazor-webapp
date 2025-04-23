namespace BusinessLayer.Services.Interfaces 
{
    public interface IUnavailableTimeService
    {
        Task<IEnumerable<UnavailableTime>> GetAllUnavailableTimesAsync();
        Task<UnavailableTime> CreateUnavailableTimeAsync(UnavailableTime unavailableTime);
        Task<UnavailableTime> GetUnavailableTimeByIdAsync(int id);
        Task<UnavailableTime> UpdateUnavailableTimeAsync(UnavailableTime unavailableTime);
        Task<bool> DeleteUnavailableTimeAsync(int id);
    }
}

