using Microsoft.EntityFrameworkCore;

namespace openAIApps.Data;

// Data/AppDbContext.cs

public class AppDbContext : DbContext
{
    public DbSet<ChatSession> Sessions { get; set; }
    public DbSet<ChatMessage> Messages { get; set; }
    public DbSet<MediaFile> Media { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=localhistory.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ensure cascading deletes: if a session is deleted, messages and media refs follow
        modelBuilder.Entity<ChatSession>()
            .HasMany(s => s.Messages)
            .WithOne()
            .HasForeignKey(m => m.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasMany(m => m.MediaFiles)
            .WithOne()
            .HasForeignKey(mf => mf.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
