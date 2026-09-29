using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Files;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Infrastructure.Files;

internal sealed class FileRecord
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Size { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Files in Postgres, in the app schema, behind the API's access control.
/// Agreements are a few hundred kilobytes and a few hundred a year; a table is
/// enough, and it needs no storage key in the API. A Supabase Storage adapter
/// can replace it behind the same port.
/// </summary>
internal sealed class DatabaseFileStore(AppDbContext db) : IFileStore
{
    public async Task<Guid> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct)
    {
        var record = new FileRecord
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            ContentType = contentType,
            Size = content.Length,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Set<FileRecord>().Add(record);
        await db.SaveChangesAsync(ct);
        return record.Id;
    }

    public async Task<StoredFile?> GetAsync(Guid id, CancellationToken ct)
    {
        var record = await db.Set<FileRecord>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        return record is null ? null : new StoredFile(record.Id, record.FileName, record.ContentType, record.Content);
    }
}
