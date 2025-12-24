namespace MovieApi.Models
{
    public class UserFavoriteMovie
    {
        public int UserAccountId { get; set; } // UserId
        public UserAccount User { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
