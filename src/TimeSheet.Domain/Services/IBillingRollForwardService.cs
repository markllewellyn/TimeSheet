namespace TimeSheet.Domain.Services;

/// <summary>FDD Technical Architecture: "monthly recurring project billing and billing period roll-forward."
/// For every Monthly-billing-period Client whose current period has closed, generates a Draft invoice for that
/// period (via the existing IInvoicingService - a Draft is always safe to freely regenerate) and advances the
/// client's window to the next calendar month. Never finalizes/sends anything - an admin still reviews and
/// finalizes the Draft manually.</summary>
public interface IBillingRollForwardService
{
    Task<BillingRollForwardResult> RunAsync(DateOnly asOfDate, CancellationToken ct);
}

public record BillingRollForwardResult(int ClientsDue, int InvoicesGenerated, int Failed);
