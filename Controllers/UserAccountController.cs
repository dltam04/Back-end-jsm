using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieApi.Data;
using MovieApi.Handlers;

namespace MovieApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserAccountController : ControllerBase
    {
        private readonly MovieContext _movieContext;

        public UserAccountController(MovieContext movieContext) => _movieContext = movieContext;

        [HttpGet]
        public async Task<List<UserAccount>> Get()
        {
            return await _movieContext.UserAccounts.ToListAsync();
        }

        [HttpGet("{id}")]

        public async Task<UserAccount?> GetById(int id)
        {
            return await _movieContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == id);
        }

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

            return CreateAtAction(nameof(GetById), new { id = userAccount.Id }, userAccount);
        }

        [HttpPut]

        public async Task
    }
}
