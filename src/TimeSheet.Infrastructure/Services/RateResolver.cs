using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class RateResolver(IRateCardRepository rateCards, IStaffCostRepository staffCosts, IUserRepository users) : IRateResolver
{
    public async Task<RateResolution> ResolveAsync(int staffId, int clientId, int projectId, DateOnly asOfDate, CancellationToken ct)
    {
        var staff = await users.GetByIdAsync(staffId, ct);
        var roleId = staff?.JobRoleId;

        RateCard? rateCard = await rateCards.FindAsync(null, staffId, null, projectId, asOfDate, ct);
        var tier = RateCardTier.PersonProject;

        if (rateCard is null)
        {
            rateCard = await rateCards.FindAsync(null, staffId, clientId, null, asOfDate, ct);
            tier = RateCardTier.PersonClient;
        }
        if (rateCard is null && roleId is not null)
        {
            rateCard = await rateCards.FindAsync(roleId, null, null, projectId, asOfDate, ct);
            tier = RateCardTier.RoleProject;
        }
        if (rateCard is null && roleId is not null)
        {
            rateCard = await rateCards.FindAsync(roleId, null, clientId, null, asOfDate, ct);
            tier = RateCardTier.RoleClient;
        }
        if (rateCard is null && roleId is not null)
        {
            rateCard = await rateCards.FindAsync(roleId, null, null, null, asOfDate, ct);
            tier = RateCardTier.RoleDefault;
        }
        if (rateCard is null)
        {
            throw RateNotConfiguredException.NoRateCard(staffId, clientId, projectId);
        }

        var cost = await staffCosts.GetCurrentAsync(staffId, asOfDate, ct)
            ?? throw RateNotConfiguredException.NoStaffCost(staffId);

        // FDD: a RateCard discount applies "before the invoice is produced" - baked in here, at resolution
        // time, since invoicing only ever sums each entry's already-frozen ResolvedCustomerRate and never
        // re-resolves rates (see InvoiceGenerationService). Applying it again there would double-discount.
        var customerRate = rateCard.DiscountPercent is { } discount ? rateCard.Rate * (1 - discount / 100m) : rateCard.Rate;

        return new RateResolution(customerRate, cost.HourlyCost, cost.OutOfHoursCost, rateCard.Id, cost.Id, tier);
    }
}
