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
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "GBP", null,
            [
                new InvoiceDocumentLine("D365 Transformation", "Sarah Chen", new DateOnly(2026, 8, 12), "Migration workshop", 40m, 100m, 4000m),
                new InvoiceDocumentLine("D365 Transformation", "Mark Llewellyn", new DateOnly(2026, 8, 14), "Client travel", null, null, 120.50m),
            ],
            4120.50m);

        var renderer = new QuestPdfInvoiceRenderer();
        var pdfBytes = await renderer.RenderAsync(model, CancellationToken.None);

        Assert.NotEmpty(pdfBytes);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }
}
