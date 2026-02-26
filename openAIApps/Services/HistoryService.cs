using Microsoft.EntityFrameworkCore;
using openAIApps.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace openAIApps.Services;
// Services/HistoryService.cs

public class HistoryService(AppDbContext context)
{
    // Fix: We now provide all 3 required arguments to the constructor
    public async Task<int> StartNewSessionAsync(string initialTitle, EndpointType endpoint)
    {
        // 1. Map the Enum to a friendly string for the 'Type' column
        string typeString = endpoint.ToString(); // e.g., "Responses" or "Video"

        // 2. Create the session matching the constructor: (endpoint, type, title)
        var session = new ChatSession(endpoint, typeString, initialTitle);

        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        return session.Id;
    }


    // Add message and return the ID to link media immediately
    public async Task<int> AddMessageAsync(
    int sessionId,
    string role,
    string content,
    string rawJson = null,
    string model = null,
    string reasoning = null,
    string tools = null,
    string imgSize = null,
    string imgQual = null,
    string searchSize = null,
    string videoLength = null,   // New: Video Tab
    string videoSize = null,     // New: Video Tab
    bool isRemix = false,        // New: Video Tab
    string remoteId = null)      // New: Video Tab (OpenAI video_id)
    {
        var msg = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = role,
            Content = content,
            RawJson = rawJson,
            ModelUsed = model,
            ReasoningLevel = reasoning,
            ActiveTools = tools,
            ImageSize = imgSize,
            ImageQuality = imgQual,
            SearchContextSize = searchSize,
            VideoLength = videoLength,
            VideoSize = videoSize,
            IsRemix = isRemix,
            RemoteId = remoteId,
            Timestamp = DateTime.UtcNow
        };

        context.Messages.Add(msg);
        await context.SaveChangesAsync();

        // Refresh the session timestamp
        var session = await context.Sessions.FindAsync(sessionId);
        if (session != null) session.LastUsedAt = DateTime.Now;
        await context.SaveChangesAsync();

        return msg.Id;
    }

    // Load full history for a specific tab (Rehydration)
    public async Task<List<ChatMessage>> GetFullSessionHistoryAsync(int sessionId)
    {
        return await context.Messages
            .Include(m => m.MediaFiles)
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    // For the unified "Logs" tab search
    public async Task<List<ChatSession>> GetRecentSessionsAsync()
    {
        return await context.Sessions
            .OrderByDescending(s => s.LastUsedAt)
            .Take(50)
            .AsNoTracking()
            .ToListAsync();
    }
    // Overload to support filtering by endpoint type(text or image/video)
    // In HistoryService.cs
    public async Task<List<ChatSession>> GetRecentSessionsAsync(string filter)
    {
        var query = context.Sessions.Include(s => s.Messages).AsQueryable();

        if (filter == "Images")
            query = query.Where(s => s.Messages.Any(m => m.ActiveTools.Contains("image_generation")));
        else if (filter == "Web")
            query = query.Where(s => s.Messages.Any(m => m.ActiveTools.Contains("web_tool")));

        return await query.OrderByDescending(s => s.LastUsedAt).ToListAsync();
    }
 
    public async Task<List<object>> GetContextForApiAsync(int sessionId)
    {
        var messages = await context.Messages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // OpenAI expects exactly: { "role": "user/assistant", "content": "text" }
        return messages.Select(m => new {
            role = m.Role.ToLower(),
            content = m.Content
        }).Cast<object>().ToList();
    }
    // Inside HistoryService.cs
    public async Task DeleteSessionAsync(int sessionId)
    {
        var session = await context.Sessions
            .Include(s => s.Messages)
            .ThenInclude(m => m.MediaFiles)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session != null)
        {
            context.Sessions.Remove(session);
            await context.SaveChangesAsync();
        }
    }


    public async Task LinkMediaAsync(int chatMessageId, string localPath, string type)
    {
        // Use the constructor parameters defined in DataModels.cs 
        var media = new MediaFile(chatMessageId, localPath, type);

        // Use the correct DbSet name 'Media' from AppDbContext 
        context.Media.Add(media);
        await context.SaveChangesAsync();
    }
    public async Task<ChatMessage> GetMessageByRemoteVideoIdAsync(string videoId)
    {
        // Find the message that holds this specific OpenAI Video ID
        return await context.Messages
            .FirstOrDefaultAsync(m => m.RemoteId == videoId);
    }
}
