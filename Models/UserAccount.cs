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
    }
}