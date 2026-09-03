using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>FDD: "Attachments and documents can be held against a project." Mirrors AttachmentsFunctions
/// (entry-scoped) closely, but gated by Admin-or-ProjectManager rather than entry ownership - a project has no
/// single "owner" the way an entry does, so RequireAdminOrProjectManager (the same gate Projects_Estimate uses)
/// is the right fit here instead.</summary>
public class ProjectAttachmentsFunctions(
    IProjectAttachmentRepository attachments,
    IProjectRepository projects,
    IFileStorageService fileStorage,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ProjectAttachments_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:int}/attachments")] HttpRequest req,
        int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(project) is { } forbidden) return forbidden;

        var result = await attachments.GetByProjectAsync(projectId, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("ProjectAttachments_Upload")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:int}/attachments")] HttpRequest req,
        int projectId, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(project) is { } forbidden) return forbidden;

        if (!req.HasFormContentType || req.Form.Files.Count == 0)
        {
            return new BadRequestObjectResult(new { error = "No file uploaded." });
        }

        var file = req.Form.Files[0];
        await using var stream = file.OpenReadStream();
        var storageKey = await fileStorage.SaveAsync(file.FileName, stream, ct);

        var attachment = new ProjectAttachment
        {
            ProjectId = projectId,
            FileName = file.FileName,
            StorageKey = storageKey,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = user.UserId,
        };
        await attachments.AddAsync(attachment, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult(
            $"/api/projects/{projectId}/attachments/{attachment.Id}",
            new ProjectAttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAtUtc, user.DisplayName));
    }

    [Function("ProjectAttachments_Download")]
    public async Task<IActionResult> Download(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "project-attachments/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var attachment = await attachments.GetByIdAsync(id, ct);
        if (attachment is null) return new NotFoundResult();

        var project = await projects.GetByIdAsync(attachment.ProjectId, ct);
        if (project is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(project) is { } forbidden) return forbidden;

        var stream = await fileStorage.OpenReadAsync(attachment.StorageKey, ct);
        return new FileStreamResult(stream, attachment.ContentType) { FileDownloadName = attachment.FileName };
    }

    [Function("ProjectAttachments_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "project-attachments/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var attachment = await attachments.GetByIdAsync(id, ct);
        if (attachment is null) return new NotFoundResult();

        var project = await projects.GetByIdAsync(attachment.ProjectId, ct);
        if (project is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(project) is { } forbidden) return forbidden;

        await fileStorage.DeleteAsync(attachment.StorageKey, ct);
        attachments.Remove(attachment);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    private static ProjectAttachmentDto ToDto(ProjectAttachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc, a.UploadedBy?.DisplayName ?? "");
}
