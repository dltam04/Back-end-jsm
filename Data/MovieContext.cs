using Microsoft.EntityFrameworkCore;
using MovieApi.Models;

namespace MovieApi.Data;

public class MovieContext : DbContext
{
    public MovieContext(DbContextOptions<MovieContext> options) : base(options) { }

    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<UserFavoriteMovie> UserFavoriteMovies { get; set; } = null!;
    public DbSet<UserWatchlistMovie> UserWatchlistMovies { get; set; } = null!;

    public DbSet<TMDbAccount> TMDbAccounts { get; set; } = null!;
    public DbSet<TMDbFavoriteMovie> TMDbFavorites { get; set; } = null!;
    public DbSet<TMDbWatchlistMovie> TMDbWatchlists { get; set; } = null!;

    public DbSet<Movie> Movies { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<MovieGenre> MovieGenres { get; set; } = null!;
    public DbSet<Person> People { get; set; } = null!;
    public DbSet<MovieCast> MovieCasts { get; set; } = null!;
    public DbSet<MovieVideo> MovieVideos { get; set; } = null!;
    public DbSet<MovieListEntry> MovieListEntries { get; set; } = null!;
    public DbSet<MovieLink> Links { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Table names
        modelBuilder.Entity<Movie>().ToTable("Movies", "dbo");
        modelBuilder.Entity<UserAccount>().ToTable("UserAccounts", "dbo");
        modelBuilder.Entity<Genre>().ToTable("Genres", "dbo");
        modelBuilder.Entity<MovieGenre>().ToTable("MovieGenres", "dbo");
        modelBuilder.Entity<Person>().ToTable("People", "dbo");
        modelBuilder.Entity<MovieCast>().ToTable("MovieCasts", "dbo");
        modelBuilder.Entity<MovieVideo>().ToTable("MovieVideos", "dbo");
        modelBuilder.Entity<MovieListEntry>().ToTable("MovieListEntries", "dbo");
        modelBuilder.Entity<TMDbAccount>().ToTable("TMDbAccounts", "dbo");
        modelBuilder.Entity<UserFavoriteMovie>().ToTable("UserFavoriteMovies", "dbo");
        modelBuilder.Entity<UserWatchlistMovie>().ToTable("UserWatchlistMovies", "dbo");

        // MovieLink
        modelBuilder.Entity<MovieLink>(entity =>
        {
            entity.ToTable("links");

            entity.HasKey(l => l.MovieId);

            entity.Property(l => l.MovieId).HasColumnName("movieId");
            entity.Property(l => l.ImdbId).HasColumnName("imdbId");
            entity.Property(l => l.TmdbId).HasColumnName("tmdbId");
        });

        // MovieGenre
        modelBuilder.Entity<MovieGenre>()
            .HasKey(mg => new { mg.MovieId, mg.GenreId });

        modelBuilder.Entity<MovieGenre>()
            .HasOne(mg => mg.Movie)
            .WithMany(m => m.MovieGenres)
            .HasForeignKey(mg => mg.MovieId);

        modelBuilder.Entity<MovieGenre>()
            .HasOne(mg => mg.Genre)
            .WithMany(g => g.MovieGenres)
            .HasForeignKey(mg => mg.GenreId);

        // MovieCast
        modelBuilder.Entity<MovieCast>()
            .HasKey(mc => new { mc.MovieId, mc.PersonId, mc.CastOrder });

        modelBuilder.Entity<MovieCast>()
            .HasOne(mc => mc.Movie)
            .WithMany(m => m.Cast)
            .HasForeignKey(mc => mc.MovieId);

        modelBuilder.Entity<MovieCast>()
            .HasOne(mc => mc.Person)
            .WithMany(p => p.MovieCredits)
            .HasForeignKey(mc => mc.PersonId);

        // MovieVideo
        modelBuilder.Entity<MovieVideo>()
            .HasOne(v => v.Movie)
            .WithMany(m => m.Videos)
            .HasForeignKey(v => v.MovieId);

        // MovieListEntry
        modelBuilder.Entity<MovieListEntry>()
            .HasKey(e => new { e.MovieId, e.ListType, e.Page });

        modelBuilder.Entity<MovieListEntry>()
            .HasOne(e => e.Movie)
            .WithMany()
            .HasForeignKey(e => e.MovieId);

        // TMDbFavoriteMovies
        modelBuilder.Entity<TMDbFavoriteMovie>(entity =>
        {
            entity.ToTable("TMDbFavoriteMovies", "dbo");   // matches SQL table name

            // Composite PK
            entity.HasKey(f => new { f.TMDbAccountId, f.MovieId });

            // FK: TMDbFavorite.TMDbAccountId -> TMDbAccounts.TMDbAccountId
            entity.HasOne(f => f.TMDbAccount)
                  .WithMany(a => a.Favorites)
                  .HasForeignKey(f => f.TMDbAccountId);

            // FK: TMDbFavorite.MovieId -> Movies.MovieId
            entity.HasOne(f => f.Movie)
                  .WithMany(m => m.TMDbFavorites)
                  .HasForeignKey(f => f.MovieId);
        });

        // TMDbWatchlistMovies
        modelBuilder.Entity<TMDbWatchlistMovie>(entity =>
        {
            // Use the table that just created
            entity.ToTable("TMDbWatchlistMovies", "dbo");

            // Composite PK
            entity.HasKey(w => new { w.TMDbAccountId, w.MovieId });

            // FK: TMDbWatchlist.TMDbAccountId -> TMDbAccounts.TMDbAccountId
            entity.HasOne(w => w.TMDbAccount)
                  .WithMany(a => a.Watchlist)
                  .HasForeignKey(w => w.TMDbAccountId);

            // FK: TMDbWatchlist.MovieId -> Movies.MovieId
            entity.HasOne(w => w.Movie)
                  .WithMany(m => m.TMDbWatchlists)
                  .HasForeignKey(w => w.MovieId);
        });

        // UserFavoriteMovies
        modelBuilder.Entity<UserFavoriteMovie>(entity =>
        {
            entity.ToTable("UserFavoriteMovies", "dbo");

            entity.HasKey(f => new { f.UserAccountId, f.MovieId });

            entity.HasOne(f => f.User)
                  .WithMany(u => u.FavoriteMovies)
                  .HasForeignKey(f => f.UserAccountId);

            entity.HasOne(f => f.Movie)
                  .WithMany(m => m.UserFavorites)
                  .HasForeignKey(f => f.MovieId);
        });

        // UserWatchlistMovie
        modelBuilder.Entity<UserWatchlistMovie>()
            .HasKey(w => new { w.UserAccountId, w.MovieId });

        modelBuilder.Entity<UserWatchlistMovie>()
            .HasOne(w => w.User)
            .WithMany(u => u.WatchlistMovies)
            .HasForeignKey(w => w.UserAccountId);

        modelBuilder.Entity<UserWatchlistMovie>()
            .HasOne(w => w.Movie)
            .WithMany()
            .HasForeignKey(w => w.MovieId);
    }
}