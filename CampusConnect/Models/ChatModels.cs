using System;
using System.Collections.Generic;

namespace CampusConnect.Models
{
    public class ChatSummary
    {
        public int Id { get; set; }
        public int? SocietyId { get; set; }
        public string Name { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public int UnreadCount { get; set; }
    }
}