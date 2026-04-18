using System;
using System.Collections.Generic;

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

        public DateTime? EventDate { get; set; }
        public string Location { get; set; } = "";
        public string Slug { get; set; } = "";

        public int LikeCount { get; set; }
        public int ReservationCount { get; set; }
        public int ViewCount { get; set; }

        // Per-user flags (populated by service when a userId is supplied)
        public bool IsViewed { get; set; }
        public bool IsLiked { get; set; }

        public List<TagDto> Tags { get; set; } = new();

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