using TimeSheet.Domain;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class RevenueRecognitionService(IProjectRepository projects, ICurrencyConversionService currencyConversion) : IRevenueRecognitionService
{
    /// <summary>
    /// Only meaningful for Fixed Project Cost projects: (hours consumed / BudgetHours) * FixedFeeAmount, since
    /// there's no per-hour billing rate to sum - ties revenue recognition to the same logged-time data the
    /// report is already querying. Time & Materials revenue is computed directly by ReportingService instead
    /// (per-user hours * per-user billing rate, via IRateResolver), since it already needs that per-user
    /// breakdown for the Profit-by-user report - this service is deliberately not involved in that trivial
    /// case, only in isolating the Fixed-Cost special case from it.
    /// </summary>
    public async Task<decimal> GetRecognizedRevenueAsync(int projectId, decimal hoursInPeriod, string targetCurrency, DateOnly asOf, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.PaymentModel != PaymentModel.FixedProjectCost) return 0;
        // Mirrors ReportingRepository's BilledAmountNative rule for Time & Materials - a project that will
        // never actually be invoiced (see InvoiceGenerationService) must not show recognized revenue either.
        if (project.CanInvoice != true) return 0;

        var amountNative = project.BudgetHours is > 0 && project.FixedFeeAmount is not null
            ? hoursInPeriod / project.BudgetHours.Value * project.FixedFeeAmount.Value
            : 0;

        var projectCurrency = project.CurrencyOverride ?? targetCurrency;
        var converted = await currencyConversion.ConvertAsync(new Money(amountNative, projectCurrency), targetCurrency, asOf, ct);
        return converted.Amount;
    }
}
