using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Services;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/admin/tmdb-import")]
    [Authorize(Roles = "Admin")]
    public class TMDbImportController : ControllerBase
    {
        private readonly TMDbSyncService _tmdbSync;

        public TMDbImportController(TMDbSyncService tmdbSync)
        {
            _tmdbSync = tmdbSync;
        }

        // POST /api/admin/tmdb-import/movies/{id}
        // Force-sync one movie’s full details (runtime, cast, videos…)
        [HttpPost("movies/{id:int}")]
        public async Task<IActionResult> ImportMovieDetails(int id)
        {
            await _tmdbSync.SyncMovieDetailsAsync(id);
            return NoContent();
        }

        // POST /api/admin/tmdb-import/people-details?max=200
        // Bulk-sync extra info for people already in dbo.People (bio, birthday, place_of_birth)
        [HttpPost("people-details")]
        public async Task<IActionResult> SyncPeopleDetails([FromQuery] int max = 200)
        {
            var processed = await _tmdbSync.SyncAllPeopleDetailsAsync(max);
            return Ok(new { processed });
        }

        /* POST /api/admin/tmdb-import/sync-from-links
            - startAfterMovieId: start after this MovieLens id (for chunking)
            - maxMovies: limit how many links to process in this call
            - batchSize: how often to SaveChanges (default 200)
        */
        [HttpPost("sync-from-links")]
        public async Task<IActionResult> SyncFromLinks(
            [FromQuery] int? startAfterMovieId = null,
            [FromQuery] int? maxMovies = null,
            [FromQuery] int batchSize = 200)
        {
            var ct = HttpContext.RequestAborted;

            var processed = await _tmdbSync.SyncMoviesFromLinksAsync(
                startAfterMovieId: startAfterMovieId,
                maxMovies: maxMovies,
                batchSize: batchSize,
                ct: ct);

            return Ok(new
            {
                processed,
                startAfterMovieId,
                maxMovies,
                batchSize
            });
        }

        // POST /api/admin/tmdb-import/movies/details-bulk?startAfterMovieId=0&maxMovies=500&batchSize=50
        [HttpPost("movies/details-bulk")]
        public async Task<IActionResult> ImportMovieDetailsBulk(
            [FromQuery] int? startAfterMovieId = null,
            [FromQuery] int maxMovies = 1000,
            [FromQuery] int batchSize = 50)
        {
            var processed = await _tmdbSync.SyncMovieDetailsForMoviesAsync(
                startAfterMovieId, maxMovies, batchSize);

            return Ok(new { processed });
        }
    }
}
