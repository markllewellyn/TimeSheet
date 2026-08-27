using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class UserWorkloadService(IStaffProjectRepository assignments) : IUserWorkloadService
{
    public async Task<EstimatedWeeklyWorkload> GetEstimatedHoursThisWeekAsync(int userId, DateOnly weekStart, CancellationToken ct)
    {
        var weekEnd = weekStart.AddDays(6);
        var active = await assignments.GetActiveForWeekAsync(userId, weekStart, weekEnd, ct);

        var byProject = new List<ProjectAllocationDto>();
        var missing = new List<int>();
        decimal total = 0;

        foreach (var a in active)
        {
            if (a.AllocatedHoursPerWeek is null)
            {
                missing.Add(a.Id);
                continue;
            }

            total += a.AllocatedHoursPerWeek.Value;
            byProject.Add(new ProjectAllocationDto(
                a.ProjectId, a.Project!.Name, a.Project.Client!.Name, a.AllocatedHoursPerWeek.Value));
        }

        return new EstimatedWeeklyWorkload(total, byProject, missing);
    }
}
