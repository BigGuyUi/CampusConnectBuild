using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CampusConnect.Services
{
    public class SocietyService : ISocietyService
    {
        private readonly string _connectionString;
        private readonly ILogger<SocietyService> _logger;

        public SocietyService(IConfiguration configuration, ILogger<SocietyService> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection' in configuration.");
        }

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public async Task<List<SocietyDto>> GetAllSocietiesAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<SocietyDto>();
            const string sql = @"
SELECT s.SocietyID, s.Name,
       (SELECT COUNT(1) FROM SocietyMembers sm WHERE sm.SocietyID = s.SocietyID) AS MemberCount,
       (SELECT COUNT(1) FROM Posts p WHERE p.SocietyID = s.SocietyID) AS PostCount
FROM Societies s
ORDER BY s.Name;
";
            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    list.Add(new SocietyDto
                    {
                        Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        MemberCount = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                        PostCount = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllSocietiesAsync failed");
                throw;
            }

            return list;
        }

        public async Task<List<SocietyDto>> GetUserSocietiesAsync(int userId, CancellationToken cancellationToken = default)
        {
            var list = new List<SocietyDto>();
            const string sql = @"
SELECT s.SocietyID, s.Name,
       (SELECT COUNT(1) FROM SocietyMembers sm WHERE sm.SocietyID = s.SocietyID) AS MemberCount,
       (SELECT COUNT(1) FROM Posts p WHERE p.SocietyID = s.SocietyID) AS PostCount
FROM Societies s
JOIN SocietyMembers sm ON s.SocietyID = sm.SocietyID
WHERE sm.UserID = @userId
ORDER BY s.Name;
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
                    list.Add(new SocietyDto
                    {
                        Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        MemberCount = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                        PostCount = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserSocietiesAsync failed for userId {UserId}", userId);
                throw;
            }

            return list;
        }

        public async Task<bool> JoinSocietyAsync(int userId, int societyId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
INSERT INTO SocietyMembers (SocietyID, UserID)
SELECT @societyId, @userId
WHERE NOT EXISTS (SELECT 1 FROM SocietyMembers WHERE SocietyID = @societyId AND UserID = @userId);
";
            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@societyId", societyId);
                cmd.Parameters.AddWithValue("@userId", userId);

                await conn.OpenAsync(cancellationToken);
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JoinSocietyAsync failed for userId {UserId}, societyId {SocietyId}", userId, societyId);
                throw;
            }
        }

        public async Task<SocietyDetailDto?> GetSocietyAsync(int societyId, CancellationToken cancellationToken = default)
        {
            const string sqlSociety = @"SELECT SocietyID, Name FROM Societies WHERE SocietyID = @socId;";
            const string sqlMembers = @"
SELECT u.UserID, u.Name
FROM Users u
JOIN SocietyMembers sm ON sm.UserID = u.UserID
WHERE sm.SocietyID = @socId
ORDER BY u.Name;
";
            const string sqlPosts = @"
SELECT p.PostID, p.Title, p.Text, p.PostTime
FROM Posts p
WHERE p.SocietyID = @socId
ORDER BY p.PostTime DESC;
";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync(cancellationToken);

                await using (var cmd = new SqliteCommand(sqlSociety, conn))
                {
                    cmd.Parameters.AddWithValue("@socId", societyId);
                    await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (!await rdr.ReadAsync(cancellationToken))
                        return null;
                }

                var detail = new SocietyDetailDto { Id = societyId };

                // load name (reuse query)
                await using (var cmdName = new SqliteCommand(sqlSociety, conn))
                {
                    cmdName.Parameters.AddWithValue("@socId", societyId);
                    await using var rdrName = await cmdName.ExecuteReaderAsync(cancellationToken);
                    if (await rdrName.ReadAsync(cancellationToken))
                    {
                        detail.Name = rdrName.IsDBNull(1) ? "" : rdrName.GetString(1);
                    }
                }

                // members
                await using (var cmdMembers = new SqliteCommand(sqlMembers, conn))
                {
                    cmdMembers.Parameters.AddWithValue("@socId", societyId);
                    await using var rdr = await cmdMembers.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        detail.Members.Add(new MemberDto
                        {
                            Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                            Name = rdr.IsDBNull(1) ? "" : rdr.GetString(1)
                        });
                    }
                }

                // posts
                await using (var cmdPosts = new SqliteCommand(sqlPosts, conn))
                {
                    cmdPosts.Parameters.AddWithValue("@socId", societyId);
                    await using var rdr = await cmdPosts.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        var post = new PostDto
                        {
                            Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                            Title = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                            Text = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                            PostTime = DateTime.MinValue
                        };

                        if (!rdr.IsDBNull(3))
                        {
                            var raw = rdr.GetValue(3)?.ToString();
                            if (!string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, out var dt))
                                post.PostTime = dt;
                        }

                        detail.Posts.Add(post);
                    }
                }

                return detail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSocietyAsync failed for societyId {SocietyId}", societyId);
                throw;
            }
        }

        public async Task<bool> LeaveSocietyAsync(int userId, int societyId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
DELETE FROM SocietyMembers
WHERE SocietyID = @societyId AND UserID = @userId;
";
            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@societyId", societyId);
                cmd.Parameters.AddWithValue("@userId", userId);

                await conn.OpenAsync(cancellationToken);
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeaveSocietyAsync failed for userId {UserId}, societyId {SocietyId}", userId, societyId);
                throw;
            }
        }
    }
}