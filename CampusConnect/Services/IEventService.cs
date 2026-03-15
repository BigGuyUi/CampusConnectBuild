using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;

namespace CampusConnect.Services
{
    public interface IEventService
    {
        Task<List<EventDto>> GetEventsAsync(CancellationToken cancellationToken = default);
        Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default);
    }
}