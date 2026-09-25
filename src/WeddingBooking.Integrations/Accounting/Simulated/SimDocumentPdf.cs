using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WeddingBooking.Domain.Accounting;

namespace WeddingBooking.Integrations.Accounting.Simulated;

/// <summary>A quote or invoice as the accounting system would print it — marked as simulated.</summary>
internal static class SimDocumentPdf
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    public static byte[] Render(SimDocument document)
    {
        var title = document.Type == DocumentType.Quote ? "Tilbud" : "Faktura";
        string Kroner(decimal amount) => amount.ToString("N2", Danish) + " kr.";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(50);
            page.DefaultTextStyle(text => text.FontSize(10));

            page.Header().Column(column =>
            {
                column.Item().Background(Colors.Amber.Lighten4).Padding(6)
                    .Text("SIMULERET REGNSKAB — ikke et rigtigt bilag").FontSize(9).Bold().FontColor(Colors.Amber.Darken4);
                column.Item().PaddingTop(16).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("Engestofte Gods").FontSize(16).Bold();
                        left.Item().Text("Engestoftevej, 4930 Maribo");
                    });
                    row.RelativeItem().AlignRight().Column(right =>
                    {
                        right.Item().Text($"{title} nr. {document.Number}").FontSize(14).Bold();
                        right.Item().Text($"Dato: {document.CreatedAt:dd-MM-yyyy}");
                        if (document.DueDate is { } due) right.Item().Text($"Forfald: {due:dd-MM-yyyy}");
                        right.Item().Text($"Kundenr.: {document.CustomerNumber}");
                    });
                });
            });

            page.Content().PaddingTop(24).Column(column =>
            {
                var customer = document.Customer;
                column.Item().Text(customer.Name).Bold();
                if (customer.Address is not null) column.Item().Text(customer.Address);
                if (customer.PostalCode is not null || customer.City is not null) column.Item().Text($"{customer.PostalCode} {customer.City}".Trim());

                column.Item().PaddingTop(20).Text(document.Heading).FontSize(12).Bold();

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(5);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        foreach (var heading in new[] { "Beskrivelse", "Antal", "Pris", "Beløb" })
                        {
                            header.Cell().BorderBottom(1).PaddingVertical(4).Text(heading).Bold();
                        }
                    });

                    foreach (var line in document.Lines.OrderBy(l => l.SortOrder))
                    {
                        table.Cell().PaddingVertical(3).Text(line.Description);
                        table.Cell().PaddingVertical(3).AlignRight().Text(line.Quantity.ToString("0.##", Danish));
                        table.Cell().PaddingVertical(3).AlignRight().Text(Kroner(line.UnitPrice));
                        table.Cell().PaddingVertical(3).AlignRight().Text(Kroner(line.Quantity * line.UnitPrice));
                    }
                });

                column.Item().PaddingTop(10).AlignRight().Text($"I alt: {Kroner(document.Amount)}").FontSize(12).Bold();
                if (document.Type == DocumentType.Invoice && document.Paid > 0)
                {
                    column.Item().AlignRight().Text($"Betalt: {Kroner(document.Paid)} · Restbeløb: {Kroner(document.Remaining)}");
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Side ");
                text.CurrentPageNumber();
            });
        })).GeneratePdf();
    }
}
