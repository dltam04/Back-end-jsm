using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Handlers;
using MovieApi.Models;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserAccountController : ControllerBase
    {
        private readonly MovieContext _movieContext;

        public UserAccountController(MovieContext movieContext) => _movieContext = movieContext;

        // GET /api/UserAccount
        [HttpGet]
        public async Task<List<UserAccount>> Get()
        {
            return await _movieContext.UserAccounts.ToListAsync();
        }

        // GET /api/UserAccount/{id}
        [HttpGet("{id}")]

        public async Task<UserAccount?> GetById(int id)
        {
            return await _movieContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == id);
        }

        // POST /api/UserAccount
        [HttpPost]

        public async Task<ActionResult> Create([FromBody] UserAccount userAccount)
        {
            if (string.IsNullOrWhiteSpace (userAccount.FullName) ||
                string.IsNullOrWhiteSpace (userAccount.UserName) ||
                string.IsNullOrWhiteSpace(userAccount.Password))
            {
                return BadRequest("Invalid Request");
            }

            userAccount.Password = PasswordHashHandler.HashPassword(userAccount.Password);
            await _movieContext.UserAccounts.AddAsync(userAccount);
            await _movieContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = userAccount.Id }, userAccount);
        }

        // PUT /api/UserAccount/{id}
        [HttpPut]

        public async Task<ActionResult> Update([FromBody] UserAccount userAccount)
        {
            if (userAccount.Id == 0 ||
                string.IsNullOrWhiteSpace(userAccount.FullName) ||
                string.IsNullOrWhiteSpace (userAccount.UserName) ||
                string.IsNullOrWhiteSpace (userAccount.Password))
            {
                return BadRequest("Invalid Request");
            }

            userAccount.Password = PasswordHashHandler.HashPassword (userAccount.Password);
            _movieContext.UserAccounts.Update(userAccount);
            await _movieContext.SaveChangesAsync();

            return Ok(userAccount);
        }

        // DELETE /api/UserAccount/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var user = await _movieContext.UserAccounts.FindAsync(id);
            if (user == null)
            {
                return NotFound($"User with ID {id} not found.");
            }

            _movieContext.UserAccounts.Remove(user);
            await _movieContext.SaveChangesAsync();

            return Ok($"User with ID {id} has been deleted.");
        }
    }
}
