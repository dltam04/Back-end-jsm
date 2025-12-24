using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MovieApi.Models.Api
{
    // Response wrapper for /movie/popular, /movie/top_rated, etc.
    public class TMDbMovieListResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("results")]
        public List<TMDbMovieSummary> Results { get; set; } = new();
    }
}
