using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Platform;

/// <summary>
/// Login and provisioning for platform admins — the operators who work across
/// every tenant, not any one <see cref="User"/> inside one.
/// </summary>
public class PlatformAdminService(
    IFleetRentalDbContext db,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator)
{
    private const int MinimumPasswordLength = 8;

    public async Task<PlatformAuthResponse> LoginAsync(
        PlatformLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = PlatformAdmin.NormalizeEmail(request.Email);

        var admin = await db.PlatformAdmins.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

        // Same message whether the email is unknown or the password is wrong, so
        // the endpoint cannot be used to enumerate platform-admin accounts.
        if (admin is null || !passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            throw new AuthenticationFailedException("Incorrect email or password.");
        }

        if (!admin.IsActive)
        {
            throw new AuthenticationFailedException("This account has been deactivated.");
        }

        var token = tokenGenerator.Generate(admin);

        return new PlatformAuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            Admin = PlatformAdminDto.FromEntity(admin),
        };
    }

    public async Task<PlatformAdminDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var admin = await db.PlatformAdmins
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PlatformAdmin), id);

        return PlatformAdminDto.FromEntity(admin);
    }

    public async Task<IReadOnlyList<PlatformAdminDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var admins = await db.PlatformAdmins
            .AsNoTracking()
            .OrderBy(a => a.FullName)
            .ToListAsync(cancellationToken);

        return [.. admins.Select(PlatformAdminDto.FromEntity)];
    }

    /// <summary>
    /// Provisions another platform admin. Only reachable by an already-authenticated
    /// platform admin — the very first one comes from <c>DbSeeder</c>, the same way
    /// the very first tenant admin does.
    /// </summary>
    public async Task<PlatformAdminDto> CreateAsync(
        CreatePlatformAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = PlatformAdmin.NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            throw ValidationException.Single(
                nameof(request.Password),
                $"Password must be at least {MinimumPasswordLength} characters.");
        }

        if (await db.PlatformAdmins.AnyAsync(a => a.Email == email, cancellationToken))
        {
            throw ValidationException.Single(nameof(request.Email), "An account with this email already exists.");
        }

        var admin = PlatformAdmin.Create(email, passwordHasher.Hash(request.Password), request.FullName);
        db.PlatformAdmins.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        return PlatformAdminDto.FromEntity(admin);
    }
}
