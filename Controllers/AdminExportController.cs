using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MovieApi.Data;

namespace MovieApi.Controllers
{
    [ApiController]
    [Route("api/admin/export")]
    [Authorize(Roles = "Admin")]
    public class AdminExportController : ControllerBase
    {
        private readonly MovieContext _db;

        public AdminExportController(MovieContext db)
        {
            _db = db;
        }

        // ----------------------------
        // Helpers
        // ----------------------------
        private static string CsvEscape(object? value)
        {
            if (value == null) return "";
            var s = Convert.ToString(value) ?? "";

            // Escape for CSV: wrap in quotes if contains special chars, double inner quotes
            var mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            if (s.Contains('"')) s = s.Replace("\"", "\"\"");

            return mustQuote ? $"\"{s}\"" : s;
        }

        private async Task StreamCsvFromSql(
            string fileName,
            string sql,
            CancellationToken ct)
        {
            Response.StatusCode = 200;
            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            await using var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 0; // long exports

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Header row
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(",");
                await writer.WriteAsync(CsvEscape(reader.GetName(i)));
            }
            await writer.WriteLineAsync();

            // Data rows
            while (await reader.ReadAsync(ct))
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) await writer.WriteAsync(",");
                    var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                    await writer.WriteAsync(CsvEscape(val));
                }
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync();
        }

        // ----------------------------
        // Export endpoints
        // ----------------------------

        // GET /api/admin/export/movies
        [HttpGet("movies")]
        public async Task<IActionResult> ExportMovies(CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    MovieId,
                    Title,
                    Overview,
                    ReleaseDate,
                    PosterPath,
                    BackdropPath,
                    VoteAverage,
                    VoteCount,
                    Popularity,
                    OriginalLanguage,
                    Runtime,
                    CreatedAt,
                    UpdatedAt,
                    TmdbId,
                    MovieLensGenres
                FROM dbo.Movies
                ORDER BY MovieId;
                ";
            await StreamCsvFromSql("movies.csv", sql, ct);
            return new EmptyResult();
        }

        // GET /api/admin/export/links
        [HttpGet("links")]
        public async Task<IActionResult> ExportLinks(CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    MovieId,
                    imdbId,
                    tmdbId
                FROM dbo.links
                ORDER BY MovieId;
                ";
            await StreamCsvFromSql("links.csv", sql, ct);
            return new EmptyResult();
        }

        // GET /api/admin/export/ratings
        [HttpGet("ratings")]
        public async Task<IActionResult> ExportRatings(CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    UserId,
                    MovieId,
                    rating,
                    timestamp
                FROM dbo.ratings
                ORDER BY UserId, MovieId;
                ";
            await StreamCsvFromSql("ratings.csv", sql, ct);
            return new EmptyResult();
        }

        // GET /api/admin/export/tags
        [HttpGet("tags")]
        public async Task<IActionResult> ExportTags(CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    UserId,
                    MovieId,
                    tag,
                    timestamp
                FROM dbo.tags
                ORDER BY UserId, MovieId;
                ";
            await StreamCsvFromSql("tags.csv", sql, ct);
            return new EmptyResult();
        }

        // OPTIONAL: GET /api/admin/export/movies-32m
        [HttpGet("movies-32m")]
        public async Task<IActionResult> ExportMovies32m(CancellationToken ct)
        {
            const string sql = @"
                SELECT
                    movieId,
                    title,
                    genres
                FROM dbo.movies_32m
                ORDER BY movieId;
                ";
            await StreamCsvFromSql("movies_32m.csv", sql, ct);
            return new EmptyResult();
        }
    }
}
