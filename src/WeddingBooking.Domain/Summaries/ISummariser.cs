namespace WeddingBooking.Domain.Summaries;

/// <summary>
/// A short, structured summary of a case, for picking up a booking that has lain
/// still for months. It suggests; it never acts, and it is never written into the
/// booking by itself.
/// </summary>
public interface ISummariser
{
    /// <summary>Which adapter answered — shown with the summary.</summary>
    string Source { get; }

    Task<CaseSummary> SummariseAsync(SummaryInput input, CancellationToken ct);
}

/// <param name="Facts">
/// The booking's own data, as text lines. Null on the standalone page. The model
/// is told not to state numbers; guest count, dates and amounts are shown from here.
/// </param>
/// <param name="Text">Notes and pasted mail threads, oldest first.</param>
public sealed record SummaryInput(IReadOnlyList<string>? Facts, string Text);

public sealed record CaseSummary(string Status, string AgreedContent, IReadOnlyList<string> OpenItems, string LastContact);
