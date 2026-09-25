namespace WeddingBooking.Domain.Files;

/// <summary>Where files live — the generated agreements and the signed ones that come back.</summary>
public interface IFileStore
{
    Task<Guid> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct);

    Task<StoredFile?> GetAsync(Guid id, CancellationToken ct);
}

public sealed record StoredFile(Guid Id, string FileName, string ContentType, byte[] Content);
