using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WeddingBooking.Domain.Agreements;

namespace WeddingBooking.Integrations.Agreements;

/// <summary>
/// The agreement confirmation, drawn with QuestPDF: pure .NET, no browser in the
/// background, runs as is on App Service. Everything in it comes from the
/// booking; the clarification is the one part written by hand.
/// </summary>
internal sealed class QuestPdfAgreementRenderer : IAgreementRenderer
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly string Green = "#1F4D3A";

    public byte[] Render(AgreementContent content)
    {
        string Kroner(decimal amount) => amount.ToString("N2", Danish) + " kr.";
        string Date(DateOnly date) => date.ToString("dddd 'den' d. MMMM yyyy", Danish);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(50);
            page.DefaultTextStyle(text => text.FontSize(10.5f).LineHeight(1.35f));

            page.Header().Column(header =>
            {
                if (content.IsPreview)
                {
                    header.Item().Background(Colors.Grey.Lighten3).Padding(6).Text("FORHÅNDSVISNING — ikke gemt").FontSize(9).Bold();
                }

                header.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("Engestofte Gods").FontSize(18).Bold().FontColor(Green);
                        left.Item().Text("Aftalebekræftelse").FontSize(13);
                    });
                    row.ConstantItem(170).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text($"Version {content.Version}").Bold();
                        right.Item().AlignRight().Text($"Dannet {content.GeneratedAt.ToLocalTime():dd-MM-yyyy}");
                    });
                });
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });

            page.Content().PaddingTop(16).Column(column =>
            {
                column.Spacing(14);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(couple =>
                    {
                        couple.Item().Text("Brudeparret").FontSize(9).FontColor(Colors.Grey.Darken1);
                        couple.Item().Text(content.CustomerName).Bold();
                        if (content.Address is not null) couple.Item().Text(content.Address);
                        var city = $"{content.PostalCode} {content.City}".Trim();
                        if (city.Length > 0) couple.Item().Text(city);
                        foreach (var contact in content.Contacts)
                        {
                            couple.Item().Text(string.Join(" · ", new[] { contact.Name, contact.Email, contact.Phone }.Where(v => !string.IsNullOrWhiteSpace(v))));
                        }
                    });
                    row.RelativeItem().Column(wedding =>
                    {
                        wedding.Item().Text("Brylluppet").FontSize(9).FontColor(Colors.Grey.Darken1);
                        var day = Date(content.EventDate);
                        wedding.Item().Text(char.ToUpper(day[0], Danish) + day[1..]).Bold();
                        wedding.Item().Text($"{content.GuestCount} gæster");
                        wedding.Item().Text("Engestofte Gods, Maribo");
                    });
                });

                column.Item().Text("Det aftalte").FontSize(12).Bold().FontColor(Green);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(5);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().BorderBottom(1).PaddingVertical(4).Text("Indhold").Bold();
                        header.Cell().BorderBottom(1).PaddingVertical(4).AlignRight().Text("Antal").Bold();
                        header.Cell().BorderBottom(1).PaddingVertical(4).AlignRight().Text("Pris").Bold();
                        header.Cell().BorderBottom(1).PaddingVertical(4).AlignRight().Text("Beløb").Bold();
                    });
                    foreach (var line in content.Lines)
                    {
                        table.Cell().PaddingVertical(3).Column(cell =>
                        {
                            cell.Item().Text(line.Description);
                            if (!string.Equals(line.Group, line.Description, StringComparison.OrdinalIgnoreCase))
                            {
                                cell.Item().Text(line.Group).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                            }
                        });
                        table.Cell().PaddingVertical(3).AlignRight().Text(line.Quantity);
                        table.Cell().PaddingVertical(3).AlignRight().Text(Kroner(line.UnitPrice));
                        table.Cell().PaddingVertical(3).AlignRight().Text(Kroner(line.Total));
                    }
                    if (content.Surcharge is { } surcharge)
                    {
                        table.Cell().PaddingVertical(3).Text($"Tillæg for bryllup under {content.SurchargeThreshold} gæster");
                        table.Cell();
                        table.Cell();
                        table.Cell().PaddingVertical(3).AlignRight().Text(Kroner(surcharge));
                    }
                });
                column.Item().AlignRight().Text($"I alt {Kroner(content.Total)}").FontSize(12).Bold();

                if (content.Clarification is { } clarification)
                {
                    column.Item().Text("Præciseringer").FontSize(12).Bold().FontColor(Green);
                    column.Item().Text(clarification);
                }

                column.Item().Text("Betaling").FontSize(12).Bold().FontColor(Green);
                column.Item().Text(content.PaymentTermsText);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });
                    foreach (var payment in content.Payments)
                    {
                        table.Cell().PaddingVertical(2).Text(payment.Name);
                        table.Cell().PaddingVertical(2).Text($"senest {payment.DueDate.ToString("d. MMMM yyyy", Danish)}");
                        table.Cell().PaddingVertical(2).AlignRight().Text(Kroner(payment.Amount));
                    }
                });
                column.Item().Text("Reservationsgebyret sikrer datoen og modregnes i restbetalingen. Det tilbagebetales ikke ved afbestilling.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                column.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().Column(sign =>
                    {
                        sign.Item().PaddingBottom(28).Text("For brudeparret");
                        sign.Item().LineHorizontal(0.75f);
                        sign.Item().Text("Dato og underskrift").FontSize(8.5f);
                    });
                    row.ConstantItem(40);
                    row.RelativeItem().Column(sign =>
                    {
                        sign.Item().PaddingBottom(28).Text("For Engestofte Gods");
                        sign.Item().LineHorizontal(0.75f);
                        sign.Item().Text("Dato og underskrift").FontSize(8.5f);
                    });
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Aftalebekræftelse · ").FontSize(8.5f);
                text.Span(content.CustomerName).FontSize(8.5f);
                text.Span(" · side ").FontSize(8.5f);
                text.CurrentPageNumber().FontSize(8.5f);
                text.Span(" af ").FontSize(8.5f);
                text.TotalPages().FontSize(8.5f);
            });
        })).GeneratePdf();
    }
}
