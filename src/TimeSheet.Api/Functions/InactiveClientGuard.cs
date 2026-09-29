using Microsoft.AspNetCore.Mvc;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Api.Functions;

/// <summary>New time and expense entries can't be logged against a deactivated client (user's decision,
/// 2026-09-29): deactivating a client is how an Admin says "no more work here". Only the paths that create a NEW
/// entry call this - Create and Duplicate - never Update/Delete, so existing entries on an inactive client can
/// still be corrected, approved and invoiced. The Add Entry/Add Expense pickers also hide such projects
/// (ProjectDto.ClientIsActive), but this is the actual rule.</summary>
public static class InactiveClientGuard
{
    /// <summary>Needs the project's Client loaded (IProjectRepository.GetByIdAsync includes it).</summary>
    public static IActionResult? RejectIfClientInactive(Project project) =>
        project.Client is { IsActive: false } client
            ? new BadRequestObjectResult(new
            {
                error = $"{client.Name} is inactive, so new entries can't be logged against it. An Admin can reactivate the client under Admin → Clients.",
            })
            : null;
}
