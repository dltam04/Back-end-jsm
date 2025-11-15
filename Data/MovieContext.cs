using Microsoft.EntityFrameworkCore;
using MovieApi.Handlers;
using MovieApi.Models;

namespace MovieApi.Data;

public class MovieContext : DbContext
{
    public MovieContext(DbContextOptions<MovieContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map to tables in SQL Server
        modelBuilder.Entity<Movie>().ToTable("Movies", "dbo");
        modelBuilder.Entity<UserAccount>().ToTable("UserAccounts", "dbo");
    }
}