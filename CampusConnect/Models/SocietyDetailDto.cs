using System.Collections.Generic;

namespace CampusConnect.Models
{
    public class SocietyDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<MemberDto> Members { get; set; } = new();
        public List<PostDto> Posts { get; set; } = new();

        public int MemberCount => Members?.Count ?? 0;
        public int PostCount => Posts?.Count ?? 0;
    }
}