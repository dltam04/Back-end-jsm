namespace MovieApi.Models
{
    public class UserWatchlistMovie
    {
        public int UserAccountId { get; set; }  // FK to UserAccounts.Id
        public UserAccount User { get; set; } = null!;

        public int MovieId { get; set; }        // FK to Movies.MovieId
        public Movie Movie { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}