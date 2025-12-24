namespace MovieApi.Models
{
    public class UserAccount
    {
        public int Id { get; set; }  // Primary Key
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Hashed password
        public string Role { get; set; } = "User";           // "User" or "Admin"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Email verification part
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationTokenHash { get; set; }
        public DateTime? EmailVerificationTokenExpiresAt { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }

        // Password reset part
        public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetTokenExpiresAt { get; set; }
        public DateTime? PasswordResetRequestedAt { get; set; }


        public ICollection<UserFavoriteMovie> FavoriteMovies { get; set; } = new List<UserFavoriteMovie>();
        public ICollection<UserWatchlistMovie> WatchlistMovies { get; set; } = new List<UserWatchlistMovie>();
    }
}