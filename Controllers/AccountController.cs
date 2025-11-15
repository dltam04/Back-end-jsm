using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Models.Api;
using MovieApi.Services;

namespace MovieApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly JwtService _jwtService;
        public AccountController(JwtService jwtService) => _jwtService = jwtService;

        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<ActionResult<LoginResponseModel>> Login([FromBody] LoginRequestModel request)
        {
            var result = await _jwtService.Authenticate(request);
            if (result is null)
                return NotFound();

            return Ok(result);
        }
    }
}
