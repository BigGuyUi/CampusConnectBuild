using System;

namespace CampusConnect.Models
{
    public class MessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsMine { get; set; }
    }
}