using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>
/// A person's effective-dated internal hourly cost (see StaffCost.cs) - Admin-only throughout, since these
/// reveal internal margin. A "change" is always a new dated row (POST) - there is no update endpoint.
/// </summary>
public class StaffCostsFunctions(
    IStaffCostRepository staffCosts,
    IUserRepository users,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("StaffCosts_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "staff-costs")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        if (!req.Query.TryGetValue("staffId", out var v) || !int.TryParse(v, out var staffId))
        {
            return new BadRequestObjectResult(new { error = "staffId query parameter is required." });
        }

        var staff = await users.GetByIdAsync(staffId, ct);
        var result = await staffCosts.GetByStaffAsync(staffId, ct);
        return new OkObjectResult(result.Select(sc => ToDto(sc, staff?.DisplayName ?? "")));
    }

    [Function("StaffCosts_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "staff-costs")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateStaffCostRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var staff = await users.GetByIdAsync(body.StaffId, ct);
        if (staff is null) return new NotFoundObjectResult(new { error = "Staff member not found." });

        if (body.HourlyCost < 0 || body.OutOfHoursCost < 0)
        {
            return new BadRequestObjectResult(new { error = "Costs cannot be negative." });
        }

        var existing = await staffCosts.GetByStaffAsync(body.StaffId, ct);
        if (existing.Any(sc => sc.EffectiveFrom == body.EffectiveFrom))
        {
            return new ConflictObjectResult(new { error = $"{staff.DisplayName} already has a cost effective from {body.EffectiveFrom:yyyy-MM-dd}." });
        }

        var cost = new StaffCost
        {
            StaffId = body.StaffId,
            HourlyCost = body.HourlyCost,
            OutOfHoursCost = body.OutOfHoursCost,
            EffectiveFrom = body.EffectiveFrom,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await staffCosts.AddAsync(cost, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/staff-costs/{cost.Id}", ToDto(cost, staff.DisplayName));
    }

    private static StaffCostDto ToDto(StaffCost sc, string staffName) => new(
        sc.Id, sc.StaffId, staffName, sc.HourlyCost, sc.OutOfHoursCost, sc.EffectiveFrom, sc.CreatedUtc);
}
