using System.Text;
using QuestPDF.Infrastructure;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Pdf;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class QuestPdfInvoiceRendererTests
{
    public QuestPdfInvoiceRendererTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_ProducesAValidPdfDocument()
    {
        var model = new InvoiceDocumentModel(
            "Antigua Ltd", "1 Example Street, London", "INV-0001",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "GBP",
            [new InvoiceDocumentLine("D365 Transformation", 40m, 4000m), new InvoiceDocumentLine("Expenses", null, 120.50m)],
            4120.50m);

        var renderer = new QuestPdfInvoiceRenderer();
        var pdfBytes = await renderer.RenderAsync(model, CancellationToken.None);

        Assert.NotEmpty(pdfBytes);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }
}
