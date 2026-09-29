using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Domain.Payments;
using WeddingBooking.Domain.Pricing;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Domain.Bookings;

/// <summary>
/// One wedding. The guest count lives here and nowhere else: every price, every
/// instalment and the kitchen's cover count is derived from it when read.
/// </summary>
public sealed class Booking
{
    /// <summary>The barn seats 350.</summary>
    public const int MaxGuests = 350;

    private readonly List<BookingLine> _lines = [];
    private readonly List<AccountingReference> _references = [];
    private readonly List<ReadinessItem> _readiness = [];

    private Booking() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    public DateOnly EventDate { get; private set; }
    public int GuestCount { get; private set; }
    public PaymentTerms PaymentTerms { get; private set; }
    public SalesStage Stage { get; private set; }

    public BookingOrigin Origin { get; private set; }

    /// <summary>A viewing until the couple says yes; then a wedding — the same row, customer and history.</summary>
    public BookingKind Kind { get; private set; } = BookingKind.Wedding;

    public IReadOnlyList<BookingLine> Lines => _lines;

    public IReadOnlyList<ReadinessItem> ReadinessItems => _readiness;

    /// <summary>The small-wedding surcharge as it stood when the booking was made.</summary>
    public SurchargeRule Surcharge { get; private set; } = SurchargeRule.None;

    /// <summary>The quote and invoice numbers the accounting system gave back. Numbers only.</summary>
    public IReadOnlyList<AccountingReference> AccountingReferences => _references;

    /// <summary>The Trello card this booking was imported from. Importing it again finds this and skips.</summary>
    public string? TrelloCardId { get; private set; }

    /// <summary>When the couple is shown round, and by whom (normally Bryan).</summary>
    public DateTimeOffset? ViewingAt { get; private set; }

    public Guid? ViewingHostId { get; private set; }

    /// <summary>
    /// Johan's own words for the agreement: what is included in practice, what is
    /// left out for now. Kept on the booking; each generated version copies it.
    /// </summary>
    public string? AgreementClarification { get; private set; }

    /// <summary>When the signed agreement came back. The 33 % falls due then for a wedding booked late.</summary>
    public DateTimeOffset? SignedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Postgres' xmin. Two people changing the guest count at once get a conflict, not a silent overwrite.</summary>
    public uint Version { get; private set; }

    /// <summary>A date asked for from the website is held this long while it waits (§11.12).</summary>
    public static readonly TimeSpan RequestHold = TimeSpan.FromDays(14);

    /// <summary>
    /// A request from the website: a viewing at a free time, or the wedding itself on a free date.
    /// It waits for the event manager as *Ny bestilling* and holds its time or date meanwhile.
    /// </summary>
    public static Booking Request(Customer customer, DateOnly eventDate, int guestCount, SurchargeRule? surcharge, DateTimeOffset? viewingAt, Guid? hostId)
    {
        var booking = Create(customer, eventDate, guestCount, PaymentTerms.New, surcharge);
        booking.Stage = SalesStage.Requested;
        booking.Origin = BookingOrigin.Website;
        booking.ViewingAt = viewingAt?.ToUniversalTime();
        booking.ViewingHostId = hostId;
        booking.Kind = viewingAt is null ? BookingKind.Wedding : BookingKind.Viewing;
        return booking;
    }

    /// <summary>Whether a request's date is still held for it.</summary>
    public bool HoldsRequestedDate(DateTimeOffset now) =>
        Stage == SalesStage.Requested && ViewingAt is null && now - CreatedAt < RequestHold;

    /// <summary>Takes the request on: a viewing is booked, a date request becomes an inquiry like any other.</summary>
    public void AcceptRequest()
    {
        if (Stage != SalesStage.Requested) throw new DomainException("Bookingen er ikke en ny bestilling.");
        Stage = ViewingAt is null ? SalesStage.Inquiry : SalesStage.Viewing;
        Touch();
    }

    /// <summary>
    /// The couple said yes: the viewing — or a date request from the website — becomes a wedding on the
    /// same row. The date, guests and terms are set now, and one of the two is chosen to pay.
    /// </summary>
    public void ConfirmAsWedding(DateOnly eventDate, int guestCount, PaymentTerms terms, Guid payerId)
    {
        if (Stage.IsClosed()) throw new DomainException("En afbestilt eller tabt sag kan ikke bekræftes.");
        if (Kind != BookingKind.Viewing && Stage != SalesStage.Requested) throw new DomainException("Sagen er allerede en booking.");

        Change(eventDate, guestCount, terms);
        Customer.MakePayer(payerId);
        Kind = BookingKind.Wedding;
        Stage = SalesStage.Inquiry;
    }

    /// <summary>A request or a viewing the couple — or the estate — said no to. Its time or date is freed.</summary>
    public void Decline()
    {
        if (Stage != SalesStage.Requested && (Kind != BookingKind.Viewing || Stage.IsClosed()))
        {
            throw new DomainException("Kun en ny bestilling eller en fremvisning kan afvises her.");
        }
        Stage = SalesStage.Lost;
        Touch();
    }

    public static Booking Create(
        Customer customer, DateOnly eventDate, int guestCount, PaymentTerms terms = PaymentTerms.New, SurchargeRule? surcharge = null)
    {
        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.Id,
            Customer = customer,
            Stage = SalesStage.Inquiry,
            PaymentTerms = terms,
            Surcharge = (surcharge ?? SurchargeRule.None).Validated(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        booking.Change(eventDate, guestCount, terms);
        booking.EnsureReadiness();
        return booking;
    }

    public void Change(DateOnly eventDate, int guestCount, PaymentTerms terms)
    {
        if (eventDate == default)
        {
            throw new DomainException("Bookingen skal have en dato.");
        }

        if (guestCount is < 1 or > MaxGuests)
        {
            throw new DomainException($"Gæsteantallet skal være mellem 1 og {MaxGuests}.");
        }

        EventDate = eventDate;
        GuestCount = guestCount;
        PaymentTerms = terms;
        Touch();
    }

    public void MoveTo(SalesStage stage)
    {
        if (!stage.IsManual())
        {
            throw new DomainException("En booking bliver underskrevet ved at uploade den underskrevne aftale.");
        }

        Stage = stage;
        Touch();
    }

    /// <summary>The signed agreement has come back.</summary>
    public void MarkSigned(DateTimeOffset at)
    {
        if (Stage.IsClosed())
        {
            throw new DomainException("En afbestilt eller tabt booking kan ikke underskrives.");
        }

        Stage = SalesStage.Signed;
        SignedAt ??= at;
        Touch();
    }

    public DateOnly? SignedOn => SignedAt is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : null;

    /// <summary>What the booking costs right now. Worked out, never stored.</summary>
    public PriceBreakdown Price() =>
        PriceCalculator.Calculate(GuestCount, OrderedLines.Select(line => (line.Id, line.Spec)), Surcharge);

    public IEnumerable<BookingLine> OrderedLines => _lines.OrderBy(line => line.SortOrder);

    /// <summary>Copies the package's lines and prices onto the booking. Later changes to the package do not reach them.</summary>
    public IReadOnlyList<BookingLine> AddPackage(Package package)
    {
        if (!package.IsActive) throw new DomainException("Pakken er udgået og kan ikke lægges på en booking.");
        var added = package.Lines.OrderBy(line => line.SortOrder)
            .Select(line => BookingLine.Create(Id, line.Spec, NextSortOrder(), package))
            .Select(line => { _lines.Add(line); return line; })
            .ToList();
        Touch();
        return added;
    }

    public BookingLine AddLine(LineSpec spec)
    {
        var line = BookingLine.Create(Id, spec, NextSortOrder());
        _lines.Add(line);
        Touch();
        return line;
    }

    public void UpdateLine(Guid lineId, LineSpec spec)
    {
        FindLine(lineId).Apply(spec);
        Touch();
    }

    public void RemoveLine(Guid lineId)
    {
        _lines.Remove(FindLine(lineId));
        Touch();
    }

    public void RecordTrelloOrigin(string cardId)
    {
        TrelloCardId = cardId;
        Origin = BookingOrigin.Trello;
    }

    /// <summary>Sets a readiness item's status as it stood in Trello, bypassing role checks: it is the import, not a person.</summary>
    public void ImportReadiness(ReadinessCategory category, ReadinessStatus status)
    {
        var item = _readiness.FirstOrDefault(i => i.Category == category);
        item?.Update(status, item.Notes, item.Responsible, "Trello-import");
    }

    /// <summary>Books a viewing. An inquiry moves on to "fremvisning aftalt".</summary>
    public void ScheduleViewing(DateTimeOffset? at, Guid? hostId)
    {
        ViewingAt = at?.ToUniversalTime();
        ViewingHostId = hostId;
        if (at is not null && Stage == SalesStage.Inquiry) Stage = SalesStage.Viewing;
        Touch();
    }

    public void SetAgreementClarification(string? text)
    {
        var clarification = Text.Blank(text);
        if (clarification?.Length > 10_000) throw new DomainException("Præciseringen er for lang.");
        AgreementClarification = clarification;
        Touch();
    }

    /// <summary>Adds the standard readiness items that are missing. Safe to call again.</summary>
    public bool EnsureReadiness()
    {
        var missing = ReadinessRules.Defaults.Where(d => _readiness.All(item => item.Category != d.Category)).ToList();
        foreach (var (category, title) in missing)
        {
            _readiness.Add(ReadinessItem.Create(Id, category, title));
        }
        return missing.Count > 0;
    }

    public ReadinessItem AddReadinessItem(ReadinessCategory category, string title)
    {
        var item = ReadinessItem.Create(Id, category, title);
        _readiness.Add(item);
        Touch();
        return item;
    }

    public void RemoveReadinessItem(Guid itemId)
    {
        var item = FindReadiness(itemId);
        if (_readiness.Count(i => i.Category == item.Category) == 1)
        {
            throw new DomainException("Det sidste punkt i en kategori kan ikke fjernes.");
        }
        _readiness.Remove(item);
        Touch();
    }

    /// <summary>Changes one readiness item, if the role may change that part.</summary>
    public void UpdateReadiness(Guid itemId, ReadinessStatus status, string? notes, string? responsible, User by)
    {
        var item = FindReadiness(itemId);
        if (!ReadinessRules.CanEdit(by.Role, item.Category))
        {
            throw new ForbiddenException("Din rolle må ikke rette dette klargøringspunkt.");
        }
        item.Update(status, notes, responsible, by.Name);
        Touch();
    }

    private ReadinessItem FindReadiness(Guid itemId) =>
        _readiness.FirstOrDefault(item => item.Id == itemId) ?? throw new DomainException("Klargøringspunktet findes ikke.");

    public DateOnly BookedOn => DateOnly.FromDateTime(CreatedAt.UtcDateTime);

    /// <summary>The newest quote. A new quote replaces the last one as the one that counts.</summary>
    public AccountingReference? LatestQuote =>
        _references.Where(r => r.Type == DocumentType.Quote).OrderByDescending(r => r.CreatedAt).FirstOrDefault();

    public AccountingReference? InvoiceFor(InstalmentKind kind) =>
        _references.FirstOrDefault(r => r.Type == DocumentType.Invoice && r.Instalment == kind);

    public void RecordQuote(string number)
    {
        _references.Add(AccountingReference.Create(Id, DocumentType.Quote, number, null));
        Touch();
    }

    public void RecordInvoice(InstalmentKind kind, string number)
    {
        if (InvoiceFor(kind) is { } existing)
        {
            if (existing.Number == number) return;
            throw new DomainException("Raten er allerede faktureret.");
        }

        _references.Add(AccountingReference.Create(Id, DocumentType.Invoice, number, kind));
        Touch();
    }

    private int NextSortOrder() => _lines.Count == 0 ? 0 : _lines.Max(line => line.SortOrder) + 1;

    private BookingLine FindLine(Guid lineId) =>
        _lines.FirstOrDefault(line => line.Id == lineId) ?? throw new DomainException("Linjen findes ikke på bookingen.");

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
