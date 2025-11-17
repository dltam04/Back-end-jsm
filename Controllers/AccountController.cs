using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Models.Api;
using MovieApi.Services;

namespace MovieApi.Controllers
{
    // Generate route based on controller name (final route: /api/Account)
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        /* The controller receives a JwtService instance through constructor injection
           The service is responsible for:
           - validate for username and password
           - verifying user exist in db
           - generate signed JWT token
           - return result

          -> Flow: Controller -> JwtService -> DbContext
        */ 
        private readonly JwtService _jwtService;
        public AccountController(JwtService jwtService) => _jwtService = jwtService;

        [AllowAnonymous] // Key for authorize (User or Admin can access)
        [HttpPost("Login")]
        public async Task<ActionResult<LoginResponseModel>> Login([FromBody] LoginRequestModel request)
        {
            // send request to JwtService.Authenticate()
            var result = await _jwtService.Authenticate(request);
            if (result is null)
                return NotFound(); // return null if credentials are invalid (check from db)

            return Ok(result); // 200 status
        }
    }
}
