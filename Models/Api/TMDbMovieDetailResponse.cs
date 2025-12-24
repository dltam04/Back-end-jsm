using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MovieApi.Models.Api
{
    // This matches /movie/{id}?append_to_response=videos,credits
    public class TMDbMovieDetailResponse : TMDbMovieSummary
    {
        [JsonPropertyName("runtime")]
        public int? Runtime { get; set; }

        [JsonPropertyName("videos")]
        public TMDbVideoList? Videos { get; set; }

        [JsonPropertyName("credits")]
        public TMDbCredits? Credits { get; set; }
    }

    public class TMDbVideoList
    {
        [JsonPropertyName("results")]
        public List<TMDbVideo> Results { get; set; } = new();
    }

    public class TMDbVideo
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("site")]
        public string? Site { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class TMDbCredits
    {
        [JsonPropertyName("cast")]
        public List<TMDbCastMember> Cast { get; set; } = new();
    }

    public class TMDbCastMember
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("character")]
        public string? Character { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }
    }

    // Helper so SyncMovieDetailsAsync can create a "summary" from a detail
    public static class TMDbDetailExtensions
    {
        public static TMDbMovieSummary ToSummary(this TMDbMovieDetailResponse d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));

            return new TMDbMovieSummary
            {
                Id = d.Id,
                Title = d.Title,
                OriginalTitle = d.OriginalTitle,
                Overview = d.Overview,
                ReleaseDate = d.ReleaseDate,
                PosterPath = d.PosterPath,
                BackdropPath = d.BackdropPath,
                VoteAverage = d.VoteAverage,
                VoteCount = d.VoteCount,
                Popularity = d.Popularity,
                OriginalLanguage = d.OriginalLanguage,
                GenreIds = d.GenreIds
            };
        }
    }
}
