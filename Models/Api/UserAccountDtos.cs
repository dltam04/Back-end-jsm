namespace MovieApi.Models.Api
{
    public class UserAccountDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string? FullName { get; set; }
        public string Email { get; set; } = null!;

        // list of movie ids
        public List<int> Favorites { get; set; } = new();
        public List<int> Watchlist { get; set; } = new();
    }

    // What ACCEPT on POST/PUT
    public class UserAccountCreateUpdateDto
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? FullName { get; set; }

        // RAW password from client – will be hashed before saving
        public string? Password { get; set; } = null!;

        // Optional initial lists of movie ids
        public List<int>? Favorites { get; set; }
        public List<int>? Watchlist { get; set; }
    }
}
