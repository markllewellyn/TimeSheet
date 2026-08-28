using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IAppSettingsRepository
{
    /// <summary>The single settings row (Id = 1) - seeded via migration, so this always resolves; there is no
    /// create path.</summary>
    Task<AppSettings> GetAsync(CancellationToken ct);
    void Update(AppSettings settings);
}
