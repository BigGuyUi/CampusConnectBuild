using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime
FROM Posts p
LEFT JOIN Societies s ON p.SocietyID = s.SocietyID
ORDER BY p.PostTime DESC;
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

                    list.Add(new EventDto
                    {
                        Id = id,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime
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
SELECT p.PostID, p.SocietyID, s.Name AS SocietyName, p.Title, p.Text, p.PostTime
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

                    return new EventDto
                    {
                        Id = evtId,
                        SocietyId = societyId,
                        SocietyName = societyName,
                        Title = title,
                        Text = text,
                        PostTime = postTime
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
    }
}