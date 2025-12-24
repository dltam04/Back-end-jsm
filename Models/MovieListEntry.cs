namespace MovieApi.Models
{
    public class MovieListEntry
    {
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public string ListType { get; set; } = null!; // "popular", "top_rated", "upcoming"
        public int Page { get; set; }
        public int Position { get; set; }
    }
}