namespace TimeSheet.Domain.Entities;

/// <summary>FDD: "Attachments and documents can be held against a project." A separate entity/table from
/// Attachment (entry-scoped) rather than a nullable-FK polymorphic row on that entity, matching this codebase's
/// existing preference for one table per concept - see Attachment.cs for the entry-scoped equivalent, which
/// this mirrors field-for-field except for the added UploadedBy nav (useful here since a project's documents
/// are seen by potentially several people - Admin and PM alike - not just the one owner an entry has).</summary>
public class ProjectAttachment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public required string FileName { get; set; }

    /// <summary>Opaque key: relative path today (IFileStorageService/LocalFileStorageService), blob name later.
    /// Callers never construct/interpret this themselves - that's what makes the Blob Storage swap a
    /// single-class change.</summary>
    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
}
