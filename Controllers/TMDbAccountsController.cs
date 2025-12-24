using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using MovieApi.Models.Api;
using MovieApi.Models;
using MovieApi.Data;
using System.Linq;
using System;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class TMDbAccountsController : ControllerBase
    {
        private readonly MovieContext _db;

        public TMDbAccountsController(MovieContext db)
        {
            _db = db;
        }

        // Map single entity to DTO (just IDs of movies)
        private async Task<TMDbAccountDto> MapToDto(TMDbAccount account)
        {
            var favIds = await _db.TMDbFavorites
                .Where(f => f.TMDbAccountId == account.TMDbAccountId)
                .Select(f => f.MovieId)
                .ToListAsync();

            var wlIds = await _db.TMDbWatchlists
                .Where(w => w.TMDbAccountId == account.TMDbAccountId)
                .Select(w => w.MovieId)
                .ToListAsync();

            return new TMDbAccountDto
            {
                TMDbAccountId = account.TMDbAccountId,
                Username = account.Username,
                Favorites = favIds,
                Watchlist = wlIds
            };
        }

        // GET /api/TMDbAccounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TMDbAccountDto>>> GetAll()
        {
            var accounts = await _db.TMDbAccounts.AsNoTracking().ToListAsync();
            var ids = accounts.Select(a => a.TMDbAccountId).ToList();

            var favLookup = await _db.TMDbFavorites
                .Where(f => ids.Contains(f.TMDbAccountId))
                .GroupBy(f => f.TMDbAccountId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(f => f.MovieId).ToList()
                );

            var wlLookup = await _db.TMDbWatchlists
                .Where(w => ids.Contains(w.TMDbAccountId))
                .GroupBy(w => w.TMDbAccountId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(w => w.MovieId).ToList()
                );

            var result = accounts.Select(a => new TMDbAccountDto
            {
                TMDbAccountId = a.TMDbAccountId,
                Username = a.Username,
                Favorites = favLookup.TryGetValue(a.TMDbAccountId, out var favs)
                    ? favs
                    : new List<int>(),
                Watchlist = wlLookup.TryGetValue(a.TMDbAccountId, out var wls)
                    ? wls
                    : new List<int>()
            }).ToList();

            return Ok(result);
        }

        // GET /api/TMDbAccounts/{tmdbAccountId}
        [HttpGet("{tmdbAccountId:int}")]
        public async Task<ActionResult<TMDbAccountDto>> GetById(int tmdbAccountId)
        {
            var account = await _db.TMDbAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

            if (account == null)
                return NotFound(new { message = "TMDb account not found" });

            var dto = await MapToDto(account);
            return Ok(dto);
        }

        // POST /api/TMDbAccounts
        // Used when TMDb login completes, to upsert the account and its lists
        [HttpPost]
        [AllowAnonymous] // frontend can call this after TMDb login
        public async Task<ActionResult<TMDbAccountDto>> CreateOrUpdate(
            [FromBody] TMDbAccountCreateUpdateDto request)
        {
            if (request.TMDbAccountId <= 0 ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.SessionId))
            {
                return BadRequest("Invalid request");
            }

            // Upsert: check if account exists
            var existing = await _db.TMDbAccounts
                .FirstOrDefaultAsync(a => a.TMDbAccountId == request.TMDbAccountId);

            if (existing == null)
            {
                existing = new TMDbAccount
                {
                    TMDbAccountId = request.TMDbAccountId,
                    Username = request.Username,
                    SessionId = request.SessionId
                };
                await _db.TMDbAccounts.AddAsync(existing);
            }
            else
            {
                existing.Username = request.Username;
                existing.SessionId = request.SessionId;
            }

            await _db.SaveChangesAsync();

            // Replace favorites if provided
            if (request.Favorites != null)
            {
                var oldFavs = _db.TMDbFavorites
                    .Where(f => f.TMDbAccountId == request.TMDbAccountId);
                _db.TMDbFavorites.RemoveRange(oldFavs);

                if (request.Favorites.Count > 0)
                {
                    var favEntities = request.Favorites
                        .Distinct()
                        .Select(movieId => new TMDbFavoriteMovie
                        {
                            TMDbAccountId = request.TMDbAccountId,
                            MovieId = movieId
                            // CreatedAt default from model
                        });

                    await _db.TMDbFavorites.AddRangeAsync(favEntities);
                }
            }

            // Replace watchlist if provided
            if (request.Watchlist != null)
            {
                var oldWls = _db.TMDbWatchlists
                    .Where(w => w.TMDbAccountId == request.TMDbAccountId);
                _db.TMDbWatchlists.RemoveRange(oldWls);

                if (request.Watchlist.Count > 0)
                {
                    var wlEntities = request.Watchlist
                        .Distinct()
                        .Select(movieId => new TMDbWatchlistMovie
                        {
                            TMDbAccountId = request.TMDbAccountId,
                            MovieId = movieId
                        });

                    await _db.TMDbWatchlists.AddRangeAsync(wlEntities);
                }
            }

            await _db.SaveChangesAsync();

            var dto = await MapToDto(existing);
            return Ok(dto);
        }

        // PUT /api/TMDbAccounts/{tmdbAccountId}
        [HttpPut("{tmdbAccountId:int}")]
        public async Task<ActionResult<TMDbAccountDto>> Update(
            int tmdbAccountId,
            [FromBody] TMDbAccountCreateUpdateDto request)
        {
            if (tmdbAccountId != request.TMDbAccountId)
                return BadRequest("TMDbAccountId mismatch");

            var account = await _db.TMDbAccounts
                .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

            if (account == null)
                return NotFound(new { message = "TMDb account not found" });

            account.Username = request.Username;
            account.SessionId = request.SessionId;

            // Same replace logic as in POST
            var oldFavs = _db.TMDbFavorites
                .Where(f => f.TMDbAccountId == tmdbAccountId);
            _db.TMDbFavorites.RemoveRange(oldFavs);

            if (request.Favorites != null && request.Favorites.Count > 0)
            {
                var favEntities = request.Favorites
                    .Distinct()
                    .Select(movieId => new TMDbFavoriteMovie
                    {
                        TMDbAccountId = tmdbAccountId,
                        MovieId = movieId
                    });

                await _db.TMDbFavorites.AddRangeAsync(favEntities);
            }

            var oldWls = _db.TMDbWatchlists
                .Where(w => w.TMDbAccountId == tmdbAccountId);
            _db.TMDbWatchlists.RemoveRange(oldWls);

            if (request.Watchlist != null && request.Watchlist.Count > 0)
            {
                var wlEntities = request.Watchlist
                    .Distinct()
                    .Select(movieId => new TMDbWatchlistMovie
                    {
                        TMDbAccountId = tmdbAccountId,
                        MovieId = movieId
                    });

                await _db.TMDbWatchlists.AddRangeAsync(wlEntities);
            }

            await _db.SaveChangesAsync();

            var dto = await MapToDto(account);
            return Ok(dto);
        }

        // DELETE /api/TMDbAccounts/{tmdbAccountId}
        [HttpDelete("{tmdbAccountId:int}")]
        public async Task<ActionResult> Delete(int tmdbAccountId)
        {
            var account = await _db.TMDbAccounts
                .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

            if (account == null)
                return NotFound(new { message = $"TMDb account {tmdbAccountId} not found." });

            var favs = _db.TMDbFavorites
                .Where(f => f.TMDbAccountId == tmdbAccountId);
            var wls = _db.TMDbWatchlists
                .Where(w => w.TMDbAccountId == tmdbAccountId);

            _db.TMDbFavorites.RemoveRange(favs);
            _db.TMDbWatchlists.RemoveRange(wls);
            _db.TMDbAccounts.Remove(account);

            await _db.SaveChangesAsync();

            return NoContent();
        }

        // --------------------------------------------------
        // Toggle Favorite for TMDb account (DB-based)
        // POST /api/TMDbAccounts/{tmdbAccountId}/favorite
        // body: { "movieId": 123, "favorite": true/false }
        // --------------------------------------------------
        [HttpPost("{tmdbAccountId:int}/favorite")]
        [AllowAnonymous] // TMDb logins have no JWT, so skip Role-based auth
        public async Task<ActionResult> ToggleFavorite(
            int tmdbAccountId,
            [FromBody] TMDbFavoriteToggleDto dto)
        {
            if (dto == null || dto.MovieId <= 0)
                return BadRequest("MovieId is required.");

            var account = await _db.TMDbAccounts
                .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

            if (account == null)
                return NotFound(new { message = "TMDb account not found" });

            // ---- mirror to your own DB (TMDbFavoriteMovies table) ----
            var existing = await _db.TMDbFavorites.FindAsync(tmdbAccountId, dto.MovieId);

            if (dto.Favorite)
            {
                // "Add" case (like POST /me/movies/favorites/{movieId})
                if (existing == null)
                {
                    _db.TMDbFavorites.Add(new TMDbFavoriteMovie
                    {
                        TMDbAccountId = tmdbAccountId,
                        MovieId = dto.MovieId,
                    });
                }
            }
            else
            {
                // "Remove" case (like DELETE /me/movies/favorites/{movieId})
                if (existing != null)
                {
                    _db.TMDbFavorites.Remove(existing);
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // --------------------------------------------------
        // Toggle WATCHLIST for TMDb account (DB-based)
        // POST /api/TMDbAccounts/{tmdbAccountId}/watchlist
        // body: { "movieId": 123, "watchlist": true/false }
        // --------------------------------------------------
        [HttpPost("{tmdbAccountId:int}/watchlist")]
        [AllowAnonymous]
        public async Task<ActionResult> ToggleWatchlist(
            int tmdbAccountId,
            [FromBody] TMDbWatchlistToggleDto dto)
        {
            if (dto == null || dto.MovieId <= 0)
                return BadRequest("MovieId is required.");

            var account = await _db.TMDbAccounts
                .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

            if (account == null)
                return NotFound(new { message = "TMDb account not found" });

            var existing = await _db.TMDbWatchlists.FindAsync(tmdbAccountId, dto.MovieId);

            if (dto.Watchlist)
            {
                // Add
                if (existing == null)
                {
                    _db.TMDbWatchlists.Add(new TMDbWatchlistMovie
                    {
                        TMDbAccountId = tmdbAccountId,
                        MovieId = dto.MovieId,
                    });
                }
            }
            else
            {
                // Remove
                if (existing != null)
                {
                    _db.TMDbWatchlists.Remove(existing);
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ------------------------------------------
        // NEW: DB-BASED lists for TMDb profile
        // GET /api/TMDbAccounts/{tmdbAccountId}/list
        // ------------------------------------------
        [HttpGet("{tmdbAccountId:int}/list")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetUserListFromDb(
            int tmdbAccountId,
            [FromQuery] string? listName,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            listName = listName?.Trim();

            if (string.IsNullOrWhiteSpace(listName))
                return BadRequest("listName is required.");

            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            IQueryable<Movie?> baseQuery;

            if (listName.Equals("favorite/movies", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = _db.TMDbFavorites
                    .AsNoTracking()
                    .Where(f => f.TMDbAccountId == tmdbAccountId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => f.Movie);
            }
            else if (listName.Equals("watchlist/movies", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = _db.TMDbWatchlists
                    .AsNoTracking()
                    .Where(w => w.TMDbAccountId == tmdbAccountId)
                    .OrderByDescending(w => w.CreatedAt)
                    .Select(w => w.Movie);
            }
            else
            {
                return BadRequest("Unsupported listName. Use 'favorite/movies' or 'watchlist/movies'.");
            }

            var safeQuery = baseQuery.Where(m => m != null).Select(m => m!);

            var totalResults = await safeQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);

            var items = await safeQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    id = m.MovieId,
                    title = m.Title,
                    poster_path = m.PosterPath,
                    vote_average = m.VoteAverage,
                    release_date = m.ReleaseDate
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                results = items,
                total_pages = totalPages,
                total_results = totalResults
            });
        }
    }
}
