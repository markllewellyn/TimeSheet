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
                });

                page.Content().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Description").Bold();
                        header.Cell().AlignRight().Text("Hours").Bold();
                        header.Cell().AlignRight().Text($"Amount ({model.Currency})").Bold();
                        header.Cell().ColumnSpan(3).BorderBottom(1).PaddingBottom(4);
                    });

                    foreach (var line in model.LineItems)
                    {
                        table.Cell().PaddingVertical(2).Text(line.Description);
                        table.Cell().PaddingVertical(2).AlignRight().Text(line.Hours?.ToString("0.00") ?? "-");
                        table.Cell().PaddingVertical(2).AlignRight().Text(line.Amount.ToString("N2"));
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Total: ").Bold();
                    text.Span($"{model.TotalAmount:N2} {model.Currency}").Bold();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
