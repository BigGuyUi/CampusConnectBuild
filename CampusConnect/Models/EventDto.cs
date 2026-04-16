using System;

namespace CampusConnect.Models
{
    public class EventDto
    {
        public int Id { get; set; }
        public int? SocietyId { get; set; }
        public string? SocietyName { get; set; }
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime PostTime { get; set; }

        // New: dedicated event date/time and location + counters
        public DateTime? EventDate { get; set; }
        public string Location { get; set; } = "";
        public string Slug { get; set; } = "";
        public int LikeCount { get; set; }
        public int ReservationCount { get; set; }

        // Prefer showing the event date for display; fallback to post time
        public string DateDisplay
        {
            get
            {
                var dt = EventDate ?? (PostTime == DateTime.MinValue ? (DateTime?)null : PostTime);
                return dt is null ? "" : dt.Value.ToLocalTime().ToString("d MMM");
            }
        }
    }
}