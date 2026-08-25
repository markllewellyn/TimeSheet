namespace TimeSheet.Domain.Entities;

public class Attachment
{
    public int Id { get; set; }
    public int TimesheetEntryId { get; set; }
    public TimesheetEntry? TimesheetEntry { get; set; }

    public required string FileName { get; set; }

    /// <summary>Opaque key: relative path today (IFileStorageService/LocalFileStorageService), blob name later.
    /// Callers never construct/interpret this themselves — that's what makes the Blob Storage swap a single-class change.</summary>
    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public int UploadedByUserId { get; set; }
}
