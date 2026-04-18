using System;
using System.Collections.Generic;

namespace CampusConnect.Models
{
    public class EventCreateRequest
    {
        public int? SocietyId { get; set; }
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime? EventDate { get; set; }
        public string Location { get; set; } = "";
        public int CreatedByUserId { get; set; }

        // Now: submit tag IDs selected from the stored list
        public List<int> TagIds { get; set; } = new();
    }
}