using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Models.Api;
using MovieApi.Services;
using MovieApi.Models;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/actors")]
    [AllowAnonymous]
    public class TMDbActorController : ControllerBase
    {
        private readonly MovieContext _db;
        private readonly TMDbSyncService _tmdbSync;
        private const int PageSize = 20;

        public TMDbActorController(MovieContext db, TMDbSyncService tmdbSync)
        {
            _db = db;
            _tmdbSync = tmdbSync;
        }

        // GET /api/actors/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<object>> GetActor(int id)
        {
            // Full details stored
            await _tmdbSync.SyncPersonDetailsAsync(id);

            var person = await _db.People.FirstOrDefaultAsync(p => p.PersonId == id);
            if (person == null) return NotFound();

            return Ok(new
            {
                id = person.PersonId,
                name = person.Name,
                biography = person.Biography,
                profile_path = person.ProfilePath,
                birthday = person.Birthday,
                place_of_birth = person.PlaceOfBirth
            });
        }

        // GET /api/actors/{id}/movies?page=1
        [HttpGet("{id:int}/movies")]
        public async Task<ActionResult<object>> GetMoviesByActorId(
            int id,
            [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;

            var query =
                from mc in _db.MovieCasts
                where mc.PersonId == id
                join m in _db.Movies on mc.MovieId equals m.MovieId
                select m;

            var totalResults = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalResults / (double)PageSize);

            var results = await query
                .OrderByDescending(m => m.Popularity)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(m => new
                {
                    id = m.MovieId,
                    title = m.Title,
                    overview = m.Overview,
                    poster_path = m.PosterPath,
                    backdrop_path = m.BackdropPath,
                    vote_average = m.VoteAverage,
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
    }
}
