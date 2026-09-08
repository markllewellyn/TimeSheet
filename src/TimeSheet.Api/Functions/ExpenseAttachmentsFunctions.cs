using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>FDD: "Staff members must be able to put lines regarding expenses and add relevant attachments."
/// Mirrors AttachmentsFunctions (the timesheet-entry equivalent) exactly - same ownership check, same
/// IFileStorageService calls, no impersonation support (matches ExpenseEntriesFunctions' own current lack of
/// impersonation support).</summary>
public class ExpenseAttachmentsFunctions(
    IExpenseAttachmentRepository attachments,
    IExpenseEntryRepository expenses,
    IFileStorageService fileStorage,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ExpenseAttachments_Upload")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense-entries/{expenseEntryId:int}/attachments")] HttpRequest req,
        int expenseEntryId, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await expenses.GetByIdAsync(expenseEntryId, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        if (!req.HasFormContentType || req.Form.Files.Count == 0)
        {
            return new BadRequestObjectResult(new { error = "No file uploaded." });
        }

        var file = req.Form.Files[0];
        await using var stream = file.OpenReadStream();
        var storageKey = await fileStorage.SaveAsync(file.FileName, stream, ct);

        var attachment = new ExpenseAttachment
        {
            ExpenseEntryId = expenseEntryId,
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
            $"/api/expense-entries/{expenseEntryId}/attachments/{attachment.Id}",
            new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAtUtc));
    }

    [Function("ExpenseAttachments_Download")]
    public async Task<IActionResult> Download(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense-attachments/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var attachment = await attachments.GetByIdAsync(id, ct);
        if (attachment is null) return new NotFoundResult();

        var entry = await expenses.GetByIdAsync(attachment.ExpenseEntryId, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        var stream = await fileStorage.OpenReadAsync(attachment.StorageKey, ct);
        return new FileStreamResult(stream, attachment.ContentType) { FileDownloadName = attachment.FileName };
    }

    [Function("ExpenseAttachments_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "expense-attachments/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var attachment = await attachments.GetByIdAsync(id, ct);
        if (attachment is null) return new NotFoundResult();

        var entry = await expenses.GetByIdAsync(attachment.ExpenseEntryId, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        await fileStorage.DeleteAsync(attachment.StorageKey, ct);
        attachments.Remove(attachment);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }
}
