using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FleetRental.Infrastructure.Security;

public class JwtTokenGenerator(IOptions<JwtOptions> options) : ITokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public AuthToken Generate(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),

            // Role drives the [Authorize(Roles = ...)] policies on admin endpoints.
            new(ClaimTypes.Role, user.Role.ToString()),

            // The tenant travels inside the signed token so it cannot be altered
            // by the caller. TenantResolutionMiddleware prefers this over any
            // header precisely because this one is tamper-evident.
            new("tenant_id", user.TenantId.ToString()),
        };

        return Sign(claims);
    }

    public AuthToken Generate(PlatformAdmin admin)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email),
            new(JwtRegisteredClaimNames.Name, admin.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.Role, PlatformRoles.PlatformAdmin),

            // Deliberately no "tenant_id" claim — a platform admin belongs to no
            // tenant, so TenantResolutionMiddleware resolves none for this token,
            // and every platform endpoint bypasses isolation explicitly per call.
        };

        return Sign(claims);
    }

    private AuthToken Sign(List<Claim> claims)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
