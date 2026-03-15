using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;
using Microsoft.Data.SqlClient;
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
            // read connection string from configuration instead of hard-coding it
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'. Add it to appsettings.json.");
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<ChatSummary>> GetUserChatsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var list = new List<ChatSummary>();
            const string sql = @"
SELECT c.ChatID, c.SocietyID, c.ChatName,
       (SELECT TOP (1) m.Text FROM Messages m WHERE m.ChatID = c.ChatID ORDER BY m.PostTime DESC) AS LastMessage
FROM Chats c
WHERE EXISTS (SELECT 1 FROM ChatMembers cm WHERE cm.ChatID = c.ChatID AND cm.UserID = @userId)
   OR EXISTS (SELECT 1 FROM SocietyMembers sm WHERE sm.SocietyID = c.SocietyID AND sm.UserID = @userId)
ORDER BY c.ChatID;
";

            try
            {
                await using var conn = CreateConnection();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    list.Add(new ChatSummary
                    {
                        Id = rdr.GetInt32(0),
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
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@chatId", SqlDbType.Int) { Value = chatId });

                await conn.OpenAsync(cancellationToken);
                await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    int messageId = rdr.GetInt32(0);
                    string sender = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                    string text = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                    DateTime sentAt = rdr.IsDBNull(3) ? DateTime.MinValue : rdr.GetDateTime(3);
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
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@chatId", SqlDbType.Int) { Value = chatId });
                cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });
                // explicitly allow large text (NVARCHAR(MAX))
                cmd.Parameters.Add(new SqlParameter("@text", SqlDbType.NVarChar, -1) { Value = text ?? string.Empty });
                cmd.Parameters.Add(new SqlParameter("@postTime", SqlDbType.DateTime2) { Value = DateTime.UtcNow });

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