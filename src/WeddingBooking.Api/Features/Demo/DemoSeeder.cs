using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.AccountingSim;
using WeddingBooking.Api.Features.Agreements;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Api.Features.Calendar;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Api.Features.Messages;
using WeddingBooking.Api.Features.Notes;
using WeddingBooking.Api.Features.Packages;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Api.Features.Public;
using WeddingBooking.Api.Features.Readiness;
using WeddingBooking.Api.Features.Trello;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Notes;
using WeddingBooking.Domain.Payments;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Users;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Demo;

/// <summary>
/// Fictional couples at every step of the flow, for showing the system. Run once with
/// <c>dotnet run --project src/WeddingBooking.Api -- seed-demo</c>; the data stays in the database.
///
/// Each step calls the same controller action the site does, as the first event manager or owner,
/// so quotes, invoices, agreements and the outbox are made by the code being shown. A couple that
/// already exists is skipped, so running it again is safe.
/// </summary>
public sealed class DemoSeeder(IServiceProvider services, AppDbContext db, CurrentUser currentUser, TimeProvider clock, IOptions<TrelloOptions> trello, ILogger<DemoSeeder> logger)
{
    // ponytail: the demo board, used when none is configured (as locally). Configure Trello:BoardIds to use another.
    private const string DemoBoard = "6abb765d215d08c7ccc2f5fd";

    private static readonly CancellationToken Ct = CancellationToken.None;
    private Dictionary<string, Guid> templates = [];
    private ViewingHostResponse? host;

    public static async Task RunAsync(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
    }

    private T C<T>() where T : ControllerBase => ActivatorUtilities.CreateInstance<T>(services);

    private async Task SeedAsync()
    {
        currentUser.Set(await db.Users.Where(u => u.IsActive && (u.Role == Role.EventManager || u.Role == Role.Owner)).OrderBy(u => u.CreatedAt).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Opret først en bruger med rollen EventManager eller Owner."));
        logger.LogInformation("Demodata som {User}.", currentUser.User.Name);

        await PriceList2027Async();
        await TemplatesAsync();
        await ViewingWeekAsync();
        await TrelloCouplesAsync();
        await EarlyCouplesAsync();
        logger.LogInformation("Demodata færdig.");
    }

    // ---- Groundwork

    private async Task PriceList2027Async()
    {
        var packages = C<PackagesController>();
        if ((await packages.List(2027, false, Ct)).Count > 0) return;
        foreach (var p in await packages.List(2026, false, Ct)) await packages.Copy(p.Id, new CopyPackageRequest(2027, 3), Ct);
        logger.LogInformation("Prisliste 2027 oprettet ud fra 2026 (+3 %).");
    }

    private async Task<Guid> PackageAsync(string name) =>
        (await C<PackagesController>().List(2027, false, Ct)).First(p => p.Name.StartsWith(name, StringComparison.Ordinal)).Id;

    private async Task TemplatesAsync()
    {
        var controller = C<MessageTemplatesController>();
        var existing = await controller.List(Ct);
        MessageTemplateRequest[] wanted =
        [
            new("Svar på forespørgsel", "Tak for jeres henvendelse, {{parret}}",
                "Kære {{parret}}\n\nTusind tak for jeres henvendelse om at holde jeres bryllup på Engestofte Gods. Jeg har vedhæftet vores grundpakke med priser og tilkøb.\n\nI er altid velkomne til at komme forbi og se stedet.\n\nVenlig hilsen\n{{afsender}}\nEngestofte Gods", false),
            new("Mødebekræftelse – fremvisning", "Fremvisning på Engestofte Gods {{fremvisning}}",
                "Kære {{parret}}\n\nHermed bekræftet: I bliver vist rundt {{fremvisning}} af {{vært}}.\nHvis I bliver forsinkede på dagen, så ring til {{vært}} på {{vært_telefon}}.\n\nVi glæder os til at se jer.\n\nVenlig hilsen\n{{afsender}}", true),
            new("Tilbud på bryllup", "Tilbud på jeres bryllup {{dato}}",
                "Kære {{parret}}\n\nTak for et godt møde. Som aftalt sender jeg tilbuddet på jeres bryllup {{dato}} for {{gæster}} gæster. Den samlede pris er {{pris}}.\n\nAftalebekræftelsen og vores generelle betingelser ligger ved. Skriv under og send den retur, så er datoen jeres, når reservationsgebyret er betalt.\n\nVenlig hilsen\n{{afsender}}", false),
            new("Bekræftelse af booking", "Jeres dato er sikret — {{dato}}",
                "Kære {{parret}}\n\nVi har modtaget den underskrevne aftale og reservationsgebyret. Jeres dato {{dato}} er hermed sikret.\n\nVi vender tilbage efter nytår og går menu, opdækning og pynt igennem med jer.\n\nVenlig hilsen\n{{afsender}}", false),
        ];
        foreach (var t in wanted.Where(t => existing.All(e => e.Name != t.Name))) await controller.Create(t, Ct);
        templates = (await controller.List(Ct)).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First().Id);
    }

    /// <summary>A week for the first host, so the public page has viewing times. Left alone if anyone has one.</summary>
    private async Task ViewingWeekAsync()
    {
        host = (await C<BookingsController>().ViewingHosts(Ct)).FirstOrDefault();
        var calendar = C<CalendarController>();
        if (host is null || (await calendar.Availability(Ct)).Any(w => w.Windows.Count > 0)) return;
        await calendar.SetAvailability(host.Id,
        [
            new(DayOfWeek.Tuesday, new(10, 0), new(16, 0)),
            new(DayOfWeek.Thursday, new(10, 0), new(16, 0)),
            new(DayOfWeek.Saturday, new(10, 0), new(14, 0)),
        ], Ct);
        logger.LogInformation("Ugeplan for fremvisninger sat for {Host}.", host.Name);
    }

    // ---- 1–3: the couples on the Trello board, imported so booking and card belong together.

    private sealed record Couple(string First, PartnerRequest[] People, int Payer, UpdateCustomerRequest Address);

    private async Task TrelloCouplesAsync()
    {
        var boards = trello.Value.BoardIds.Length > 0 ? trello.Value.BoardIds : [DemoBoard];
        var report = await C<TrelloImportController>().Import(null, string.Join(",", boards), Ct);
        var cards = report.Boards.SelectMany(b => b.Cards).Where(c => c.BookingId is not null).ToList();
        logger.LogInformation("Trello: {Imported} importeret, {Already} fandtes allerede.", report.Imported, report.AlreadyImported);

        async Task Run(Couple couple, Func<Guid, Task> flow)
        {
            var card = cards.FirstOrDefault(c => c.CoupleName?.StartsWith(couple.First, StringComparison.Ordinal) == true);
            if (card is null) { logger.LogWarning("{Couple}: ikke på Trello-boardet — springes over.", couple.First); return; }
            var id = card.BookingId!.Value;
            if ((await BookingAsync(id)).Lines.Count > 0) { logger.LogInformation("{Couple}: findes allerede — springes over.", couple.First); return; }
            await PeopleAsync(id, couple);
            await flow(id);
            logger.LogInformation("{A} og {B}: færdig.", couple.People[0].FirstName, couple.People[1].FirstName);
        }

        await Run(new("Tobias",
            [new("Tobias", "Holm", "tobias.holm@example.com", "11 22 33 44"), new("Cecilie", "Lund", "cecilie.lund@example.com", "55 66 77 88")],
            2, new("Søndergade 12", "4930", "Maribo")), async id =>
        {
            await AddPackagesAsync(id, "Grundpakke", "Natmad");
            await NoteAsync(id, NoteType.Call, "Cecilie ringede efter bryllupsmessen. De er 80 gæster og vil gerne have natmad. Morgenmad dagen efter er ikke afgjort.");
            await QuoteAndAgreementAsync(id, "Morgenmad dagen efter er ikke med i denne aftale. Den kan tilføjes senere.");
            await MailAsync(id, "Tilbud på bryllup");
            await InvoiceAsync(id, InstalmentKind.ReservationFee);
            await ReadinessAsync(id, new() { [ReadinessCategory.Contact] = (ReadinessStatus.InProgress, "Mangler toastmaster", "Johan") });
        });

        await Run(new("Mads",
            [new("Mads", "Krogh", "mads.krogh@example.com", "22 33 44 55"), new("Sofie", "Bech", "sofie.bech@example.com", "66 77 88 99")],
            1, new("Vestergade 4", "4800", "Nykøbing F")), async id =>
        {
            await AddPackagesAsync(id, "Grundpakke", "Fredagsarrangement");
            await NoteAsync(id, NoteType.Email, "Sofie skriver: vi bliver nok 55, men vil gerne have grill fredag aften for de nærmeste — vi vender tilbage med antal.");
            await QuoteAndAgreementAsync(id, "Fredagsgrillen er for 30 personer; antallet kan justeres indtil en måned før.");
            await MailAsync(id, "Tilbud på bryllup");
            await InvoiceAsync(id, InstalmentKind.ReservationFee, pay: true);
            await MailAsync(id, "Bekræftelse af booking");
            await InvoiceAsync(id, InstalmentKind.Instalment, book: false);
            await ReadinessAsync(id, new()
            {
                [ReadinessCategory.Contact] = (ReadinessStatus.Done, null, "Johan"),
                [ReadinessCategory.Food] = (ReadinessStatus.InProgress, "2 vegetarer, 1 glutenfri. Fredagsgrill: antal afventer svar.", "Kenneth"),
            });
        });

        await Run(new("Emil",
            [new("Emil", "Dahl", "emil.dahl@example.com", "33 44 55 66"), new("Laura", "Friis", "laura.friis@example.com", "77 88 99 00")],
            2, new("Strandvejen 30", "4900", "Nakskov")), async id =>
        {
            await AddPackagesAsync(id, "Grundpakke", "Natmad", "Spiritus");
            await NoteAsync(id, NoteType.Internal, "Alt er gennemgået med parret. Bryan har kørselsplanen med toastmasteren.");
            await QuoteAndAgreementAsync(id, "Vielsen foregår i haven; ved regn flyttes den til værkstedet.");
            await MailAsync(id, "Tilbud på bryllup");
            await InvoiceAsync(id, InstalmentKind.ReservationFee, pay: true);
            await MailAsync(id, "Bekræftelse af booking");
            await InvoiceAsync(id, InstalmentKind.Instalment, pay: true);
            await InvoiceAsync(id, InstalmentKind.FinalPayment, book: false);
            await ReadinessAsync(id, new()
            {
                [ReadinessCategory.Contact] = (ReadinessStatus.Done, "Toastmaster: Jens, 40 50 60 70", "Johan"),
                [ReadinessCategory.TableSetting] = (ReadinessStatus.Done, "Lange borde i laden, reception i værkstedet", "Bryan"),
                [ReadinessCategory.Decoration] = (ReadinessStatus.Done, "Blomster fra Maribo Blomster", "Bryan"),
                [ReadinessCategory.Food] = (ReadinessStatus.Done, "110 kuverter, 4 vegetarer", "Kenneth"),
                [ReadinessCategory.PostEventInvoicing] = (ReadinessStatus.Done, null, "Johan"),
            });
        });
    }

    // ---- 4–7: couples without a Trello card, in the early steps.

    private async Task EarlyCouplesAsync()
    {
        var bookings = C<BookingsController>();
        var requests = C<PublicRequestsController>();

        // 4: Created by Johan after a call; a viewing is booked.
        if (!await ExistsAsync("Jonas"))
        {
            var b = Value(await bookings.Create(new CreateBookingRequest(null,
                [new("Jonas", "Winther", "jonas.winther@example.com", "44 55 66 77"), new("Ida", "Kjær", "ida.kjaer@example.com", "88 99 00 11")],
                new DateOnly(2027, 9, 11), 70, PaymentTerms.New), Ct));
            await NoteAsync(b.Id, NoteType.Call, "Ida ringede. De har set os på Instagram og vil gerne se stedet en lørdag.");
            await MailAsync(b.Id, "Svar på forespørgsel");
            if ((await requests.ViewingTimes(Ct)).Skip(2).FirstOrDefault() is { } slot)
            {
                await bookings.ScheduleViewing(b.Id, new ScheduleViewingRequest(slot.Start, host?.Id), Ct);
                await MailAsync(b.Id, "Mødebekræftelse – fremvisning");
            }
            logger.LogInformation("Jonas og Ida: forespørgsel med fremvisning.");
        }

        // 5: A viewing booked from the website — waits as a new request.
        if (!await ExistsAsync("Anna") && (await requests.ViewingTimes(Ct)).FirstOrDefault() is { } annaSlot)
        {
            await requests.RequestViewing(new ViewingRequest(
                [new("Anna", "Møller", "anna.moeller@example.com", "12 12 34 34"), new("Frederik", "Berg", "frederik.berg@example.com", "56 56 78 78")],
                annaSlot.Start, new DateOnly(2028, 6, 17), 90, true, null), Ct);
            logger.LogInformation("Anna og Frederik: fremvisning bestilt fra hjemmesiden.");
        }

        // 6: The wedding itself booked from the website on a free Sunday — the date is held.
        if (!await ExistsAsync("Oliver"))
        {
            var dates = await requests.WeddingDates(Ct);
            var date = dates.FirstOrDefault(d => d.DayOfWeek == DayOfWeek.Sunday, dates[0]);
            await requests.RequestWedding(new WeddingRequest(
                [new("Oliver", "Juhl", "oliver.juhl@example.com", "23 23 45 45"), new("Freja", "Lind", "freja.lind@example.com", "67 67 89 89")],
                date, 65, true, null), Ct);
            logger.LogInformation("Oliver og Freja: bryllup bestilt fra hjemmesiden {Date}.", date);
        }

        // 7: A viewing that ended in a no — lost with the reason, its time freed.
        if (!await ExistsAsync("Lucas") && (await requests.ViewingTimes(Ct)).Skip(1).FirstOrDefault() is { } lucasSlot)
        {
            await requests.RequestViewing(new ViewingRequest(
                [new("Lucas", "Holt", "lucas.holt@example.com", "34 34 56 56"), new("Clara", "Brandt", "clara.brandt@example.com", "78 78 90 90")],
                lucasSlot.Start, new DateOnly(2027, 8, 28), 120, true, null), Ct);
            var id = await db.Bookings.Where(b => b.Customer.Contacts.Any(c => c.FirstName == "Lucas")).Select(b => b.Id).FirstAsync();
            await bookings.AcceptRequest(id, Ct);
            await NoteAsync(id, NoteType.Call, "Clara ringede efter fremvisningen: de har valgt et sted tættere på København.");
            await bookings.DeclineRequest(id, new DeclineRequest("Har valgt et sted tættere på København."), currentUser, clock, Ct);
            logger.LogInformation("Lucas og Clara: fremvisning afvist som tabt.");
        }
    }

    // ---- Steps

    private async Task<BookingResponse> BookingAsync(Guid id) => Value(await C<BookingsController>().Get(id, Ct));

    private Task<bool> ExistsAsync(string firstName) => db.Customers.AnyAsync(c => c.Contacts.Any(p => p.FirstName == firstName));

    private async Task PeopleAsync(Guid bookingId, Couple couple)
    {
        var customers = C<CustomersController>();
        var customer = (await BookingAsync(bookingId)).Customer;
        foreach (var (contact, person) in customer.Contacts.OrderBy(c => c.Position).Zip(couple.People))
        {
            await customers.UpdatePartner(customer.Id, contact.Id, person, Ct);
        }
        await customers.Update(customer.Id, couple.Address, Ct);
        await customers.MakePayer(customer.Id, customer.Contacts.Single(c => c.Position == couple.Payer).Id, Ct);
    }

    private async Task AddPackagesAsync(Guid id, params string[] names)
    {
        foreach (var name in names) await C<BookingLinesController>().AddPackage(id, new AddPackageRequest(await PackageAsync(name)), Ct);
    }

    private Task NoteAsync(Guid id, NoteType type, string text) => C<NotesController>().Write(id, new WriteNoteRequest(type, text), Ct);

    private async Task MailAsync(Guid id, string template)
    {
        var messages = C<BookingMessagesController>();
        var draft = Value(await messages.Draft(id, new DraftRequest(templates[template]), Ct));
        // A placeholder the booking has no value for (a host without a phone) goes with its line.
        var body = string.Join("\n", draft.Body.Split('\n').Where(line => !line.Contains("{{")));
        await messages.Send(id, new SendRequest(draft.To, draft.Cc, draft.Subject, body), Ct);
    }

    private async Task QuoteAndAgreementAsync(Guid id, string clarification)
    {
        var quote = Value(await C<BookingPaymentsController>().CreateQuote(id, Ct)).Quote!;
        var agreements = C<AgreementsController>();
        var version = Value(await agreements.Generate(id, new ClarificationRequest(clarification), Ct)).Versions.MaxBy(v => v.Number)!;
        await agreements.AttachToQuote(id, version.Id, Ct);
        await C<AccountingSimController>().MarkSent(quote.Number, Ct);
        await C<BookingsController>().ChangeStage(id, new ChangeStageRequest(SalesStage.QuoteSent), Ct);

        // The couple's signed copy: for the demo, the generated PDF itself.
        var pdf = ((FileContentResult)await agreements.Pdf(id, version.Id, Ct)).FileContents;
        var file = new FormFile(new MemoryStream(pdf), 0, pdf.Length, "file", "aftale-underskrevet.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
        await agreements.UploadSigned(id, version.Id, file, Ct);
    }

    private async Task InvoiceAsync(Guid id, InstalmentKind kind, bool book = true, bool pay = false)
    {
        var payments = C<BookingPaymentsController>();
        var invoice = Value(await payments.CreateInvoice(id, new CreateInvoiceRequest(kind), Ct)).Instalments.Single(i => i.Kind == kind).Invoice!;
        if (book) await payments.BookInvoice(id, invoice.Number, Ct);
        if (pay) await C<AccountingSimController>().RegisterPayment(invoice.Number, new RegisterPaymentRequest(invoice.Amount, null), Ct);
    }

    private async Task ReadinessAsync(Guid id, Dictionary<ReadinessCategory, (ReadinessStatus Status, string? Notes, string? Responsible)> wanted)
    {
        var readiness = C<ReadinessController>();
        foreach (var item in Value(await readiness.Get(id, Ct)).Items)
        {
            if (wanted.TryGetValue(item.Category, out var w)) await readiness.Update(id, item.Id, new UpdateReadinessRequest(w.Status, w.Notes, w.Responsible), Ct);
        }
    }

    private static T Value<T>(ActionResult<T> result) =>
        result.Value ?? (result.Result is ObjectResult { Value: T value } ? value : throw new InvalidOperationException($"Svaret var {result.Result?.GetType().Name}."));
}
