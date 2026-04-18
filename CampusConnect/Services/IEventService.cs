using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;

namespace CampusConnect.Services
{
    public interface IEventService
    {
        Task<List<EventDto>> GetEventsAsync(int? userId = null, CancellationToken cancellationToken = default);
        Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default);
        Task<EventDto?> GetEventBySlugAsync(string slug, CancellationToken cancellationToken = default);

        // Per-user toggle behavior
        Task<bool> LikeEventAsync(int eventId, int userId, CancellationToken cancellationToken = default);
        Task<bool> RsvpEventAsync(int eventId, int userId, CancellationToken cancellationToken = default);

        // User-specific lists
        Task<List<EventDto>> GetUserRsvpdEventsAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<EventDto>> GetUserLikedEventsAsync(int userId, CancellationToken cancellationToken = default);

        // Create new event and return slug for navigation
        Task<string> CreateEventAsync(EventCreateRequest request, CancellationToken cancellationToken = default);

        // Tags
        Task<List<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default);

        // New: record first view by user (only increments once per user) and helpers
        Task RecordViewAsync(int eventId, int userId, CancellationToken cancellationToken = default);
        Task<bool> IsEventLikedByUserAsync(int eventId, int userId, CancellationToken cancellationToken = default);

        // New: check if a specific user has viewed an event
        Task<bool> IsEventViewedByUserAsync(int eventId, int userId, CancellationToken cancellationToken = default);
    }
}