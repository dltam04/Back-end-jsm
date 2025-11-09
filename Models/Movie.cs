using System;

namespace MovieApi.Models;
public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public DateTime? ReleaseDate { get; set; }
    public float? Rating { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
}
