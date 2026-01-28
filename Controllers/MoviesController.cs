using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieApi.Data;
using MovieApi.Models;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api")]
    public class MoviesController : ControllerBase
    {
        private readonly MovieContext _db;
        private const int PageSize = 20;

        public MoviesController(MovieContext db)
        {
            _db = db;
        }
        
        // GET /api/movies?category=&genreId=&search=&page=
        [HttpGet("movies")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> Browse(
            [FromQuery] string? category,
            [FromQuery] int? genreId,
            [FromQuery] string? search,
            [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;

            var query = _db.Movies
                .AsNoTracking()
                .AsQueryable();

            // 1. search text
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m =>
                    m.Title.Contains(search) ||
                    (m.Overview ?? "").Contains(search));
            }

            // 2. optional filter by genre
            if (genreId.HasValue)
            {
                query =
                    from m in query
                    join mg in _db.MovieGenres.AsNoTracking()
                        on m.MovieId equals mg.MovieId
                    where mg.GenreId == genreId.Value
                    select m;
            }

            // 3. category = how to order / filter
            switch (category)
            {
                case "top_rated":
                    query = query.OrderByDescending(m => m.VoteAverage);
                    break;

                case "upcoming":
                    query = query
                        .Where(m => m.ReleaseDate.HasValue && m.ReleaseDate.Value >= DateTime.UtcNow.Date)
                        .OrderBy(m => m.ReleaseDate);
                    break;

                // null, "popular", anything else
                default:
                    query = query.OrderByDescending(m => m.Popularity);
                    break;
            }

            // remove dupes that might come from the genre JOIN
            query = query.Distinct();

            var totalResults = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalResults / (double)PageSize);

            var results = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(m => new
                {
                    id = m.MovieId,
                    tmdb_id = m.TmdbId,
                    title = m.Title,
                    overview = m.Overview,
                    poster_path = m.PosterPath,
                    backdrop_path = m.BackdropPath,
                    vote_average = m.VoteAverage,
                    vote_count = m.VoteCount,
                    release_date = m.ReleaseDate
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                results,
                total_results = totalResults,
                total_pages = totalPages
            });
        }

        // GET /api/movies/{id}
        [HttpGet("movies/{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> Get(int id, CancellationToken ct)
        {
            var movie = await _db.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Videos)
                .Include(m => m.Cast).ThenInclude(c => c.Person)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            // ---- IMDb id from dbo.Links (MovieLens mapping) ----
            var link = await _db.Links
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.MovieId == id, ct);

            // MovieLens imdbId is numeric -> IMDb uses "tt" + zero padding (7 digits)
            string? imdb_id = null;
            if (link?.ImdbId != null)
            {
                imdb_id = "tt" + link.ImdbId.Value.ToString().PadLeft(7, '0');
            }

            var genres = movie.MovieGenres
                .Select(mg => new { id = mg.GenreId, name = mg.Genre.Name })
                .ToList();

            var credits = new
            {
                cast = movie.Cast
                    .OrderBy(c => c.CastOrder)
                    .Select(c => new
                    {
                        id = c.PersonId,
                        name = c.Person.Name,
                        character = c.Character,
                        profile_path = c.Person.ProfilePath
                    })
                    .ToList()
            };

            var videos = new
            {
                results = movie.Videos
                    .Select(v => new
                    {
                        key = v.Key,
                        site = v.Site,
                        type = v.Type,
                        name = v.Name
                    })
                    .ToList()
            };

            return Ok(new
            {
                id = movie.MovieId,
                tmdb_id = movie.TmdbId,
                imdb_id,
                title = movie.Title,
                genres,
                overview = movie.Overview,
                poster_path = movie.PosterPath,
                backdrop_path = movie.BackdropPath,
                vote_average = movie.VoteAverage,
                vote_count = movie.VoteCount,
                runtime = movie.Runtime,
                release_date = movie.ReleaseDate,
                original_language = movie.OriginalLanguage,
                credits,
                videos
            });
        }

        // POST /api/admin/movies
        [HttpPost("admin/movies")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Movie>> Create([FromBody] Movie movie)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync();

            // point to the PUBLIC Get action
            return CreatedAtAction(nameof(Get), new { id = movie.MovieId }, movie);
        }

        // PUT /api/admin/movies/{id}
        [HttpPut("admin/movies/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Movie movie)
        {
            if (id != movie.MovieId)
                return BadRequest(new { message = "Movie ID mismatch" });

            var existing = await _db.Movies.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Movie not found" });

            _db.Entry(existing).CurrentValues.SetValues(movie);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/admin/movies/{id}
        [HttpDelete("admin/movies/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _db.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            _db.Movies.Remove(movie);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}