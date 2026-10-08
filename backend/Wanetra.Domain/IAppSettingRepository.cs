namespace Wanetra.Domain;

public interface IAppSettingRepository
{
    Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken);
    Task SaveAsync(IReadOnlyDictionary<string, string?> changes, DateTime updatedAt, CancellationToken cancellationToken);
}
