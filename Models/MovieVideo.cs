namespace MovieApi.Models
{
    public class MovieVideo
    {
        public int MovieVideoId { get; set; }
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public string Key { get; set; } = null!;
        public string Site { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
    }
}
