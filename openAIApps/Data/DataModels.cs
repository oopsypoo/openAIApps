#pragma warning disable CS8618 // Non-nullable field is uninitialized
#pragma warning disable CS8603 // Possible null reference return
#pragma warning disable CS8632 // annotation for nullable ref types should only be used in code within a '#nullable' annotations context
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace openAIApps.Data;

public enum EndpointType { Responses, Video }

// Primary Constructor: Compact and modern
public class ChatSession(EndpointType endpoint, string type, string title)
{
    [Key]
    public int Id { get; set; }
    public EndpointType Endpoint { get; set; } = endpoint;
    public string Type { get; set; } = type; // e.g., "Responses" or "Video"
    public string Title { get; set; } = title;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    // Navigation: One session has many messages
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

// Use 'chatSessionId' (camelCase) to match 'ChatSessionId' (PascalCase)
public class ChatMessage
{
    public ChatMessage() { }

    [Key]
    public int Id { get; set; }
    public int ChatSessionId { get; set; }
    public string Role { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // --- Responses DNA ---
    public string ModelUsed { get; set; }
    public string ReasoningLevel { get; set; }
    public string ActiveTools { get; set; }
    public string SearchContextSize { get; set; }

    // --- Image DNA ---
    public string ImageSize { get; set; }
    public string ImageQuality { get; set; }

    // --- Video DNA ---
    public string VideoLength { get; set; }     // From cmbVideoLength
    public string VideoSize { get; set; }       // From cmbVideoSize
    public bool IsRemix { get; set; }           // From cbVideoRemix
    public string RemoteId { get; set; }        // OpenAI's video_id for future remixes

    public string RawJson { get; set; }

    // Navigation properties
    public virtual ChatSession ChatSession { get; set; } = null!;
    public virtual ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();
}

public class MediaFile(int chatMessageId, string localPath, string mediaType)
{
    [Key]
    public int Id { get; set; }

    public int ChatMessageId { get; set; } = chatMessageId;

    public string LocalPath { get; set; } = localPath; // Path to the .mp4 or .png
    public string MediaType { get; set; } = mediaType;

    public ChatMessage ChatMessage { get; set; } = null!;
}
