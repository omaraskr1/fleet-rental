using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Auth;

/// <summary>
/// Signup and login for both roles (feature 5). The client app and admin panel use
/// separate login screens but the same endpoint — the returned role tells the
/// frontend which shell to render.
/// </summary>
public class AuthService(
    IFleetRentalDbContext db,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator)
{
    private const int MinimumPasswordLength = 8;

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        var email = User.NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw ValidationException.Single(nameof(request.Email), "An account with this email already exists.");
        }

        // Public signup only ever creates clients. Admins are provisioned by
        // seeding or by another admin, never by anyone hitting this endpoint.
        var user = User.RegisterClient(
            email,
            passwordHasher.Hash(request.Password),
            request.FullName,
            request.PhoneNumber);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = User.NormalizeEmail(request.Email);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same message whether the email is unknown or the password is wrong, so the
        // endpoint cannot be used to enumerate which addresses have accounts.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Incorrect email or password.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("This account has been deactivated.");
        }

        return BuildResponse(user);
    }

    /// <summary>
    /// Login for the admin panel. Rejects client accounts outright rather than
    /// issuing a token the panel would then have to refuse to use.
    /// </summary>
    public async Task<AuthResponse> AdminLoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await LoginAsync(request, cancellationToken);

        if (response.User.Role != nameof(UserRole.Admin))
        {
            throw new ForbiddenException("This login is for fleet administrators only.");
        }

        return response;
    }

    public async Task<UserDto> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        return UserDto.FromEntity(user);
    }

    /// <summary>Stores the push token Capacitor hands the app after permission is granted.</summary>
    public async Task RegisterDeviceAsync(
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .Include(u => u.DeviceTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.RegisterDeviceToken(request.Token, request.Platform, request.DeviceId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterDeviceAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .Include(u => u.DeviceTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.RemoveDeviceToken(deviceId);
        await db.SaveChangesAsync(cancellationToken);
    }

    private AuthResponse BuildResponse(User user)
    {
        var token = tokenGenerator.Generate(user);

        return new AuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            User = UserDto.FromEntity(user),
        };
    }

    private static void Validate(SignUpRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            errors[nameof(request.Email)] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            errors[nameof(request.Password)] =
                [$"Password must be at least {MinimumPasswordLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["Full name is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
