namespace MovieApi.Models
{
    public class TMDbFavoriteMovie
    {
        public int TMDbAccountId { get; set; }  // TMDb account id
        public TMDbAccount TMDbAccount { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
