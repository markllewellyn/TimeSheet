using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Collates a billing period's TimesheetEntry/ExpenseEntry rows per client into presentation-ready
/// InvoiceLineItems: one line per entry for Time & Materials and Expenses (FDD: a line shows the staff
/// member's name, the task date, the description and the rate - not just a project total), a single line per
/// Fixed Project Cost project (no natural per-entry shape for a flat fee), everything converted into the
/// CLIENT's single reporting currency so the invoice totals in one coherent currency even when individual
/// projects carry a CurrencyOverride for their own reporting.
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
            // Legacy [Projects].CanInvoice - "not true" (null OR false) means not invoiceable, matching the
            // established convention elsewhere this flag is already read: ExpenseEntriesFunctions' Contract-kind
            // gate (`CanInvoice == true` is rejected for Contract values - i.e. Contract values require a
            // non-true CanInvoice) and project-edit-page.ts's own comment ("Null and false both mean 'not
            // invoiceable'"). Excludes a project from every kind of line item (T&M, Fixed Fee, and Expense
            // alike), not just some of them.
            if (project.CanInvoice != true) continue;

            var projectCurrency = project.CurrencyOverride ?? targetCurrency;

            if (project.PaymentModel == PaymentModel.TimeAndMaterials)
            {
                // One InvoiceLineItem per entry, not a project-level rollup - FDD: a finalized invoice line
                // shows "the staff member's name, the project name, the task date, the description... the
                // rate". Rate/amount come from each entry's own stamped ResolvedCustomerRate (see
                // TimesheetEntry), not re-resolved via IRateResolver here - so the rate that actually applied
                // when the entry was recorded is what gets billed, never "today's" rate.
                var entries = await timesheetEntries.GetCountedForInvoicingAsync(project.Id, periodStart, periodEnd, ct);
                foreach (var entry in entries)
                {
                    var hours = entry.WorkHours + entry.OutOfHoursHours;
                    var amountNative = hours * (entry.ResolvedCustomerRate ?? 0);
                    var amount = await ConvertAsync(amountNative, projectCurrency);

                    lineItems.Add(new InvoiceLineItem
                    {
                        ProjectId = project.Id,
                        Description = entry.Description,
                        Hours = hours,
                        Rate = entry.ResolvedCustomerRate,
                        StaffId = entry.UserId,
                        StaffName = entry.User?.DisplayName,
                        TaskDate = entry.Date,
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

            // One line per expense, same reasoning as the T&M entries above - who incurred it, when, and what
            // it was for, not a single rolled-up "Project - Expenses" total.
            var billableExpenses = await expenseEntries.GetBillableForProjectAsync(project.Id, periodStart, periodEnd, ct);
            foreach (var expense in billableExpenses)
            {
                var amount = Math.Round(await ConvertAsync(expense.Amount, expense.Currency), 2);
                lineItems.Add(new InvoiceLineItem
                {
                    ProjectId = project.Id,
                    Description = expense.Description ?? "Expense",
                    Hours = null,
                    Rate = null,
                    StaffId = expense.UserId,
                    StaffName = expense.User?.DisplayName,
                    TaskDate = expense.Date,
                    GrossAmount = amount,
                    Amount = amount,
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
