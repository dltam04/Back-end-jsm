/* Authorization to know which APIs can be used 
    -> Bearer box only working as place to fill in the accessToken
    true work only show when you receive the status from call each api
    - 200: Ok
    - 201: created
    - 204: no content
    - 400: bad request
    - 401: unauthorized
    - 403: forbiden
    - 404: not found
    - 409: conflict
    - 500: internal server error
*/

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using MovieApi.Models.Api;
using MovieApi.Handlers;
using MovieApi.Services;
using MovieApi.Models;
using MovieApi.Data;
using System.Linq;
using System;

namespace MovieApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly MovieContext _db;
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public AccountController(JwtService jwtService, MovieContext db, EmailService email, IConfiguration config)
        {
            _jwtService = jwtService;
            _db = db;
            _email = email;
            _config = config;
        }

        // Login

        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<ActionResult<LoginResponseModel>> Login([FromBody] LoginRequestModel request)
        {
            var result = await _jwtService.Authenticate(request);

            // keep your existing behavior for now
            if (result is null)
                return NotFound(new { message = "Invalid credentials" });

            return Ok(result);
        }

        // Register

        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestModel request)
        {
            var fullName = request.FullName?.Trim();
            var email = request.Email?.Trim().ToLowerInvariant();
            var username = request.Username?.Trim();
            var password = request.Password;
            var confirm = request.ConfirmPassword;

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirm))
            {
                return BadRequest(new { message = "Invalid request" });
            }

            if (password != confirm)
                return BadRequest(new { message = "Passwords do not match" });

            // FullName rules
            if (fullName.Length > 80)
                return BadRequest(new { message = "Full name is too long" });

            // Username rules
            if (username.Length < 3 || username.Length > 30)
                return BadRequest(new { message = "Username must be 3–30 characters" });

            if (username.Any(char.IsWhiteSpace))
                return BadRequest(new { message = "Username cannot contain spaces" });

            if (!username.All(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-'))
                return BadRequest(new { message = "Username contains invalid characters" });

            // Email rules (lightweight)
            if (email.Length > 254)
                return BadRequest(new { message = "Email is too long" });

            if (!email.Contains("@") || email.StartsWith("@") || email.EndsWith("@"))
                return BadRequest(new { message = "Invalid email format" });

            // Password policy
            if (password.Length < 8 || password.Length > 64)
                return BadRequest(new { message = "Password must be 8–64 characters" });

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

            if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                return BadRequest(new { message = "Password must include upper, lower, number, and symbol" });

            // Common password block (starter list)
            var commonPasswords = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "12345678", "qwerty", "11111111", "admin123", "letmein", "123456789", "00000000"
            };
            if (commonPasswords.Contains(password))
                return BadRequest(new { message = "Password is too common" });

            // Uniqueness checks
            if (await _db.UserAccounts.AnyAsync(u => u.UserName == username))
                return Conflict(new { message = "Username already exists" });

            if (await _db.UserAccounts.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email already exists" });

            // Create user (unverified)
            var user = new UserAccount
            {
                UserName = username,
                FullName = fullName,
                Email = email,
                Password = PasswordHashHandler.HashPassword(password),
                Role = "User",
                IsEmailVerified = false,
                EmailVerifiedAt = null
            };

            await _db.UserAccounts.AddAsync(user);
            await _db.SaveChangesAsync();

            // Create verification token (store hash only)
            var rawToken = TokenHandler.GenerateToken();
            user.EmailVerificationTokenHash = TokenHandler.Sha256(rawToken);
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);

            await _db.SaveChangesAsync();

            // Build verification link
            var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var verifyLink = $"{frontendBase}/verify-email?token={rawToken}";

            await _email.SendEmailAsync(
                user.Email,
                "Verify your email - Recomovie",
                $@"<p>Hi {user.FullName},</p>
                   <p>Please verify your email by clicking the link below:</p>
                   <p><a href=""{verifyLink}"">{verifyLink}</a></p>
                   <p>This link expires in 24 hours.</p>"
            );

            return StatusCode(201, new
            {
                message = "Registration created. Verification email sent.",
                userId = user.Id
            });
        }

        // Email verification

        [AllowAnonymous]
        [HttpGet("VerifyEmail")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Invalid token" });

            var tokenHash = TokenHandler.Sha256(token);

            var user = await _db.UserAccounts.FirstOrDefaultAsync(u =>
                u.EmailVerificationTokenHash == tokenHash);

            if (user == null)
                return BadRequest(new { message = "Invalid token" });

            if (user.EmailVerificationTokenExpiresAt == null || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "Token expired" });

            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.EmailVerificationTokenHash = null;
            user.EmailVerificationTokenExpiresAt = null;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Email verified successfully" });
        }

        // Resend verification email part

        [AllowAnonymous]
        [HttpPost("ResendVerificationEmail")]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationRequestModel request)
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Invalid request" });

            var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email);

            // Respond OK to avoid account enumeration
            if (user == null)
                return Ok(new { message = "If the email exists, a verification link has been sent." });

            if (user.IsEmailVerified)
                return Ok(new { message = "Email is already verified." });

            // Generate a fresh verification token
            var rawToken = TokenHandler.GenerateToken();
            user.EmailVerificationTokenHash = TokenHandler.Sha256(rawToken);
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);

            await _db.SaveChangesAsync();

            var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var verifyLink = $"{frontendBase}/verify-email?token={rawToken}";

            await _email.SendEmailAsync(
                user.Email,
                "Verify your email - Recomovie",
                $@"<p>Hi {user.FullName},</p>
           <p>Please verify your email by clicking the link below:</p>
           <p><a href=""{verifyLink}"">{verifyLink}</a></p>
           <p>This link expires in 24 hours.</p>"
            );

            return Ok(new { message = "If the email exists, a verification link has been sent." });
        }


        // Forgot password
        [AllowAnonymous]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestModel request)
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Invalid request" });

            var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email);

            // Return OK (prevents account enumeration)
            if (user == null)
                return Ok(new { message = "If the email exists, a reset link has been sent." });

            // Optional: require verified email to reset
            // if (!user.IsEmailVerified)
            //     return Ok(new { message = "If the email exists, a reset link has been sent." });

            var rawToken = TokenHandler.GenerateToken();
            user.PasswordResetTokenHash = TokenHandler.Sha256(rawToken);
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
            user.PasswordResetRequestedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var resetLink = $"{frontendBase}/reset-password?token={rawToken}";

            await _email.SendEmailAsync(
                user.Email,
                "Reset your password - Recomovie",
                $@"<p>Hi {user.FullName},</p>
                <p>Click the link below to reset your password:</p>
                <p><a href=""{resetLink}"">{resetLink}</a></p>
                <p>This link expires in 30 minutes.</p>
                <p>If you didn’t request this, you can ignore this email.</p>"
            );

            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }

        // Reset password (consume token)
        [AllowAnonymous]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestModel request)
        {
            var token = request.Token?.Trim();
            var password = request.NewPassword;
            var confirm = request.ConfirmPassword;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirm))
                return BadRequest(new { message = "Invalid request" });

            if (password != confirm)
                return BadRequest(new { message = "Passwords do not match" });

            // Password policy
            if (password.Length < 8 || password.Length > 64)
                return BadRequest(new { message = "Password must be 8–64 characters" });

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

            if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                return BadRequest(new { message = "Password must include upper, lower, number, and symbol" });

            var commonPasswords = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "12345678", "qwerty", "11111111", "admin123", "letmein", "123456789", "00000000"
            };
            if (commonPasswords.Contains(password))
                return BadRequest(new { message = "Password is too common" });

            var tokenHash = TokenHandler.Sha256(token);

            var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash);
            if (user == null)
                return BadRequest(new { message = "Invalid token" });

            if (user.PasswordResetTokenExpiresAt == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "Token expired" });

            user.Password = PasswordHashHandler.HashPassword(password);

            // Clear reset token
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAt = null;
            user.PasswordResetRequestedAt = null;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully. Please log in." });
        }
    }
}
