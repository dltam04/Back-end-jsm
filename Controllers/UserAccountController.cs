using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieApi.Handlers;
using MovieApi.Models;
using MovieApi.Data;

// DTOs block
using System;
using System.Linq;
using MovieApi.Models.Api;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MovieApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserAccountController : ControllerBase
    {
        private readonly MovieContext _movieContext;

        public UserAccountController(MovieContext movieContext)
        {
            _movieContext = movieContext;
        }

        // Map a single UserAccount to UserAccountDto
        private async Task<UserAccountDto> MapToDto(UserAccount user)
        {
            var favoriteIds = await _movieContext.UserFavoriteMovies
                .Where(f => f.UserAccountId == user.Id)
                .Select(f => f.MovieId)
                .ToListAsync();

            var watchlistIds = await _movieContext.UserWatchlistMovies
                .Where(w => w.UserAccountId == user.Id)
                .Select(w => w.MovieId)
                .ToListAsync();

            return new UserAccountDto
            {
                Id = user.Id,
                Username = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                Favorites = favoriteIds,
                Watchlist = watchlistIds
            };
        }

        // ---- GET /api/UserAccount ----
        // Returns: id, username, email, favorites, watchlist for each user
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserAccountDto>>> GetAll()
        {
            var users = await _movieContext.UserAccounts
                .AsNoTracking()
                .ToListAsync();

            // EGet all favorites & watchlists
            var userIds = users.Select(u => u.Id).ToList();

            var favoritesLookup = await _movieContext.UserFavoriteMovies
                .Where(f => userIds.Contains(f.UserAccountId))
                .GroupBy(f => f.UserAccountId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(f => f.MovieId).ToList()
                );

            var watchlistLookup = await _movieContext.UserWatchlistMovies
                .Where(w => userIds.Contains(w.UserAccountId))
                .GroupBy(w => w.UserAccountId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(w => w.MovieId).ToList()
                );

            var result = users.Select(u => new UserAccountDto
            {
                Id = u.Id,
                Username = u.UserName,
                FullName = u.FullName,
                Email = u.Email,
                Favorites = favoritesLookup.TryGetValue(u.Id, out var favs) ? favs : new List<int>(),
                Watchlist = watchlistLookup.TryGetValue(u.Id, out var wl) ? wl : new List<int>()
            }).ToList();

            return Ok(result);
        }

        // ---- GET /api/UserAccount/{id} ----
        // Returns: id, username, email, favorites, watchlist
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserAccountDto>> GetById(int id)
        {
            var user = await _movieContext.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
                return NotFound(new { message = "User not found" });

            var dto = await MapToDto(user);
            return Ok(dto);
        }

        // ---- POST /api/UserAccount ----
        // Body: username, fullname, email, password(raw), optional favorites[], watchlist[]
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UserAccountDto>> Create([FromBody] UserAccountCreateUpdateDto request)
        {
            // ---------- Normalize ----------
            var username = request.Username?.Trim();
            var email = request.Email?.Trim().ToLowerInvariant();
            var fullName = request.FullName?.Trim();
            var password = request.Password; // POST requires it

            // ---------- Required fields (POST requires password + fullName) ----------
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { message = "Invalid request" });
            }

            // ---------- FullName rules ----------
            if (fullName.Length > 80)
                return BadRequest(new { message = "Full name is too long" });

            // ---------- Username rules ----------
            if (username.Length < 3 || username.Length > 30)
                return BadRequest(new { message = "Username must be 3–30 characters" });

            if (username.Any(char.IsWhiteSpace))
                return BadRequest(new { message = "Username cannot contain spaces" });

            if (!username.All(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-'))
                return BadRequest(new { message = "Username contains invalid characters" });

            // ---------- Email rules (lightweight) ----------
            if (email.Length > 254)
                return BadRequest(new { message = "Email is too long" });

            if (!email.Contains("@") || email.StartsWith("@") || email.EndsWith("@"))
                return BadRequest(new { message = "Invalid email format" });

            // ---------- Password policy ----------
            if (password.Length < 8 || password.Length > 64)
                return BadRequest(new { message = "Password must be 8–64 characters" });

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

            if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                return BadRequest(new { message = "Password must include upper, lower, number, and symbol" });

            // ---------- Common password block (starter list) ----------
            var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "12345678", "qwerty", "11111111", "admin123", "letmein", "123456789", "00000000"
    };

            if (commonPasswords.Contains(password))
                return BadRequest(new { message = "Password is too common" });

            // ---------- Uniqueness checks ----------
            var usernameExists = await _movieContext.UserAccounts.AnyAsync(u => u.UserName == username);
            if (usernameExists)
                return Conflict(new { message = "Username already exists" });

            var emailExists = await _movieContext.UserAccounts.AnyAsync(u => u.Email == email);
            if (emailExists)
                return Conflict(new { message = "Email already exists" });

            // ---------- Create user ----------
            var user = new UserAccount
            {
                UserName = username,
                FullName = fullName,
                Email = email,
                Password = PasswordHashHandler.HashPassword(password),
                Role = "User"
            };

            await _movieContext.UserAccounts.AddAsync(user);
            await _movieContext.SaveChangesAsync(); // get Id

            // ---------- Initial favorites ----------
            if (request.Favorites != null && request.Favorites.Count > 0)
            {
                var favEntities = request.Favorites
                    .Distinct()
                    .Select(movieId => new UserFavoriteMovie
                    {
                        UserAccountId = user.Id,
                        MovieId = movieId
                    });

                await _movieContext.UserFavoriteMovies.AddRangeAsync(favEntities);
            }

            // ---------- Initial watchlist ----------
            if (request.Watchlist != null && request.Watchlist.Count > 0)
            {
                var wlEntities = request.Watchlist
                    .Distinct()
                    .Select(movieId => new UserWatchlistMovie
                    {
                        UserAccountId = user.Id,
                        MovieId = movieId
                    });

                await _movieContext.UserWatchlistMovies.AddRangeAsync(wlEntities);
            }

            await _movieContext.SaveChangesAsync();

            var dto = await MapToDto(user);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }



        // ---- PUT /api/UserAccount/{id} ----
        // Body: username, fullname, email, password(optional), optional favorites[], watchlist[]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<UserAccountDto>> Update(int id, [FromBody] UserAccountCreateUpdateDto request)
        {
            // ---------- Normalize ----------
            var username = request.Username?.Trim();
            var email = request.Email?.Trim().ToLowerInvariant();
            var fullName = request.FullName?.Trim();
            var password = request.Password; // optional for PUT

            // Require username + email (fullName can be optional)
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Invalid request" });

            var user = await _movieContext.UserAccounts.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            // ---------- Username rules (same as POST) ----------
            if (username.Length < 3 || username.Length > 30)
                return BadRequest(new { message = "Username must be 3–30 characters" });

            if (username.Any(char.IsWhiteSpace))
                return BadRequest(new { message = "Username cannot contain spaces" });

            if (!username.All(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-'))
                return BadRequest(new { message = "Username contains invalid characters" });

            // ---------- Email rules ----------
            if (email.Length > 254)
                return BadRequest(new { message = "Email is too long" });

            if (!email.Contains("@") || email.StartsWith("@") || email.EndsWith("@"))
                return BadRequest(new { message = "Invalid email format" });

            // ---------- Uniqueness checks (exclude current user) ----------
            var usernameExists = await _movieContext.UserAccounts.AnyAsync(u => u.Id != id && u.UserName == username);
            if (usernameExists)
                return Conflict(new { message = "Username already exists" });

            var emailExists = await _movieContext.UserAccounts.AnyAsync(u => u.Id != id && u.Email == email);
            if (emailExists)
                return Conflict(new { message = "Email already exists" });

            // ---------- Update basic fields ----------
            user.UserName = username;
            user.Email = email;

            // Fullname update
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                if (fullName.Length > 80)
                    return BadRequest(new { message = "Full name is too long" });

                user.FullName = fullName;
            }

            // Password optional for PUT
            if (!string.IsNullOrWhiteSpace(password))
            {
                // Apply same password policy as POST
                if (password.Length < 8 || password.Length > 64)
                    return BadRequest(new { message = "Password must be 8–64 characters" });

                bool hasUpper = password.Any(char.IsUpper);
                bool hasLower = password.Any(char.IsLower);
                bool hasDigit = password.Any(char.IsDigit);
                bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

                if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                    return BadRequest(new { message = "Password must include upper, lower, number, and symbol" });

                var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "12345678", "qwerty", "11111111", "admin123", "letmein", "123456789", "00000000"
        };

                if (commonPasswords.Contains(password))
                    return BadRequest(new { message = "Password is too common" });

                user.Password = PasswordHashHandler.HashPassword(password);
            }

            // ---------- Replace favorites ----------
            var existingFavs = _movieContext.UserFavoriteMovies.Where(f => f.UserAccountId == id);
            _movieContext.UserFavoriteMovies.RemoveRange(existingFavs);

            if (request.Favorites != null && request.Favorites.Count > 0)
            {
                var favEntities = request.Favorites
                    .Distinct()
                    .Select(movieId => new UserFavoriteMovie
                    {
                        UserAccountId = id,
                        MovieId = movieId
                    });

                await _movieContext.UserFavoriteMovies.AddRangeAsync(favEntities);
            }

            // ---------- Replace watchlist ----------
            var existingWls = _movieContext.UserWatchlistMovies.Where(w => w.UserAccountId == id);
            _movieContext.UserWatchlistMovies.RemoveRange(existingWls);

            if (request.Watchlist != null && request.Watchlist.Count > 0)
            {
                var wlEntities = request.Watchlist
                    .Distinct()
                    .Select(movieId => new UserWatchlistMovie
                    {
                        UserAccountId = id,
                        MovieId = movieId
                    });

                await _movieContext.UserWatchlistMovies.AddRangeAsync(wlEntities);
            }

            await _movieContext.SaveChangesAsync();

            var dto = await MapToDto(user);
            return Ok(dto);
        }


        // ---- DELETE /api/UserAccount/{id} ----
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var user = await _movieContext.UserAccounts.FindAsync(id);
            if (user == null)
                return NotFound(new { message = $"User with ID {id} not found." });

            // Remove related favorites & watchlist
            var favs = _movieContext.UserFavoriteMovies.Where(f => f.UserAccountId == id);
            var wls = _movieContext.UserWatchlistMovies.Where(w => w.MovieId == id);

            _movieContext.UserFavoriteMovies.RemoveRange(favs);
            _movieContext.UserWatchlistMovies.RemoveRange(wls);

            _movieContext.UserAccounts.Remove(user);
            await _movieContext.SaveChangesAsync();

            return NoContent();
        }
    }
}
