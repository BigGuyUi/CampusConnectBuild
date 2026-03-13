using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusConnect.Models;

namespace CampusConnect.Services
{
    public interface IChatService
    {
        Task<List<ChatSummary>> GetUserChatsAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int currentUserId, CancellationToken cancellationToken = default);
        Task<bool> SendMessageAsync(int chatId, int userId, string text, CancellationToken cancellationToken = default);
    }
}