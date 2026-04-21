using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;

namespace CampusConnect.Services
{
    public interface ISocietyService
    {
        Task<List<SocietyDto>> GetAllSocietiesAsync(CancellationToken cancellationToken = default);
        Task<List<SocietyDto>> GetUserSocietiesAsync(int userId, CancellationToken cancellationToken = default);
        Task<bool> JoinSocietyAsync(int userId, int societyId, CancellationToken cancellationToken = default);
        Task<bool> LeaveSocietyAsync(int userId, int societyId, CancellationToken cancellationToken = default);
        Task<SocietyDetailDto?> GetSocietyAsync(int societyId, CancellationToken cancellationToken = default);
    }
}