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

        public string DateDisplay => PostTime == DateTime.MinValue
            ? ""
            : PostTime.ToLocalTime().ToString("d MMM");
    }
}