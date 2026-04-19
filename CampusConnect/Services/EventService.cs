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

        // Adds an optional userId so the result sets include IsLiked and IsViewed flags
        public async Task<List<EventDto>> GetEventsAsync(int? userId = null, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT p.PostID,
       p.SocietyID,
       s.Name AS SocietyName,
       p.Title,
       p.Text,
       p.PostTime,
       p.EventDate,
       p.Location,
       p.EventSlug,
       COALESCE(p.LikeCount,0)    AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0)    AS ViewCount,
       CASE WHEN @userId IS NOT NULL AND EXISTS(
            SELECT 1 FROM EventLikes el2 WHERE el2.PostID = p.PostID AND el2.UserID = @userId
       ) THEN 1 ELSE 0 END AS IsLiked,
       CASE WHEN @userId IS NOT NULL AND EXISTS(
            SELECT 1 FROM EventViews ev2 WHERE ev2.PostID = p.PostID AND ev2.UserID = @userId
       ) THEN 1 ELSE 0 END AS IsViewed
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";

            var list = new List<EventDto>();
            await using var conn = CreateConnection();
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqliteCommand(sql, conn);
            // Use DBNull.Value when userId is null so the CASE checks work correctly
            cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);

            await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                var dto = new EventDto
                {
                    Id = rdr.GetInt32(0),
                    SocietyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                    SocietyName = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    Title = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    Text = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                    PostTime = rdr.IsDBNull(5) ? DateTime.MinValue : DateTime.Parse(rdr.GetString(5)),
                    EventDate = rdr.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(rdr.GetString(6)),
                    Location = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    Slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                    LikeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9),
                    ReservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10),
                    ViewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11),
                    IsLiked = !rdr.IsDBNull(12) && rdr.GetInt32(12) == 1,
                    IsViewed = !rdr.IsDBNull(13) && rdr.GetInt32(13) == 1
                };

                dto.Tags = await GetTagsForEventAsync(conn, dto.Id, cancellationToken);
                list.Add(dto);
            }

            return list;
        }

        public async Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       COALESCE(p.LikeCount,0) AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0) AS ViewCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
WHERE p.PostID = @postId;
";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@postId", id);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await rdr.ReadAsync(cancellationToken))
                    return null;

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
                var viewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);

                var dto = new EventDto
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
                    ReservationCount = reservationCount,
                    ViewCount = viewCount
                };

                dto.Tags = await GetTagsForEventAsync(conn, evtId, cancellationToken);

                return dto;
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
       COALESCE(p.LikeCount,0) AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0) AS ViewCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
WHERE lower(p.EventSlug) = lower(@slug)
   OR lower(replace(p.Title,' ','-')) = lower(@slug)
LIMIT 1;
";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@slug", slug);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await rdr.ReadAsync(cancellationToken))
                    return null;

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
                var viewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);

                var dto = new EventDto
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
                    ReservationCount = reservationCount,
                    ViewCount = viewCount
                };

                dto.Tags = await GetTagsForEventAsync(conn, evtId, cancellationToken);

                return dto;
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
                    exists = await rdr.ReadAsync(cancellationToken);

                if (exists)
                {
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

                await using var insCmd = new SqliteCommand(insertSql, conn, tx);
                insCmd.Parameters.AddWithValue("@postId", eventId);
                insCmd.Parameters.AddWithValue("@userId", userId);
                insCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                await insCmd.ExecuteNonQueryAsync(cancellationToken);

                await using var incCmd2 = new SqliteCommand(incSql, conn, tx);
                incCmd2.Parameters.AddWithValue("@postId", eventId);
                await incCmd2.ExecuteNonQueryAsync(cancellationToken);

                tx.Commit();
                return true;
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
                    exists = await rdr.ReadAsync(cancellationToken);

                if (exists)
                {
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

                await using var insCmd = new SqliteCommand(insertSql, conn, tx);
                insCmd.Parameters.AddWithValue("@postId", eventId);
                insCmd.Parameters.AddWithValue("@userId", userId);
                insCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                await insCmd.ExecuteNonQueryAsync(cancellationToken);

                await using var incCmd2 = new SqliteCommand(incSql, conn, tx);
                incCmd2.Parameters.AddWithValue("@postId", eventId);
                await incCmd2.ExecuteNonQueryAsync(cancellationToken);

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RsvpEventAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
                throw;
            }
        }

        public async Task<List<EventDto>> GetUserRsvpdEventsAsync(int userId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       COALESCE(p.LikeCount,0) AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0) AS ViewCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
JOIN Bookings b ON b.PostID = p.PostID
WHERE b.UserID = @userId
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";
            var list = new List<EventDto>();
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
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
                    var viewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);

                    var dto = new EventDto
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
                        ReservationCount = reservationCount,
                        ViewCount = viewCount
                    };

                    dto.Tags = await GetTagsForEventAsync(conn, id, cancellationToken);
                    list.Add(dto);
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
            const string sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       COALESCE(p.LikeCount,0) AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0) AS ViewCount
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
JOIN EventLikes el ON el.PostID = p.PostID
WHERE el.UserID = @userId
ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;
";
            var list = new List<EventDto>();
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
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
                    var viewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);

                    var dto = new EventDto
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
                        ReservationCount = reservationCount,
                        ViewCount = viewCount
                    };

                    dto.Tags = await GetTagsForEventAsync(conn, id, cancellationToken);
                    list.Add(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserLikedEventsAsync failed for user {UserId}", userId);
                throw;
            }

            return list;
        }

        // Create new event and link provided tag IDs (stored tags only)
        public async Task<string> CreateEventAsync(EventCreateRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var baseSlug = Slugify(request.Title);
            var slug = baseSlug;

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
INSERT INTO Posts (SocietyID, Title, Text, PostTime, EventDate, Location, EventSlug, ViewCount)
VALUES (@societyId, @title, @text, @postTime, @eventDate, @location, @slug, 0);
SELECT last_insert_rowid();
";
            await using var insertCmd = new SqliteCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@societyId", request.SocietyId.HasValue ? (object)request.SocietyId.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@title", request.Title);
            insertCmd.Parameters.AddWithValue("@text", request.Text);
            insertCmd.Parameters.AddWithValue("@postTime", DateTime.UtcNow);
            insertCmd.Parameters.AddWithValue("@eventDate", request.EventDate.HasValue ? (object)request.EventDate.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@location", string.IsNullOrWhiteSpace(request.Location) ? (object)"" : request.Location);
            insertCmd.Parameters.AddWithValue("@slug", slug);

            long insertedId;
            await using (var rdr = await insertCmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await rdr.ReadAsync(cancellationToken))
                {
                    insertedId = rdr.GetInt64(0);
                }
                else
                {
                    await using var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn);
                    insertedId = (long)(await idCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                }
            }

            var eventId = (int)insertedId;

            // Link provided TagIds (only existing tags). Double-check server-side cap
            if (request.TagIds != null && request.TagIds.Count > 0)
            {
                const string selectTagSql = "SELECT 1 FROM Tags WHERE TagID = @tagId LIMIT 1;";
                const string insertEventTagSql = "INSERT OR IGNORE INTO EventTags (EventID, TagID) VALUES (@eventId, @tagId);";

                var linked = 0;
                foreach (var tagId in request.TagIds)
                {
                    if (linked >= 5) break;

                    await using (var checkCmd = new SqliteCommand(selectTagSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@tagId", tagId);
                        var exists = false;
                        await using (var rdr = await checkCmd.ExecuteReaderAsync(cancellationToken))
                            exists = await rdr.ReadAsync(cancellationToken);

                        if (!exists) continue;
                    }

                    await using var linkCmd = new SqliteCommand(insertEventTagSql, conn);
                    linkCmd.Parameters.AddWithValue("@eventId", eventId);
                    linkCmd.Parameters.AddWithValue("@tagId", tagId);
                    await linkCmd.ExecuteNonQueryAsync(cancellationToken);

                    linked++;
                }
            }

            return slug;
        }

        public async Task<List<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT TagID, Name FROM Tags ORDER BY Name COLLATE NOCASE;";
            var tags = new List<TagDto>();
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqliteCommand(sql, conn);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    tags.Add(new TagDto
                    {
                        Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllTagsAsync failed");
                throw;
            }

            return tags;
        }

        private async Task<List<TagDto>> GetTagsForEventAsync(SqliteConnection conn, int eventId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT t.TagID, t.Name
FROM Tags t
JOIN EventTags et ON et.TagID = t.TagID
WHERE et.EventID = @eventId
ORDER BY t.Name COLLATE NOCASE;
";
            var tags = new List<TagDto>();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@eventId", eventId);
            await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                tags.Add(new TagDto
                {
                    Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                    Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1)
                });
            }
            return tags;
        }

        // New: record a user's first view of an event (inserts into EventViews and increments Posts.ViewCount)
        public async Task RecordViewAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return;

            const string checkSql = "SELECT 1 FROM EventViews WHERE PostID = @postId AND UserID = @userId LIMIT 1;";
            const string insertSql = "INSERT INTO EventViews (PostID, UserID, CreatedAt) VALUES (@postId, @userId, @createdAt);";
            const string incSql = "UPDATE Posts SET ViewCount = COALESCE(ViewCount,0) + 1 WHERE PostID = @postId;";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var tx = conn.BeginTransaction();

                await using (var checkCmd = new SqliteCommand(checkSql, conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@postId", eventId);
                    checkCmd.Parameters.AddWithValue("@userId", userId);
                    var exists = false;
                    await using (var rdr = await checkCmd.ExecuteReaderAsync(cancellationToken))
                        exists = await rdr.ReadAsync(cancellationToken);

                    if (exists)
                    {
                        tx.Commit();
                        return;
                    }
                }

                await using (var insCmd = new SqliteCommand(insertSql, conn, tx))
                {
                    insCmd.Parameters.AddWithValue("@postId", eventId);
                    insCmd.Parameters.AddWithValue("@userId", userId);
                    insCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    await insCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var incCmd = new SqliteCommand(incSql, conn, tx))
                {
                    incCmd.Parameters.AddWithValue("@postId", eventId);
                    await incCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecordViewAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
                // swallow or rethrow depending on desired behaviour; rethrowing will surface to UI
                throw;
            }
        }

        public async Task<bool> IsEventLikedByUserAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return false;
            const string sql = "SELECT 1 FROM EventLikes WHERE PostID = @postId AND UserID = @userId LIMIT 1;";
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@postId", eventId);
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                return await rdr.ReadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IsEventLikedByUserAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
                throw;
            }
        }

        // only added method shown
        public async Task<bool> IsEventViewedByUserAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return false;
            const string sql = "SELECT 1 FROM EventViews WHERE PostID = @postId AND UserID = @userId LIMIT 1;";
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@postId", eventId);
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                return await rdr.ReadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IsEventViewedByUserAsync failed for eventId {EventId}, userId {UserId}", eventId, userId);
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

        public async Task<HashSet<int>> GetUserSocietyIdsAsync(int userId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT SocietyID
FROM SocietyMembers
WHERE UserID = @userId;
";
            var ids = new HashSet<int>();
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    if (!rdr.IsDBNull(0))
                        ids.Add(rdr.GetInt32(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserSocietyIdsAsync failed for user {UserId}", userId);
                throw;
            }
            return ids;
        }

        public async Task<List<EventDto>> GetFilteredEventsAsync(
    int? userId,
    bool upcomingOnly,
    bool mySocietiesOnly,
    CancellationToken cancellationToken = default)
        {
            var sql = @"
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime,
       p.EventDate, p.Location, p.EventSlug,
       COALESCE(p.LikeCount,0) AS LikeCount,
       COALESCE(p.ReservationCount,0) AS ReservationCount,
       COALESCE(p.ViewCount,0) AS ViewCount,
       CASE WHEN @userId IS NOT NULL AND EXISTS(
            SELECT 1 FROM EventLikes el2 WHERE el2.PostID = p.PostID AND el2.UserID = @userId
       ) THEN 1 ELSE 0 END AS IsLiked,
       CASE WHEN @userId IS NOT NULL AND EXISTS(
            SELECT 1 FROM EventViews ev2 WHERE ev2.PostID = p.PostID AND ev2.UserID = @userId
       ) THEN 1 ELSE 0 END AS IsViewed
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
WHERE 1=1
" +
            (upcomingOnly ? " AND p.EventDate >= @today" : "") +
            (mySocietiesOnly ? " AND p.SocietyID IN (SELECT SocietyID FROM SocietyMembers WHERE UserID = @userId)" : "") +
            " ORDER BY COALESCE(p.EventDate, p.PostTime) DESC;";

            var list = new List<EventDto>();
            await using var conn = CreateConnection();
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
            if (upcomingOnly)
                cmd.Parameters.AddWithValue("@today", DateTime.Today.ToString("yyyy-MM-dd"));

            await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                var dto = new EventDto
                {
                    Id = rdr.GetInt32(0),
                    SocietyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                    SocietyName = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    Title = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    Text = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                    PostTime = rdr.IsDBNull(5) ? DateTime.MinValue : DateTime.Parse(rdr.GetString(5)),
                    EventDate = rdr.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(rdr.GetString(6)),
                    Location = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    Slug = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                    LikeCount = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9),
                    ReservationCount = rdr.IsDBNull(10) ? 0 : rdr.GetInt32(10),
                    ViewCount = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11),
                    IsLiked = !rdr.IsDBNull(12) && rdr.GetInt32(12) == 1,
                    IsViewed = !rdr.IsDBNull(13) && rdr.GetInt32(13) == 1
                };
                dto.Tags = await GetTagsForEventAsync(conn, dto.Id, cancellationToken);
                list.Add(dto);
            }
            return list;
        }
    }
}