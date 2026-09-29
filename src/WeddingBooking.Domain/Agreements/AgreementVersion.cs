using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Agreements;

/// <summary>
/// One generated agreement. Every version is kept: a new one after the guest
/// count changed does not replace the one the couple may already have signed.
/// </summary>
public sealed class AgreementVersion
{
    private AgreementVersion() { }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public int Number { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
    public string GeneratedBy { get; private set; } = string.Empty;

    /// <summary>The clarification as it read when this version was made.</summary>
    public string? Clarification { get; private set; }

    public Guid PdfFileId { get; private set; }

    public DateTimeOffset? SignedAt { get; private set; }
    public Guid? SignedFileId { get; private set; }
    public string? SignedUploadedBy { get; private set; }

    public static AgreementVersion Create(Guid bookingId, int number, string generatedBy, string? clarification, Guid pdfFileId, DateTimeOffset at) => new()
    {
        Id = Guid.CreateVersion7(),
        BookingId = bookingId,
        Number = number,
        GeneratedAt = at,
        GeneratedBy = generatedBy,
        Clarification = clarification,
        PdfFileId = pdfFileId,
    };

    /// <summary>The signed copy that came back by mail.</summary>
    public void RecordSigned(Guid fileId, string uploadedBy, DateTimeOffset at)
    {
        if (SignedFileId is not null) throw new DomainException("Der er allerede uploadet en underskrevet udgave af denne version.");
        SignedFileId = fileId;
        SignedUploadedBy = uploadedBy;
        SignedAt = at;
    }
}
