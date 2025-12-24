using System;

namespace MovieApi.Models;
public class Movie
{
    // NEW semantics: MovieId is MovieLens id
    public int MovieId { get; set; } // TMDb movie id

    // Keep TMDb id separately
    public int? TmdbId { get; set; }

    public string Title { get; set; } = default!;
    public string? Overview { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public decimal VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
    public string? OriginalLanguage { get; set; }
    public int? Runtime { get; set; }

    // ML genres string
    public string? MovieLensGenres { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    public ICollection<MovieCast> Cast { get; set; } = new List<MovieCast>();
    public ICollection<MovieVideo> Videos { get; set; } = new List<MovieVideo>();
    public ICollection<TMDbFavoriteMovie> TMDbFavorites { get; set; } = new List<TMDbFavoriteMovie>();
    public ICollection<TMDbWatchlistMovie> TMDbWatchlists { get; set; } = new List<TMDbWatchlistMovie>();
    public ICollection<UserFavoriteMovie> UserFavorites { get; set; }
        = new List<UserFavoriteMovie>();
}
