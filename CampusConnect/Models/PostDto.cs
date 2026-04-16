using System;

namespace CampusConnect.Models
{
    public class PostDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime PostTime { get; set; }
    }
}