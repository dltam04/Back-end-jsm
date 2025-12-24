using System.Text.Json.Serialization;

namespace MovieApi.Models.Api
{
    public class TMDbPersonDetailsResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Biography { get; set; }

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }
        public string? Birthday { get; set; }

        [JsonPropertyName("place_of_birth")]
        public string? PlaceOfBirth { get; set; }
    }
}
