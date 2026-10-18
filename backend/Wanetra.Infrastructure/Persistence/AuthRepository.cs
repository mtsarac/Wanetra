using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

public sealed class AuthRepository(WanetraDbContext dbContext) : IAuthRepository
{
    public Task<AuthAccount?> GetAccountAsync(CancellationToken cancellationToken) =>
        dbContext.AuthAccounts.OrderBy(account => account.Id).FirstOrDefaultAsync(cancellationToken);

    public void AddAccount(AuthAccount account) => dbContext.AuthAccounts.Add(account);

    public Task<AuthSession?> FindSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.AuthSessions.AsNoTracking().FirstOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

    public void AddSession(AuthSession session) => dbContext.AuthSessions.Add(session);

    public Task<int> DeleteExpiredSessionsAsync(DateTime now, CancellationToken cancellationToken) =>
        dbContext.AuthSessions.Where(session => session.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteSessionsExceptAsync(string? keepTokenHash, CancellationToken cancellationToken) =>
        dbContext.AuthSessions.Where(session => session.TokenHash != keepTokenHash).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteSessionAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.AuthSessions.Where(session => session.TokenHash == tokenHash).ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
