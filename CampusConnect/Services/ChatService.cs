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
    public class ChatService : IChatService
    {
        private readonly string _connectionString;
        private readonly ILogger<ChatService> _logger;

        public ChatService(IConfiguration configuration, ILogger<ChatService> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'. Add it to appsettings.json.");
        }

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public async Task<List<ChatSummary>> GetUserChatsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var list = new List<ChatSummary>();
            const string sql = @"
SELECT c.ChatID, c.SocietyID, c.ChatName,
       (SELECT m.Text FROM Messages m WHERE m.ChatID = c.ChatID ORDER BY m.PostTime DESC LIMIT 1) AS LastMessage
FROM Chats c
WHERE EXISTS (SELECT 1 FROM ChatMembers cm WHERE cm.ChatID = c.ChatID AND cm.UserID = @userId)
   OR EXISTS (SELECT 1 FROM SocietyMembers sm WHERE sm.SocietyID = c.SocietyID AND sm.UserID = @userId)
ORDER BY c.ChatID;
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
                    list.Add(new ChatSummary
                    {
                        Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        SocietyId = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                        Name = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        LastMessage = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                        UnreadCount = 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserChatsAsync failed for userId {UserId}", userId);
                throw;
            }

            return list;
        }

        public async Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int currentUserId, CancellationToken cancellationToken = default)
        {
            var messages = new List<MessageDto>();
            const string sql = @"
SELECT m.MessageID, u.Name, m.Text, m.PostTime, m.UserID
FROM Messages m
JOIN Users u ON m.UserID = u.UserID
WHERE m.ChatID = @chatId
ORDER BY m.PostTime;
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@chatId", chatId);

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    int messageId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    string sender = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                    string text = rdr.IsDBNull(2) ? "" : rdr.GetString(2);

                    DateTime sentAt = DateTime.MinValue;
                    if (!rdr.IsDBNull(3))
                    {
                        var raw = rdr.GetValue(3)?.ToString();
                        if (!string.IsNullOrEmpty(raw))
                            DateTime.TryParse(raw, out sentAt);
                    }

                    int senderId = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);

                    messages.Add(new MessageDto
                    {
                        Id = messageId,
                        Sender = sender,
                        Text = text,
                        SentAt = sentAt,
                        IsMine = (senderId == currentUserId)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetChatMessagesAsync failed for chatId {ChatId}", chatId);
                throw;
            }

            return messages;
        }

        public async Task<bool> SendMessageAsync(int chatId, int userId, string text, CancellationToken cancellationToken = default)
        {
            const string sql = @"
INSERT INTO Messages (ChatID, UserID, Text, Image, PostTime)
VALUES (@chatId, @userId, @text, NULL, @postTime);
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@chatId", chatId);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@text", text ?? string.Empty);
                cmd.Parameters.AddWithValue("@postTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                await conn.OpenAsync(cancellationToken);
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendMessageAsync failed for chatId {ChatId}, userId {UserId}", chatId, userId);
                throw;
            }
        }
    }
}