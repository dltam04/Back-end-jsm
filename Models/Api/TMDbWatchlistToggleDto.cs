namespace MovieApi.Models.Api
{
    public class TMDbWatchlistToggleDto
    {
        public int MovieId { get; set; }
        public bool Watchlist { get; set; }
    }
}