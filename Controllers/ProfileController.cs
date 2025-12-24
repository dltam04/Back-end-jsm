using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieApi.Models;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/me/movies")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly MovieContext _db;

        public ProfileController(MovieContext db)
        {
            _db = db;
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst(JwtRegisteredClaimNames.Sub);

            if (idClaim == null)
                throw new InvalidOperationException("No user id claim in token.");

            return int.Parse(idClaim.Value);
        }

        // Favorites

        [HttpGet("favorites")]
        public async Task<ActionResult<IEnumerable<object>>> GetFavorites()
        {
            var userId = GetUserId();

            var favorites = await _db.UserFavoriteMovies
                .Where(f => f.UserAccountId == userId)
                .Include(f => f.Movie)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    id = f.Movie.MovieId,
                    title = f.Movie.Title,
                    poster_path = f.Movie.PosterPath,
                    vote_average = f.Movie.VoteAverage,
                    release_date = f.Movie.ReleaseDate
                })
                .ToListAsync();

            return Ok(favorites);
        }

        [HttpPost("favorites/{movieId:int}")]
        public async Task<ActionResult> AddFavorite(int movieId)
        {
            var userId = GetUserId();

            var existing = await _db.UserFavoriteMovies.FindAsync(userId, movieId);
            if (existing != null)
                return NoContent();

            var fav = new UserFavoriteMovie
            {
                UserAccountId = userId,
                MovieId = movieId
            };

            _db.UserFavoriteMovies.Add(fav);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("favorites/{movieId:int}")]
        public async Task<ActionResult> RemoveFavorite(int movieId)
        {
            var userId = GetUserId();

            var fav = await _db.UserFavoriteMovies.FindAsync(userId, movieId);
            if (fav == null)
                return NotFound();

            _db.UserFavoriteMovies.Remove(fav);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Watchlist

        [HttpGet("watchlist")]
        public async Task<ActionResult<IEnumerable<object>>> GetWatchlist()
        {
            var userId = GetUserId();

            var watchlist = await _db.UserWatchlistMovies
                .Where(w => w.UserAccountId == userId)
                .Include(w => w.Movie)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    id = w.Movie.MovieId,
                    title = w.Movie.Title,
                    poster_path = w.Movie.PosterPath,
                    vote_average = w.Movie.VoteAverage,
                    release_date = w.Movie.ReleaseDate
                })
                .ToListAsync();

            return Ok(watchlist);
        }

        [HttpPost("watchlist/{movieId:int}")]
        public async Task<ActionResult> AddWatchlist(int movieId)
        {
            var userId = GetUserId();

            var existing = await _db.UserWatchlistMovies.FindAsync(userId, movieId);
            if (existing != null)
                return NoContent();

            var item = new UserWatchlistMovie
            {
                UserAccountId = userId,
                MovieId = movieId
            };

            _db.UserWatchlistMovies.Add(item);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("watchlist/{movieId:int}")]
        public async Task<ActionResult> RemoveWatchlist(int movieId)
        {
            var userId = GetUserId();

            var item = await _db.UserWatchlistMovies.FindAsync(userId, movieId);
            if (item == null)
                return NotFound();

            _db.UserWatchlistMovies.Remove(item);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
