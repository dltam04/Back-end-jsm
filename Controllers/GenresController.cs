using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/genres")]
    [AllowAnonymous]
    public class GenresController : ControllerBase
    {
        private readonly MovieContext _context;

        public GenresController(MovieContext context)
        {
            _context = context;
        }

        // GET /api/genres
        [HttpGet]
        public async Task<ActionResult<object>> GetGenres()
        {
            var genres = await _context.Genres
                .AsNoTracking()
                .Select(g => new
                {
                    id = g.GenreId,
                    name = g.Name
                })
                .ToListAsync();

            return Ok(new { genres });
        }
    }
}
