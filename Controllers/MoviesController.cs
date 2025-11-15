using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using MovieApi.Models;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController : ControllerBase
    {
        private readonly MovieContext _db;
        public MoviesController(MovieContext db) => _db = db;

        // GET /api/movies
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var movies = await _db.Movies.AsNoTracking().ToListAsync();
            return Ok(movies);
        }

        // GET /api/movies/{id}
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOne(int id)
        {
            var movie = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            return movie is null ? NotFound(new { message = "Movie not found" }) : Ok(movie);
        }

        // POST /api/movies
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Movie movie)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOne), new { id = movie.Id }, movie);
        }

        // PUT /api/movies/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Movie movie)
        {
            if (id != movie.Id)
                return BadRequest(new { message = "Movie ID mismatch" });

            var existing = await _db.Movies.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Movie not found" });

            _db.Entry(existing).CurrentValues.SetValues(movie);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/movies/{id}
        [HttpDelete("{id:int}")]
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
