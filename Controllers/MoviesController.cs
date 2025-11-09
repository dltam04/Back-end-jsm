using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieApi.Models;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly MovieContext _db;
    public MoviesController(MovieContext db) => _db = db;

    // GET /api/movies
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Movies.AsNoTracking().ToListAsync());

    // GET /api/movies/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var movie = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        return movie is null ? NotFound() : Ok(movie);
    }

    // POST /api/movies
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Movie movie)
    {
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = movie.Id }, movie);
    }
}
