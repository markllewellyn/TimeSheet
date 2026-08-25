namespace TimeSheet.Domain.Entities;

/// <summary>Join entity: which internal Users act as account manager(s) for a Client. Distinct from the client-side PrimaryContact* fields on Client.</summary>
public class ClientAccountManager
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset AssignedUtc { get; set; }
}
