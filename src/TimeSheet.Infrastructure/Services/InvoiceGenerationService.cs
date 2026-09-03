using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Collates a billing period's TimesheetEntry/ExpenseEntry rows per client into presentation-ready
/// InvoiceLineItems: grouped by project (not by day/raw row), rates/fixed-fee applied by payment model,
/// internal-only fields (raw descriptions, UserId, escalation history) never surfaced, everything converted
/// into the CLIENT's single reporting currency so the invoice totals in one coherent currency even when
/// individual projects carry a CurrencyOverride for their own reporting.
/// </summary>
public class InvoiceGenerationService(
    IClientRepository clients,
    IProjectRepository projects,
    ITimesheetEntryRepository timesheetEntries,
    IExpenseEntryRepository expenseEntries,
    ICurrencyConversionService currencyConversion) : IInvoiceGenerationService
{
    public async Task<Invoice> BuildDraftAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct)
    {
        var client = await clients.GetByIdAsync(clientId, ct)
            ?? throw new InvalidOperationException($"Client {clientId} not found.");
        var targetCurrency = client.ReportingCurrencyCode;

        if (manualExchangeRate is not null)
        {
            if (string.Equals(targetCurrency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This client's reporting currency is GBP; an exchange rate is not applicable.");
            }
            if (manualExchangeRate <= 0)
            {
                throw new InvalidOperationException("The exchange rate must be a positive number.");
            }
        }

        // When a manual rate is supplied it REPLACES automatic (Frankfurter-based) conversion entirely for
        // this run - every native amount is treated as GBP and multiplied straight through by the rate. This
        // deliberately bypasses the per-project CurrencyOverride and ExpenseEntry.Currency distinctions
        // (pre-existing ambiguities - StaffCost.CustomerRate and ExpenseEntry.Currency have no guarantee of
        // actually being GBP - out of scope for this feature).
        async Task<decimal> ConvertAsync(decimal amountNative, string nativeCurrency) =>
            manualExchangeRate is { } rate
                ? amountNative * rate
                : (await currencyConversion.ConvertAsync(new Money(amountNative, nativeCurrency), targetCurrency, periodEnd, ct)).Amount;

        var clientProjects = await projects.GetByClientIdAsync(clientId, includeInactive: true, ct);
        var lineItems = new List<InvoiceLineItem>();

        foreach (var project in clientProjects)
        {
            // Legacy [Projects].CanInvoice, null = invoiceable (the pre-existing default for every project
            // predating this flag being wired up) - only an explicit false excludes a project from every kind
            // of line item (T&M, Fixed Fee, and Expense alike), not just some of them.
            if (project.CanInvoice == false) continue;

            var projectCurrency = project.CurrencyOverride ?? targetCurrency;

            if (project.PaymentModel == PaymentModel.TimeAndMaterials)
            {
                var entries = await timesheetEntries.GetCountedForInvoicingAsync(project.Id, periodStart, periodEnd, ct);
                if (entries.Count > 0)
                {
                    // Summed from each entry's stamped ResolvedCustomerRate (see TimesheetEntry), not
                    // re-resolved via IRateResolver here - so the rate that actually applied when the entry
                    // was recorded is what gets billed, never "today's" rate.
                    var totalHours = entries.Sum(e => e.WorkHours + e.OutOfHoursHours);
                    var amountNative = entries.Sum(e => (e.WorkHours + e.OutOfHoursHours) * (e.ResolvedCustomerRate ?? 0));

                    var amount = await ConvertAsync(amountNative, projectCurrency);
                    lineItems.Add(new InvoiceLineItem
                    {
                        ProjectId = project.Id,
                        Description = project.Name,
                        Hours = totalHours,
                        GrossAmount = amount,
                        Amount = amount,
                        Type = InvoiceLineItemType.TimeAndMaterials,
                    });
                }
            }
            else // FixedProjectCost - the flat fee billed each invoicing period; hours retained internally only.
            {
                if (project.FixedFeeAmount is > 0)
                {
                    var amount = await ConvertAsync(project.FixedFeeAmount.Value, projectCurrency);
                    lineItems.Add(new InvoiceLineItem
                    {
                        ProjectId = project.Id,
                        Description = $"{project.Name} (Fixed Fee)",
                        Hours = null,
                        GrossAmount = amount,
                        Amount = amount,
                        Type = InvoiceLineItemType.FixedFee,
                    });
                }
            }

            var billableExpenses = await expenseEntries.GetBillableForProjectAsync(project.Id, periodStart, periodEnd, ct);
            if (billableExpenses.Count > 0)
            {
                decimal expenseTotal = 0;
                foreach (var expense in billableExpenses)
                {
                    expenseTotal += await ConvertAsync(expense.Amount, expense.Currency);
                }

                lineItems.Add(new InvoiceLineItem
                {
                    ProjectId = project.Id,
                    Description = $"{project.Name} - Expenses",
                    Hours = null,
                    GrossAmount = Math.Round(expenseTotal, 2),
                    Amount = Math.Round(expenseTotal, 2),
                    Type = InvoiceLineItemType.Expense,
                });
            }
        }

        return new Invoice
        {
            ClientId = clientId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ReportingCurrency = targetCurrency,
            ExchangeRate = manualExchangeRate,
            Status = InvoiceStatus.Draft,
            TotalAmount = Math.Round(lineItems.Sum(l => l.Amount), 2),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            LineItems = lineItems,
        };
    }
}
