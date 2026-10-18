namespace Wanetra.Domain;

public interface IAuthRepository
{
    Task<AuthAccount?> GetAccountAsync(CancellationToken cancellationToken);
    void AddAccount(AuthAccount account);

    Task<AuthSession?> FindSessionAsync(string tokenHash, CancellationToken cancellationToken);
    void AddSession(AuthSession session);

    Task<int> DeleteExpiredSessionsAsync(DateTime now, CancellationToken cancellationToken);
    Task<int> DeleteSessionsExceptAsync(string? keepTokenHash, CancellationToken cancellationToken);
    Task<int> DeleteSessionAsync(string tokenHash, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
