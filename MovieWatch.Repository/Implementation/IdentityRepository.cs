using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;

namespace MovieWatch.Repository.Implementation;

public sealed class IdentityRepository(
    ApplicationDbContext context,
    UserManager<IdentityUser> users,
    IRepository<Viewer> viewers) : IIdentityRepository
{
    public async Task<ViewerDto> RegisterAsync(RegisterViewerDto registration, string role, CancellationToken cancellationToken = default)
    {
        var account = new IdentityUser { UserName = registration.Email.Trim(), Email = registration.Email.Trim() };
        var viewer = new Viewer(account.Id, registration.DisplayName);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            CheckResult(await users.CreateAsync(account, registration.Password));
            await viewers.InsertAsync(viewer, cancellationToken);
            CheckResult(await users.AddToRoleAsync(account, role));
            await transaction.CommitAsync(cancellationToken);
            return new ViewerDto(viewer.Id, account.Email, viewer.DisplayName);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 }
            && exception.InnerException.Message.Contains("AspNetUsers.Normalized", StringComparison.Ordinal))
        {
            throw new OperationException(FailureKind.Conflict, "An account with this email already exists.");
        }
    }

    public async Task<AccountDto?> CheckCredentialsAsync(LoginDto login, CancellationToken cancellationToken = default)
    {
        var account = await users.FindByEmailAsync(login.Email.Trim());
        if (account is null || !await users.CheckPasswordAsync(account, login.Password)
            || await viewers.GetAsync(v => v.AccountId == account.Id, cancellationToken) is null)
            return null;
        return new AccountDto(account.Id, (await users.GetRolesAsync(account)).ToArray());
    }

    public async Task<ViewerDto?> GetProfileAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var account = await users.FindByIdAsync(accountId);
        var viewer = await viewers.GetAsync(v => v.AccountId == accountId, cancellationToken);
        return account is null || viewer is null ? null : new ViewerDto(viewer.Id, account.Email!, viewer.DisplayName);
    }

    public async Task EnsureAdministratorAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default)
    {
        var existing = await users.FindByEmailAsync(registration.Email.Trim());
        if (existing is null)
        {
            await RegisterAsync(registration, AccountRoles.Administrator, cancellationToken);
            return;
        }
        // Bootstrap never elevates a public account that happened to claim the configured email.
        if (!await users.IsInRoleAsync(existing, AccountRoles.Administrator)
            || await viewers.GetAsync(v => v.AccountId == existing.Id, cancellationToken) is null)
            throw new OperationException(FailureKind.Conflict, "Administrator bootstrap requires an unused email or an existing administrator profile.");
    }

    public async Task<List<ViewerDto>> ListProfilesAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var profiles = await context.Viewers.AsNoTracking().OrderBy(v => v.Id).Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
        var accounts = await context.Users.AsNoTracking().Where(a => profiles.Select(v => v.AccountId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        return profiles.Select(v => new ViewerDto(v.Id, accounts[v.AccountId].Email!, v.DisplayName)).ToList();
    }

    public async Task<ViewerDto?> GetProfileByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        var viewer = await context.Viewers.AsNoTracking().SingleOrDefaultAsync(v => v.Id == viewerId, cancellationToken);
        return viewer is null ? null : await GetProfileAsync(viewer.AccountId, cancellationToken);
    }

    public async Task<ViewerDto> UpdateProfileAsync(Guid viewerId, UpdateViewerDto update, CancellationToken cancellationToken = default)
    {
        var viewer = await context.Viewers.SingleOrDefaultAsync(v => v.Id == viewerId, cancellationToken)
            ?? throw new OperationException(FailureKind.Validation, "Viewer was not found.");
        var account = await users.FindByIdAsync(viewer.AccountId)
            ?? throw new OperationException(FailureKind.Validation, "Account was not found.");
        viewer.Rename(update.DisplayName);
        if (string.IsNullOrWhiteSpace(update.Email))
            throw new OperationException(FailureKind.Validation, "Email is required.");
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        CheckResult(await users.SetEmailAsync(account, update.Email.Trim()));
        CheckResult(await users.SetUserNameAsync(account, update.Email.Trim()));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ViewerDto(viewer.Id, account.Email!, viewer.DisplayName);
    }

    public async Task DeleteProfileAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        var viewer = await context.Viewers.SingleOrDefaultAsync(v => v.Id == viewerId, cancellationToken)
            ?? throw new OperationException(FailureKind.Validation, "Viewer was not found.");
        if (await context.Groups.AnyAsync(g => g.OwnerViewerId == viewerId, cancellationToken))
            throw new OperationException(FailureKind.Conflict, "Transfer or delete owned groups before deleting this viewer.");
        var account = await users.FindByIdAsync(viewer.AccountId)
            ?? throw new OperationException(FailureKind.Validation, "Account was not found.");
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        CheckResult(await users.DeleteAsync(account));
        await transaction.CommitAsync(cancellationToken);
    }

    private static void CheckResult(IdentityResult result)
    {
        if (result.Succeeded)
            return;
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw new OperationException(FailureKind.Conflict, "An account with this email already exists.");
        throw new OperationException(FailureKind.Validation,
            string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
