using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Pdf;

/// <summary>
/// QuestPDF, Community license - a fluent C# document API with no HTML/browser rendering pipeline, which is
/// lighter for an Azure Functions host than a Puppeteer/Razor-to-PDF approach. Community licensing is free
/// under an annual gross revenue threshold (~USD 1M) - confirm current terms against SVG IT's actual revenue
/// before shipping (see QuestPDF.Settings.License in Program.cs).
/// </summary>
public class QuestPdfInvoiceRenderer : IPdfInvoiceRenderer
{
    public Task<byte[]> RenderAsync(InvoiceDocumentModel model, CancellationToken ct)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("INVOICE").FontSize(20).Bold();
                    column.Item().PaddingTop(4).Text($"Invoice Number: {model.InvoiceNumber}");
                    column.Item().Text($"Billing Period: {model.PeriodStart:d MMM yyyy} - {model.PeriodEnd:d MMM yyyy}");
                    column.Item().PaddingTop(8).Text(model.ClientName).Bold();
                    if (model.ClientAddress is not null) column.Item().Text(model.ClientAddress);
                    if (model.ExchangeRate is { } rate) column.Item().PaddingTop(4).Text($"Exchange rate: 1 GBP = {rate:0.####} {model.Currency}");
                });

                // Grouped by project (one InvoiceLineItem per entry now, not one per project - FDD: a line
                // shows staff/task date/description/rate as well as the project). The grand Total lives here,
                // as the last item in this flowing Column, rather than in page.Footer() - QuestPDF repeats a
                // page.Footer() on every page, which would print "Total: X" at the bottom of every page of a
                // multi-page invoice instead of once at the end.
                page.Content().PaddingTop(20).Column(column =>
                {
                    foreach (var projectGroup in model.LineItems.GroupBy(l => l.ProjectName))
                    {
                        column.Item().PaddingTop(12).Text(projectGroup.Key).Bold().FontSize(12);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Staff
                                columns.RelativeColumn(1); // Date
                                columns.RelativeColumn(3); // Description
                                columns.RelativeColumn(1); // Hours
                                columns.RelativeColumn(1); // Rate
                                columns.RelativeColumn(1); // Amount
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Staff").Bold();
                                header.Cell().Text("Date").Bold();
                                header.Cell().Text("Description").Bold();
                                header.Cell().AlignRight().Text("Hours").Bold();
                                header.Cell().AlignRight().Text("Rate").Bold();
                                header.Cell().AlignRight().Text($"Amount ({model.Currency})").Bold();
                                header.Cell().ColumnSpan(6).BorderBottom(1).PaddingBottom(4);
                            });

                            foreach (var line in projectGroup)
                            {
                                table.Cell().PaddingVertical(2).Text(line.StaffName ?? "-");
                                table.Cell().PaddingVertical(2).Text(line.TaskDate?.ToString("d MMM yyyy") ?? "-");
                                table.Cell().PaddingVertical(2).Text(line.Description);
                                table.Cell().PaddingVertical(2).AlignRight().Text(line.Hours?.ToString("0.00") ?? "-");
                                table.Cell().PaddingVertical(2).AlignRight().Text(line.Rate?.ToString("0.00") ?? "-");
                                table.Cell().PaddingVertical(2).AlignRight().Text(line.Amount.ToString("N2"));
                            }
                        });

                        column.Item().PaddingTop(2).AlignRight().Text($"Subtotal: {projectGroup.Sum(l => l.Amount):N2} {model.Currency}");
                    }

                    column.Item().PaddingTop(16).AlignRight().Text(text =>
                    {
                        text.Span("Total: ").Bold();
                        text.Span($"{model.TotalAmount:N2} {model.Currency}").Bold();
                    });
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
