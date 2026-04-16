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
        Task<EventDto?> GetEventBySlugAsync(string slug, CancellationToken cancellationToken = default);

        // Updated: include userId for per-user toggle behavior
        Task<bool> LikeEventAsync(int eventId, int userId, CancellationToken cancellationToken = default);
        Task<bool> RsvpEventAsync(int eventId, int userId, CancellationToken cancellationToken = default);

        // User-specific lists
        Task<List<EventDto>> GetUserRsvpdEventsAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<EventDto>> GetUserLikedEventsAsync(int userId, CancellationToken cancellationToken = default);

        // Create new event and return slug for navigation
        Task<string> CreateEventAsync(EventCreateRequest request, CancellationToken cancellationToken = default);
    }
}