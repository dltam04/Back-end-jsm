using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using MovieApi.Models.Api;
using MovieApi.Handlers;
using MovieApi.Models;
using MovieApi.Data;
using System.Text;
using System;

namespace MovieApi.Services
{
    public class JwtService
    {
        private readonly MovieContext _movieContext;
        private readonly IConfiguration _configuration;

        public JwtService(MovieContext movieContext, IConfiguration configuration)
        {
            _movieContext = movieContext;
            _configuration = configuration;
        }

        // Authenticate user and Generate JWT Token
        public async Task<LoginResponseModel?> Authenticate(LoginRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return null;

            // Retrieve user from DB
            var userAccount = await _movieContext.UserAccounts
                .FirstOrDefaultAsync(x => x.UserName == request.Username);

            if (userAccount is null)
                return null;

            // Verify password
            if (request.Password != userAccount.Password)
                return null;


            // JWT Configuration
            var issuer = _configuration["JwtConfig:Issuer"];
            var audience = _configuration["JwtConfig:Audience"];
            var key = _configuration["JwtConfig:Key"];
            var tokenValidityMins =
                _configuration.GetValue<int?>("JwtConfig:TokenValidityMins") ?? 60;

            var tokenExpiryTimeStamp = DateTime.UtcNow.AddMinutes(tokenValidityMins);

            // Claims (add user info and roles)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userAccount.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, userAccount.UserName!),
                new Claim(ClaimTypes.Name, userAccount.UserName!),
                new Claim(ClaimTypes.Role, userAccount.Role ?? "User"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // unique token ID
            };

            // Token Creation
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = tokenExpiryTimeStamp,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(securityToken);

            // Return response
            return new LoginResponseModel
            {
                AccessToken = accessToken,
                Username = userAccount.UserName!,
                Role = userAccount.Role ?? "User",
                ExpiresIn = (int)(tokenExpiryTimeStamp - DateTime.UtcNow).TotalSeconds
            };
        }
    }
}
