using Microsoft.EntityFrameworkCore;

namespace MovieApi.Models;

public class MovieContext : DbContext
{
    public MovieContext(DbContextOptions<MovieContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // map to dbo.Movies explicitly
        b.Entity<Movie>().ToTable("Movies", "dbo");

        // (optional but recommended when sharing a DB with other EF projects)
        // keep a separate migrations history table to avoid collisions
        // If you use this, also set it in Program.cs's UseSqlServer call.
    }
}
