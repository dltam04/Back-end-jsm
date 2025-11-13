namespace MovieApi.Models.Api
{
    public class LoginResponseModel
    {
        public string Username { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
