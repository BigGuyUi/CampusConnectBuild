using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CampusConnect.Services
{
    public class EventService : IEventService
    {
        private readonly string _connectionString;
        private readonly ILogger<EventService> _logger;

        public EventService(IConfiguration configuration, ILogger<EventService> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
        }

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public async Task<List<EventDto>> GetEventsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<EventDto>();
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       (SELECT COUNT(1) FROM EventLikes el WHERE el.PostID = p.PostID) AS LikeCount,
       (SELECT COUNT(1) FROM Bookings b WHERE b.PostID = p.PostID) AS ReservationCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    var id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var societyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1);
                    var societyName = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                    var title = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    var text = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                    DateTime postTime = DateTime.MinValue;
                    if (!rdr.IsDBNull(5))
                    {
                        var raw = rdr.GetValue(5)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out postTime);
                    }

                    DateTime? eventDate = null;
                    if (!rdr.IsDBNull(6))
                    {
                        var raw = rdr.GetValue(6)?.ToString();
                        if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                            eventDate = dt;
                    }

                    var location = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    var slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8);
                    var likeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                    var reservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                    list.Add(new EventDto
                    {
                        Id = id,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime,
                        EventDate = eventDate,
                        Location = location,
                        Slug = slug ?? "",
                        LikeCount = likeCount,
                        ReservationCount = reservationCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEventsAsync failed");
                throw;
            }

            return list;
        }

        public async Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       (SELECT COUNT(1) FROM EventLikes el WHERE el.PostID = p.PostID) AS LikeCount,
       (SELECT COUNT(1) FROM Bookings b WHERE b.PostID = p.PostID) AS ReservationCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
WHERE p.PostID = @postId;
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@postId", id);

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await rdr.ReadAsync(cancellationToken))
                {
                    var evtId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var societyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1);
                    var societyName = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                    var title = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    var text = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                    DateTime postTime = DateTime.MinValue;
                    if (!rdr.IsDBNull(5))
                    {
                        var raw = rdr.GetValue(5)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out postTime);
                    }

                    DateTime? eventDate = null;
                    if (!rdr.IsDBNull(6))
                    {
                        var raw = rdr.GetValue(6)?.ToString();
                        if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                            eventDate = dt;
                    }

                    var location = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    var slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8);
                    var likeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                    var reservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                    return new EventDto
                    {
                        Id = evtId,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime,
                        EventDate = eventDate,
                        Location = location,
                        Slug = slug ?? "",
                        LikeCount = likeCount,
                        ReservationCount = reservationCount
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEventAsync failed for id {Id}", id);
                throw;
            }
        }

        public async Task<EventDto?> GetEventBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       (SELECT COUNT(1) FROM EventLikes el WHERE el.PostID = p.PostID) AS LikeCount,
       (SELECT COUNT(1) FROM Bookings b WHERE b.PostID = p.PostID) AS ReservationCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
WHERE lower(p.EventSlug) = lower(@slug)
   OR lower(replace(p.Title,' ','-')) = lower(@slug)
LIMIT 1;
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@slug", slug);

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await rdr.ReadAsync(cancellationToken))
                {
                    var evtId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var societyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1);
                    var societyName = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                    var title = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    var text = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                    DateTime postTime = DateTime.MinValue;
                    if (!rdr.IsDBNull(5))
                    {
                        var raw = rdr.GetValue(5)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out postTime);
                    }

                    DateTime? eventDate = null;
                    if (!rdr.IsDBNull(6))
                    {
                        var raw = rdr.GetValue(6)?.ToString();
                        if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                            eventDate = dt;
                    }

                    var location = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    var slugOut = rdr.IsDBNull(8) ? "" : rdr.GetString(8);
                    var likeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                    var reservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                    return new EventDto
                    {
                        Id = evtId,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime,
                        EventDate = eventDate,
                        Location = location,
                        Slug = slugOut ?? "",
                        LikeCount = likeCount,
                        ReservationCount = reservationCount
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEventBySlugAsync failed for slug {Slug}", slug);
                throw;
            }
        }

        public async Task<bool> LikeEventAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            const string checkSql = "SELECT 1 FROM EventLikes WHERE PostID = @postId AND UserID = @userId LIMIT 1;";
            const string insertSql = "INSERT INTO EventLikes (PostID, UserID, CreatedAt) VALUES (@postId, @userId, @createdAt);";
            const string deleteSql = "DELETE FROM EventLikes WHERE PostID = @postId AND UserID = @userId;";
            const string incSql = "UPDATE Posts SET LikeCount = COALESCE(LikeCount,0) + 1 WHERE PostID = @postId;";
            const string decSql = "UPDATE Posts SET LikeCount = CASE WHEN LikeCount > 0 THEN LikeCount - 1 ELSE 0 END WHERE PostID = @postId;";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var tx = conn.BeginTransaction();
                await using var checkCmd = new SqliteCommand(checkSql, conn, tx);
                checkCmd.Parameters.AddWithValue("@postId", eventId);
                checkCmd.Parameters.AddWithValue("@userId", userId);

                var exists = false;
                await using (var rdr = await checkCmd.ExecuteReaderAsync(cancellationToken))
                {
                    exists = await rdr.ReadAsync(cancellationToken);
                }

                if (exists)
                {
                    // remove like
                    await using var delCmd = new SqliteCommand(deleteSql, conn, tx);
                    delCmd.Parameters.AddWithValue("@postId", eventId);
                    delCmd.Parameters.AddWithValue("@userId", userId);
                    await delCmd.ExecuteNonQueryAsync(cancellationToken);

                    await using var decCmd = new SqliteCommand(decSql, conn, tx);
                    decCmd.Parameters.AddWithValue("@postId", eventId);
                    await decCmd.ExecuteNonQueryAsync(cancellationToken);

                    tx.Commit();
                    return false;
                }
                else
                {
                    // add like
                    await using var insCmd = new SqliteCommand(insertSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@postId", eventId);
                    insCmd.Parameters.AddWithValue("@userId", userId);
                    insCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    await insCmd.ExecuteNonQueryAsync(cancellationToken);

                    await using var incCmd = new SqliteCommand(incSql, conn, tx);
                    incCmd.Parameters.AddWithValue("@postId", eventId);
                    await incCmd.ExecuteNonQueryAsync(cancellationToken);

                    tx.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LikeEventAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
                throw;
            }
        }

        public async Task<bool> RsvpEventAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            const string checkSql = "SELECT 1 FROM Bookings WHERE PostID = @postId AND UserID = @userId LIMIT 1;";
            const string insertSql = "INSERT INTO Bookings (PostID, UserID, CreatedAt) VALUES (@postId, @userId, @createdAt);";
            const string deleteSql = "DELETE FROM Bookings WHERE PostID = @postId AND UserID = @userId;";
            const string incSql = "UPDATE Posts SET ReservationCount = COALESCE(ReservationCount,0) + 1 WHERE PostID = @postId;";
            const string decSql = "UPDATE Posts SET ReservationCount = CASE WHEN ReservationCount > 0 THEN ReservationCount - 1 ELSE 0 END WHERE PostID = @postId;";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var tx = conn.BeginTransaction();
                await using var checkCmd = new SqliteCommand(checkSql, conn, tx);
                checkCmd.Parameters.AddWithValue("@postId", eventId);
                checkCmd.Parameters.AddWithValue("@userId", userId);

                var exists = false;
                await using (var rdr = await checkCmd.ExecuteReaderAsync(cancellationToken))
                {
                    exists = await rdr.ReadAsync(cancellationToken);
                }

                if (exists)
                {
                    // remove booking
                    await using var delCmd = new SqliteCommand(deleteSql, conn, tx);
                    delCmd.Parameters.AddWithValue("@postId", eventId);
                    delCmd.Parameters.AddWithValue("@userId", userId);
                    await delCmd.ExecuteNonQueryAsync(cancellationToken);

                    await using var decCmd = new SqliteCommand(decSql, conn, tx);
                    decCmd.Parameters.AddWithValue("@postId", eventId);
                    await decCmd.ExecuteNonQueryAsync(cancellationToken);

                    tx.Commit();
                    return false;
                }
                else
                {
                    // add booking
                    await using var insCmd = new SqliteCommand(insertSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@postId", eventId);
                    insCmd.Parameters.AddWithValue("@userId", userId);
                    insCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    await insCmd.ExecuteNonQueryAsync(cancellationToken);

                    await using var incCmd = new SqliteCommand(incSql, conn, tx);
                    incCmd.Parameters.AddWithValue("@postId", eventId);
                    await incCmd.ExecuteNonQueryAsync(cancellationToken);

                    tx.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RsvpEventAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
                throw;
            }
        }

        // User-specific list methods (use Bookings for RSVPs)
        public async Task<List<EventDto>> GetUserRsvpdEventsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var list = new List<EventDto>();
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       (SELECT COUNT(1) FROM EventLikes el WHERE el.PostID = p.PostID) AS LikeCount,
       (SELECT COUNT(1) FROM Bookings b WHERE b.PostID = p.PostID) AS ReservationCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
JOIN Bookings b ON b.PostID = p.PostID
WHERE b.UserID = @userId
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";
            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    var id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var societyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1);
                    var societyName = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                    var title = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    var text = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                    DateTime postTime = DateTime.MinValue;
                    if (!rdr.IsDBNull(5))
                    {
                        var raw = rdr.GetValue(5)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out postTime);
                    }

                    DateTime? eventDate = null;
                    if (!rdr.IsDBNull(6))
                    {
                        var raw = rdr.GetValue(6)?.ToString();
                        if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                            eventDate = dt;
                    }

                    var location = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    var slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8);
                    var likeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                    var reservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                    list.Add(new EventDto
                    {
                        Id = id,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime,
                        EventDate = eventDate,
                        Location = location,
                        Slug = slug ?? "",
                        LikeCount = likeCount,
                        ReservationCount = reservationCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserRsvpdEventsAsync failed for user {UserId}", userId);
                throw;
            }

            return list;
        }

        public async Task<List<EventDto>> GetUserLikedEventsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var list = new List<EventDto>();
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       (SELECT COUNT(1) FROM EventLikes el WHERE el.PostID = p.PostID) AS LikeCount,
       (SELECT COUNT(1) FROM Bookings b WHERE b.PostID = p.PostID) AS ReservationCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
JOIN EventLikes el ON el.PostID = p.PostID
WHERE el.UserID = @userId
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";
            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    var id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var societyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1);
                    var societyName = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                    var title = rdr.IsDBNull(3) ? "" : rdr.GetString(3);
                    var text = rdr.IsDBNull(4) ? "" : rdr.GetString(4);

                    DateTime postTime = DateTime.MinValue;
                    if (!rdr.IsDBNull(5))
                    {
                        var raw = rdr.GetValue(5)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out postTime);
                    }

                    DateTime? eventDate = null;
                    if (!rdr.IsDBNull(6))
                    {
                        var raw = rdr.GetValue(6)?.ToString();
                        if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                            eventDate = dt;
                    }

                    var location = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    var slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8);
                    var likeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9);
                    var reservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10);

                    list.Add(new EventDto
                    {
                        Id = id,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime,
                        EventDate = eventDate,
                        Location = location,
                        Slug = slug ?? "",
                        LikeCount = likeCount,
                        ReservationCount = reservationCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserLikedEventsAsync failed for user {UserId}", userId);
                throw;
            }

            return list;
        }

        // New: create event implementation
        public async Task<string> CreateEventAsync(EventCreateRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            // generate slug and ensure uniqueness
            var baseSlug = Slugify(request.Title);
            var slug = baseSlug;
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                // ensure slug unique
                var suffix = 1;
                while (true)
                {
                    const string checkSql = "SELECT 1 FROM Posts WHERE lower(EventSlug) = lower(@slug) LIMIT 1;";
                    await using var checkCmd = new SqliteCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@slug", slug);
                    var exists = false;
                    await using (var rdr = await checkCmd.ExecuteReaderAsync(cancellationToken))
                    {
                        exists = await rdr.ReadAsync(cancellationToken);
                    }

                    if (!exists) break;
                    slug = $"{baseSlug}-{suffix++}";
                }

                const string insertSql = @"
INSERT INTO Posts (SocietyID, Title, Text, PostTime, EventDate, Location, EventSlug)
VALUES (@societyId, @title, @text, @postTime, @eventDate, @location, @slug);
SELECT last_insert_rowid();
";
                await using var insertCmd = new SqliteCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@societyId", request.SocietyId.HasValue ? (object)request.SocietyId.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@title", request.Title ?? "");
                insertCmd.Parameters.AddWithValue("@text", request.Text ?? "");
                insertCmd.Parameters.AddWithValue("@postTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                insertCmd.Parameters.AddWithValue("@eventDate", request.EventDate.HasValue ? (object)request.EventDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@location", request.Location ?? "");
                insertCmd.Parameters.AddWithValue("@slug", slug ?? "");
                var scalar = await insertCmd.ExecuteScalarAsync(cancellationToken);
                // scalar is last_insert_rowid()
                return slug;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateEventAsync failed for Title {Title}", request.Title);
                throw;
            }
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("n").Substring(0, 8);
            value = value.ToLowerInvariant().Trim();
            // remove invalid chars
            value = Regex.Replace(value, @"[^\w\s-]", "");
            // normalize spaces/hyphens
            value = Regex.Replace(value, @"\s+", "-");
            value = Regex.Replace(value, @"-+", "-");
            return value;
        }
    }
}