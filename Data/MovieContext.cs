using Microsoft.EntityFrameworkCore;
using MovieApi.Models;

namespace MovieApi.Data;

public class MovieContext : DbContext
{
    public MovieContext(DbContextOptions<MovieContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // map to dbo.Movies explicitly
        b.Entity<Movie>().ToTable("Movies", "dbo");
        b.Entity<UserAccount>().ToTable("UserAccounts", "dbo");
    }
}