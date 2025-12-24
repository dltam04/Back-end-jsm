using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieApi.Data;
using MovieApi.Models;
using MovieApi.Models.Api;

namespace MovieApi.Services
{
    public class TMDbSyncService
    {
        private readonly MovieContext _db;
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public TMDbSyncService(
            MovieContext db,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _db = db;
            _http = httpClientFactory.CreateClient("tmdb");
            _apiKey = configuration["TMDb:ApiKey"]
                      ?? throw new InvalidOperationException("TMDb:ApiKey missing");
            _baseUrl = configuration["TMDb:BaseUrl"] ?? "https://api.themoviedb.org/3";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        // --------------------------------------------------------------------
        // Generic helpers
        // --------------------------------------------------------------------
        private async Task<T> GetAsync<T>(string pathAndQuery)
        {
            var separator = pathAndQuery.Contains('?') ? '&' : '?';
            var url = $"{_baseUrl}{pathAndQuery}{separator}api_key={_apiKey}";

            using var response = await _http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"TMDb call failed.\nURL: {url}\nStatus: {(int)response.StatusCode} {response.StatusCode}\nBody: {body}");
            }

            var data = JsonSerializer.Deserialize<T>(body, _jsonOptions);
            if (data == null)
                throw new Exception($"Failed to deserialize TMDb response for {pathAndQuery}");

            return data;
        }

        // Like GetAsync, but returns null on 404 instead of throwing.
        private async Task<T?> TryGetAsync<T>(string pathAndQuery) where T : class
        {
            var separator = pathAndQuery.Contains('?') ? '&' : '?';
            var url = $"{_baseUrl}{pathAndQuery}{separator}api_key={_apiKey}";

            using var response = await _http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"TMDb call failed.\nURL: {url}\nStatus: {(int)response.StatusCode} {response.StatusCode}\nBody: {body}");
            }

            var data = JsonSerializer.Deserialize<T>(body, _jsonOptions);
            if (data == null)
                throw new Exception($"Failed to deserialize TMDb response for {pathAndQuery}");

            return data;
        }

        // --------------------------------------------------------------------
        // 1. Sync genres
        // --------------------------------------------------------------------
        public async Task SyncGenresAsync()
        {
            var tmdbGenres = await GetAsync<TMDbGenreListResponse>("/genre/movie/list");

            foreach (var g in tmdbGenres.Genres)
            {
                var existing = await _db.Genres.FindAsync(g.Id);
                if (existing == null)
                {
                    _db.Genres.Add(new Genre
                    {
                        GenreId = g.Id,
                        Name = g.Name
                    });
                }
                else
                {
                    existing.Name = g.Name;
                }
            }

            await _db.SaveChangesAsync();
        }

        // --------------------------------------------------------------------
        // 2. Sync list movies (popular / top_rated / upcoming)
        // --------------------------------------------------------------------
        public async Task SyncMoviesForListAsync(string listType, int pages)
        {
            for (int page = 1; page <= pages; page++)
            {
                var listResponse =
                    await GetAsync<MovieApi.Models.Api.TMDbMovieListResponse>(
                        $"/movie/{listType}?page={page}");

                var indexOnPage = 0;

                foreach (var m in listResponse.Results)
                {
                    await UpsertBasicMovieAsync(m, listType, page, indexOnPage);
                    indexOnPage++;
                }

                await _db.SaveChangesAsync();
            }
        }

        private async Task<Movie> UpsertBasicMovieAsync(
            MovieApi.Models.Api.TMDbMovieSummary tm,
            string listType,
            int page,
            int indexOnPage)
        {
            var now = DateTime.UtcNow;

            var movie = await _db.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == tm.Id);

            // Parse release date safely
            DateTime? releaseDate = null;
            if (!string.IsNullOrWhiteSpace(tm.ReleaseDate) &&
                DateTime.TryParse(tm.ReleaseDate, out var parsed))
            {
                releaseDate = parsed;
            }

            // Normalise title + language a bit
            string? incomingTitle = !string.IsNullOrWhiteSpace(tm.Title)
                ? tm.Title
                : tm.OriginalTitle;

            if (string.IsNullOrWhiteSpace(incomingTitle))
            {
                incomingTitle = movie?.Title;
            }

            if (string.IsNullOrWhiteSpace(incomingTitle))
            {
                incomingTitle = "(unknown title)";
            }

            var safeOriginalLanguage = string.IsNullOrWhiteSpace(tm.OriginalLanguage)
                ? (movie?.OriginalLanguage ?? "unknown")
                : tm.OriginalLanguage;

            if (movie == null)
            {
                movie = new Movie
                {
                    MovieId = tm.Id,                         // TMDb id as PK here
                    Title = incomingTitle,
                    Overview = tm.Overview,
                    PosterPath = tm.PosterPath,
                    BackdropPath = tm.BackdropPath,
                    ReleaseDate = releaseDate,
                    Popularity = tm.Popularity,
                    VoteAverage = (decimal)tm.VoteAverage,
                    VoteCount = tm.VoteCount,
                    OriginalLanguage = safeOriginalLanguage,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.Movies.Add(movie);
            }
            else
            {
                movie.Title = incomingTitle;
                movie.Overview = tm.Overview ?? movie.Overview;
                movie.PosterPath = tm.PosterPath ?? movie.PosterPath;
                movie.BackdropPath = tm.BackdropPath ?? movie.BackdropPath;
                movie.ReleaseDate = releaseDate ?? movie.ReleaseDate;
                movie.Popularity = tm.Popularity;
                movie.VoteAverage = (decimal)tm.VoteAverage;
                movie.VoteCount = tm.VoteCount;
                movie.OriginalLanguage = safeOriginalLanguage;
                movie.UpdatedAt = now;
            }

            // ----- MovieGenres junction -----
            movie.MovieGenres ??= new List<MovieGenre>();
            movie.MovieGenres.Clear();

            if (tm.GenreIds != null)
            {
                foreach (var gid in tm.GenreIds.Distinct())
                {
                    movie.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = movie.MovieId,
                        GenreId = gid
                    });
                }
            }

            // ---- MovieListEntry (ONE per (MovieId, ListType)) ----
            var listEntry = await _db.MovieListEntries
                .FirstOrDefaultAsync(e => e.MovieId == movie.MovieId
                                          && e.ListType == listType);

            if (listEntry == null)
            {
                listEntry = new MovieListEntry
                {
                    MovieId = movie.MovieId,
                    ListType = listType,
                    Page = page,
                    Position = indexOnPage
                };
                _db.MovieListEntries.Add(listEntry);
            }
            else
            {
                listEntry.Page = page;
                listEntry.Position = indexOnPage;
            }

            return movie;
        }

        // --------------------------------------------------------------------
        // 3. Sync full movie details (videos + credits)
        // --------------------------------------------------------------------
        public async Task SyncMovieDetailsAsync(int movieId)
        {
            // 1) Load local movie row
            var movie = await _db.Movies
                .Include(m => m.Videos)
                .Include(m => m.Cast)
                .FirstOrDefaultAsync(m => m.MovieId == movieId);

            if (movie == null)
            {
                // Nothing to do if we don't even have this movie
                return;
            }

            // 2) Prefer TMDb id, fall back to MovieId if needed
            var tmdbId = movie.TmdbId ?? movieId;

            var details = await TryGetAsync<TMDbMovieDetailResponse>(
                $"/movie/{tmdbId}?append_to_response=videos,credits");

            // Not found on TMDb? just skip
            if (details == null)
                return;

            // ---------------------------
            // 3) Scalar fields (NORMALISED)
            // ---------------------------

            // Title
            string? incomingTitle = !string.IsNullOrWhiteSpace(details.Title)
                ? details.Title
                : details.OriginalTitle;

            if (string.IsNullOrWhiteSpace(incomingTitle))
                incomingTitle = movie.Title;

            if (string.IsNullOrWhiteSpace(incomingTitle))
                incomingTitle = "(unknown title)";

            movie.Title = incomingTitle;

            // Other text fields (use existing value if TMDb sends null)
            movie.Overview = details.Overview ?? movie.Overview;
            movie.PosterPath = details.PosterPath ?? movie.PosterPath;
            movie.BackdropPath = details.BackdropPath ?? movie.BackdropPath;

            // Numbers
            movie.Popularity = details.Popularity;
            movie.VoteAverage = (decimal)details.VoteAverage;
            movie.VoteCount = details.VoteCount;

            // OriginalLanguage – never allow null/empty
            var safeOriginalLanguage = string.IsNullOrWhiteSpace(details.OriginalLanguage)
                ? movie.OriginalLanguage
                : details.OriginalLanguage;

            if (string.IsNullOrWhiteSpace(safeOriginalLanguage))
                safeOriginalLanguage = "unknown";

            movie.OriginalLanguage = safeOriginalLanguage;

            // ReleaseDate
            if (!string.IsNullOrWhiteSpace(details.ReleaseDate) &&
                DateTime.TryParse(details.ReleaseDate, out var parsedDate))
            {
                movie.ReleaseDate = parsedDate;
            }

            // Runtime (only overwrite if TMDb gave a value)
            if (details.Runtime.HasValue)
            {
                movie.Runtime = details.Runtime;
            }

            movie.UpdatedAt = DateTime.UtcNow;

            // ---------------------------
            // 4) Videos (NORMALISED)
            // ---------------------------
            movie.Videos ??= new List<MovieVideo>();
            movie.Videos.Clear();

            if (details.Videos?.Results != null)
            {
                foreach (var v in details.Videos.Results
                             .Where(v => string.Equals(v.Site, "YouTube",
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    var safeKey = string.IsNullOrWhiteSpace(v.Key) ? "missing-key" : v.Key;
                    var safeSite = string.IsNullOrWhiteSpace(v.Site) ? "unknown-site" : v.Site;
                    var safeType = string.IsNullOrWhiteSpace(v.Type) ? "unknown-type" : v.Type;
                    var safeName = string.IsNullOrWhiteSpace(v.Name) ? "unknown-name" : v.Name;

                    movie.Videos.Add(new MovieVideo
                    {
                        MovieId = movie.MovieId,
                        Key = safeKey,
                        Site = safeSite,
                        Type = safeType,
                        Name = safeName
                    });
                }
            }

            // ---------------------------
            // 5) Cast + People (NORMALISED)
            // ---------------------------
            movie.Cast ??= new List<MovieCast>();
            movie.Cast.Clear();

            if (details.Credits?.Cast != null)
            {
                foreach (var c in details.Credits.Cast.Take(20))
                {
                    var safePersonName = string.IsNullOrWhiteSpace(c.Name)
                        ? "(unknown person)"
                        : c.Name;

                    // PersonId = TMDb person id
                    var person = await _db.People
                        .FirstOrDefaultAsync(p => p.PersonId == c.Id);

                    if (person == null)
                    {
                        person = new Person
                        {
                            PersonId = c.Id,
                            Name = safePersonName,
                            ProfilePath = c.ProfilePath
                        };
                        _db.People.Add(person);
                    }
                    else
                    {
                        // Make sure Name is never null/empty in DB
                        person.Name = safePersonName;
                        person.ProfilePath ??= c.ProfilePath;
                    }

                    movie.Cast.Add(new MovieCast
                    {
                        MovieId = movie.MovieId,      // MovieLens id
                        PersonId = person.PersonId,    // TMDb person id
                        Character = c.Character,
                        CastOrder = c.Order
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        // --------------------------------------------------------------------
        // 3b. Bulk: sync details + cast for many movies
        // --------------------------------------------------------------------
        public async Task<int> SyncMovieDetailsForMoviesAsync(
            int? startAfterMovieId = null,
            int maxMovies = 1000,
            int batchSize = 50,
            CancellationToken ct = default)
        {
            if (maxMovies <= 0) return 0;
            if (batchSize <= 0) batchSize = 50;

            // 1) Base query: only movies that actually exist (optionally starting after some id)
            var query = _db.Movies.AsNoTracking();

            if (startAfterMovieId.HasValue)
            {
                query = query.Where(m => m.MovieId > startAfterMovieId.Value);
            }

            // Optional but recommended: only movies that have a TMDb id
            query = query.Where(m => m.TmdbId != null);

            var movieIds = await query
                .OrderBy(m => m.MovieId)
                .Select(m => m.MovieId)
                .Take(maxMovies)
                .ToListAsync(ct);

            if (movieIds.Count == 0)
                return 0;

            var processed = 0;

            foreach (var id in movieIds)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await SyncMovieDetailsAsync(id);
                    processed++;
                }
                catch (Exception ex)
                {
                    // log and keep going
                    Console.WriteLine(
                        $"[DETAILS BULK] Movie {id} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return processed;
        }

        // --------------------------------------------------------------------
        // 4. Sync person details
        // --------------------------------------------------------------------
        public async Task<Person?> SyncPersonDetailsAsync(int personId)
        {
            // Load existing person (may be null if we only know them from credits)
            var person = await _db.People.FirstOrDefaultAsync(p => p.PersonId == personId);

            // If we have no row OR important fields are missing, call TMDb
            if (person == null || person.Biography == null ||
                person.Birthday == null || person.PlaceOfBirth == null)
            {
                var details = await GetAsync<TMDbPersonDetailsResponse>($"/person/{personId}");

                if (details == null)
                {
                    return person; // nothing we can do
                }

                if (person == null)
                {
                    person = new Person
                    {
                        PersonId = details.Id,
                    };
                    _db.People.Add(person);
                }

                person.Name = details.Name ?? person.Name;
                person.ProfilePath = details.ProfilePath ?? person.ProfilePath;
                person.Biography = details.Biography;
                person.PlaceOfBirth = details.PlaceOfBirth;

                if (!string.IsNullOrWhiteSpace(details.Birthday) &&
                    DateTime.TryParse(details.Birthday, out var bday))
                {
                    person.Birthday = bday;
                }

                await _db.SaveChangesAsync();
            }

            return person;
        }

        public async Task<int> SyncAllPeopleDetailsAsync(int max = 200)
        {
            // Pick people that are still missing some interesting info
            var toUpdate = await _db.People
                .Where(p =>
                    p.Biography == null ||
                    p.Birthday == null ||
                    p.PlaceOfBirth == null)
                .OrderBy(p => p.PersonId)
                .Take(max)
                .ToListAsync();

            int processed = 0;

            foreach (var p in toUpdate)
            {
                await SyncPersonDetailsAsync(p.PersonId);
                processed++;
            }

            return processed;
        }

        // --------------------------------------------------------------------
        // 5. TMDb account lists
        // --------------------------------------------------------------------
        public async Task<TMDbMovieListResponse> GetAccountMovieListAsync(
            int tmdbAccountId,
            string listName,
            string? sessionId,
            int page = 1)
        {
            if (string.IsNullOrWhiteSpace(listName))
                throw new ArgumentException("listName is required", nameof(listName));

            if (page < 1) page = 1;

            // If sessionId is not passed, try to use what we stored in DB
            var effectiveSessionId = sessionId;
            if (string.IsNullOrWhiteSpace(effectiveSessionId))
            {
                var account = await _db.TMDbAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TMDbAccountId == tmdbAccountId);

                if (account == null || string.IsNullOrWhiteSpace(account.SessionId))
                    throw new InvalidOperationException("No session id available for this TMDb account.");

                effectiveSessionId = account.SessionId;
            }

            // TMDb endpoint: /account/{id}/{listName}?session_id=...&page=...
            var path =
                $"/account/{tmdbAccountId}/{listName}?session_id={effectiveSessionId}&page={page}";

            return await GetAsync<TMDbMovieListResponse>(path);
        }

        // --------------------------------------------------------------------
        // 6. Sync Movies from MovieLens links (MovieId = MovieLens, TmdbId = TMDb)
        // --------------------------------------------------------------------
        public async Task<int> SyncMoviesFromLinksAsync(
            int? startAfterMovieId = null,
            int? maxMovies = null,
            int batchSize = 200,
            CancellationToken ct = default)
        {
            if (batchSize <= 0) batchSize = 200;

            // 1. Base query over dbo.links
            var query = _db.Links
                .AsNoTracking()
                .Where(l => l.TmdbId != null);

            if (startAfterMovieId.HasValue)
            {
                query = query.Where(l => l.MovieId > startAfterMovieId.Value);
            }

            query = query.OrderBy(l => l.MovieId);

            if (maxMovies.HasValue)
            {
                query = query.Take(maxMovies.Value);
            }

            var links = await query.ToListAsync(ct);

            var processed = 0;
            var now = DateTime.UtcNow;

            foreach (var link in links)
            {
                ct.ThrowIfCancellationRequested();

                var movieLensId = link.MovieId;
                var tmdbId = link.TmdbId!.Value;

                // 2. Call TMDb for full movie details
                MovieApi.Models.Api.TMDbMovieDetailResponse details;

                try
                {
                    details = await GetAsync<MovieApi.Models.Api.TMDbMovieDetailResponse>(
                        $"/movie/{tmdbId}");
                }
                catch
                {
                    // If TMDb fails for this one, skip it and continue
                    continue;
                }

                // 3. Parse release date
                DateTime? releaseDate = null;
                if (!string.IsNullOrWhiteSpace(details.ReleaseDate) &&
                    DateTime.TryParse(details.ReleaseDate, out var parsedDate))
                {
                    releaseDate = parsedDate;
                }

                // 4. Upsert into dbo.Movies using MovieLens MovieId as PK
                var movie = await _db.Movies
                    .SingleOrDefaultAsync(m => m.MovieId == movieLensId, ct);

                if (movie == null)
                {
                    movie = new Movie
                    {
                        MovieId = movieLensId,
                        CreatedAt = now
                    };
                    _db.Movies.Add(movie);
                }

                movie.TmdbId = tmdbId;
                movie.Title = details.Title ?? details.OriginalTitle ?? movie.Title ?? "(unknown title)";
                movie.Overview = details.Overview ?? movie.Overview;
                movie.PosterPath = details.PosterPath ?? movie.PosterPath;
                movie.BackdropPath = details.BackdropPath ?? movie.BackdropPath;
                movie.ReleaseDate = releaseDate ?? movie.ReleaseDate;
                movie.Popularity = details.Popularity;
                movie.VoteAverage = (decimal)details.VoteAverage;
                movie.VoteCount = details.VoteCount;
                movie.OriginalLanguage = string.IsNullOrWhiteSpace(details.OriginalLanguage)
                    ? (movie.OriginalLanguage ?? "unknown")
                    : details.OriginalLanguage;
                movie.Runtime = details.Runtime ?? movie.Runtime;
                movie.UpdatedAt = now;

                processed++;

                if (processed % batchSize == 0)
                {
                    await _db.SaveChangesAsync(ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            return processed;
        }

        // --------------------------------------------------------------------
        // 7. Sync Missing Movies between links and movies tables
        // --------------------------------------------------------------------
        public async Task<int> SyncMissingFromLinksAsync(int maxMovies = 1000, int batchSize = 200)
        {
            // 1) Find links that have a TMDb id but no row in dbo.Movies
            var missingLinksQuery =
                from l in _db.Links
                join m in _db.Movies on l.MovieId equals m.MovieId into gj
                from m in gj.DefaultIfEmpty()
                where l.TmdbId != null && m == null
                orderby l.MovieId
                select new { l.MovieId, l.TmdbId };

            var toImportAll = await missingLinksQuery
                .Take(maxMovies)
                .ToListAsync();

            if (toImportAll.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            int imported = 0;

            // 2) Process in batches to avoid hammering TMDb too hard
            for (int i = 0; i < toImportAll.Count; i += batchSize)
            {
                var batch = toImportAll
                    .Skip(i)
                    .Take(batchSize)
                    .ToList();

                foreach (var item in batch)
                {
                    var tmdbId = item.TmdbId!.Value;
                    var movieId = item.MovieId;   // MovieLens id

                    var details = await TryGetAsync<TMDbMovieDetailResponse>(
                        $"/movie/{tmdbId}?append_to_response=videos,credits");

                    if (details == null)
                    {
                        // 404 from TMDb – skip this one
                        continue;
                    }

                    // Map to summary, but override Id so that MovieId = MovieLens movieId
                    var summary = details.ToSummary();
                    summary.Id = movieId;

                    // Upsert basic movie row
                    var movie = await UpsertBasicMovieAsync(
                        summary,
                        listType: "links-missing",
                        page: 1,
                        indexOnPage: 0);

                    // Make sure we also store the TMDb id in Movies.TmdbId
                    movie.TmdbId = tmdbId;
                    movie.UpdatedAt = now;

                    imported++;
                }

                await _db.SaveChangesAsync();
            }

            return imported;
        }
    }

    // ------------------------------------------------------------------------
    // Local DTOs (only what this file actually uses)
    // ------------------------------------------------------------------------
    internal class TMDbGenreListResponse
    {
        public List<TMDbGenreDto> Genres { get; set; } = new();
    }

    internal class TMDbGenreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
