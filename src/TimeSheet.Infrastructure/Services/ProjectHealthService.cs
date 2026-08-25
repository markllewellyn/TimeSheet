using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ProjectHealthService(
    IProjectHealthAssessmentRepository assessments,
    IProjectRepository projects,
    IProjectHealthAssessor assessor,
    INotificationService notificationService,
    IUnitOfWork uow) : IProjectHealthService
{
    public async Task<ProjectHealthAssessment?> GetLatestAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project?.LatestHealthAssessmentId is null) return null;
        return await assessments.GetByIdAsync(project.LatestHealthAssessmentId.Value, ct);
    }

    public async Task<IReadOnlyList<ProjectHealthAssessment>> GetHistoryAsync(int projectId, CancellationToken ct) =>
        await assessments.GetHistoryAsync(projectId, ct);

    public async Task<IReadOnlyList<ProjectHealthAssessment>> GetDashboardAsync(CancellationToken ct)
    {
        var activeProjects = await projects.GetAllActiveAsync(ct);
        var rows = new List<ProjectHealthAssessment>();

        foreach (var project in activeProjects.Where(p => p.LatestHealthAssessmentId is not null))
        {
            var assessment = await assessments.GetByIdAsync(project.LatestHealthAssessmentId!.Value, ct);
            if (assessment is not null) rows.Add(assessment);
        }

        return rows.OrderByDescending(a => Severity(a.Status)).ThenByDescending(a => a.AssessedAtUtc).ToList();
    }

    public async Task<ProjectHealthAssessment> ReassessAsync(int projectId, CancellationToken ct)
    {
        var prior = await GetLatestAsync(projectId, ct);
        var assessment = await assessor.AssessAsync(projectId, ct);

        await assessments.AddAsync(assessment, ct);
        await uow.SaveChangesAsync(ct); // populates assessment.Id

        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is not null)
        {
            project.LatestHealthAssessmentId = assessment.Id;
            projects.Update(project);
        }

        // Notify-on-worsening only, to avoid alert fatigue - a nightly run that stays OnTrack (or improves)
        // raises nothing.
        if (Severity(assessment.Status) > Severity(prior?.Status))
        {
            await notificationService.RaiseToAdminsAsync(
                NotificationType.ProjectHealthDeclined,
                $"Project {project?.Name} health has declined to {assessment.Status}: {assessment.Summary}",
                NotificationChannel.Both, relatedProjectId: projectId, ct: ct);
            assessment.NotificationSent = true;
        }

        await uow.SaveChangesAsync(ct);
        return assessment;
    }

    private static int Severity(ProjectHealthStatus? status) => status switch
    {
        ProjectHealthStatus.Behind => 2,
        ProjectHealthStatus.AtRisk => 1,
        ProjectHealthStatus.OnTrack => 0,
        null => -1,
        _ => 0,
    };
}
