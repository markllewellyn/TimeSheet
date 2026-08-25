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
    IRateResolver rateResolver,
    ICurrencyConversionService currencyConversion) : IInvoiceGenerationService
{
    public async Task<Invoice> BuildDraftAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var client = await clients.GetByIdAsync(clientId, ct)
            ?? throw new InvalidOperationException($"Client {clientId} not found.");
        var targetCurrency = client.ReportingCurrencyCode;

        var clientProjects = await projects.GetByClientIdAsync(clientId, includeInactive: true, ct);
        var lineItems = new List<InvoiceLineItem>();

        foreach (var project in clientProjects)
        {
            var projectCurrency = project.CurrencyOverride ?? targetCurrency;

            if (project.PaymentModel == PaymentModel.TimeAndMaterials)
            {
                var entries = await timesheetEntries.GetCountedForProjectAsync(project.Id, periodStart, periodEnd, ct);
                if (entries.Count > 0)
                {
                    decimal totalHours = 0;
                    decimal amountNative = 0;

                    foreach (var byUser in entries.GroupBy(e => e.UserId))
                    {
                        var hours = byUser.Sum(e => e.WorkHours + e.OutOfHoursHours);
                        var rate = await rateResolver.GetEffectiveRateAsync(project.Id, byUser.Key, periodEnd, ct);
                        totalHours += hours;
                        amountNative += hours * (rate.BillingRatePerHour ?? 0);
                    }

                    var converted = await currencyConversion.ConvertAsync(new Money(amountNative, projectCurrency), targetCurrency, periodEnd, ct);
                    lineItems.Add(new InvoiceLineItem
                    {
                        ProjectId = project.Id,
                        Description = project.Name,
                        Hours = totalHours,
                        Amount = converted.Amount,
                        Type = InvoiceLineItemType.TimeAndMaterials,
                    });
                }
            }
            else // FixedProjectCost - the flat fee billed each invoicing period; hours retained internally only.
            {
                if (project.FixedFeeAmount is > 0)
                {
                    var converted = await currencyConversion.ConvertAsync(new Money(project.FixedFeeAmount.Value, projectCurrency), targetCurrency, periodEnd, ct);
                    lineItems.Add(new InvoiceLineItem
                    {
                        ProjectId = project.Id,
                        Description = $"{project.Name} (Fixed Fee)",
                        Hours = null,
                        Amount = converted.Amount,
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
                    var converted = await currencyConversion.ConvertAsync(new Money(expense.Amount, expense.Currency), targetCurrency, periodEnd, ct);
                    expenseTotal += converted.Amount;
                }

                lineItems.Add(new InvoiceLineItem
                {
                    ProjectId = project.Id,
                    Description = $"{project.Name} - Expenses",
                    Hours = null,
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
            Status = InvoiceStatus.Draft,
            TotalAmount = Math.Round(lineItems.Sum(l => l.Amount), 2),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            LineItems = lineItems,
        };
    }
}
