namespace MovieApi.Models.Api
{
    public class TMDbAccountDto
    {
        public int TMDbAccountId { get; set; }             // TMDb account id
        public string Username { get; set; } = null!;

        // movie ids
        public List<int> Favorites { get; set; } = new();
        public List<int> Watchlist { get; set; } = new();
    }

    // What ACCEPT on POST/PUT (when you create/update from TMDb login flow)
    public class TMDbAccountCreateUpdateDto
    {
        public int TMDbAccountId { get; set; }             // TMDb account id
        public string Username { get; set; } = null!;
        public string SessionId { get; set; } = null!;

        public List<int>? Favorites { get; set; }
        public List<int>? Watchlist { get; set; }
    }
}
