using System.Text.RegularExpressions;

using WeddingBooking.Domain.Summaries;

namespace WeddingBooking.Integrations.Summaries;

/// <summary>
/// The fixed adapter, used when there is no OpenAI key: no model, just the
/// booking's facts and the text's own sentences, picked by a few plain rules.
/// </summary>
internal sealed partial class OfflineSummariser : ISummariser
{
    public string Source => "Uden AI — ingen OpenAI-nøgle er sat op";

    private static readonly string[] OpenWords = ["afventer", "vender tilbage", "vente", "overvejer", "mangler", "spørger", "?", "ikke besluttet", "senere"];
    private static readonly string[] Greetings = ["hej", "kære", "tak for", "mange hilsner", "venlig hilsen", "vh"];

    public Task<CaseSummary> SummariseAsync(SummaryInput input, CancellationToken ct)
    {
        // Greetings and sign-offs say nothing about the case.
        var sentences = SentenceBreak().Split(input.Text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 3 && !Greetings.Any(g => s.StartsWith(g, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var open = sentences.Where(s => OpenWords.Any(w => s.Contains(w, StringComparison.OrdinalIgnoreCase))).Distinct().Take(5).ToList();
        var status = input.Facts is { Count: > 0 } facts ? facts[0] : sentences.FirstOrDefault() ?? "Ingen tekst at opsummere.";
        var agreed = input.Facts?.Skip(1).FirstOrDefault(f => f.StartsWith("Indhold", StringComparison.Ordinal)) ?? "Se teksten.";
        var last = sentences.LastOrDefault() ?? "Ingen kontakt registreret.";

        return Task.FromResult(new CaseSummary(status, agreed, open, last));
    }

    [GeneratedRegex(@"(?<=[.!?])\s+|\n+")]
    private static partial Regex SentenceBreak();
}
