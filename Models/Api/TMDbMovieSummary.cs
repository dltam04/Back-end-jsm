using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MovieApi.Models.Api
{
    // Basic movie info coming from TMDb "list" endpoints
    public class TMDbMovieSummary
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; set; }

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        [JsonPropertyName("original_language")]
        public string? OriginalLanguage { get; set; }

        // list of genre IDs from TMDb
        [JsonPropertyName("genre_ids")]
        public List<int>? GenreIds { get; set; }
    }
}
