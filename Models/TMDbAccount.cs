namespace MovieApi.Models
{
    public class TMDbAccount
    {
        public int TMDbAccountId { get; set; }         // TMDb account id
        public string Username { get; set; } = null!;
        public string SessionId { get; set; } = null!; // TMDb session_id (if you want)

        public ICollection<TMDbFavoriteMovie> Favorites { get; set; } = new List<TMDbFavoriteMovie>();
        public ICollection<TMDbWatchlistMovie> Watchlist { get; set; } = new List<TMDbWatchlistMovie>();
    }
}
