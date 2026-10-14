using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BatchReleaseIQ.Api.Security;

public class JwtOptions
{
    public string Issuer { get; set; } = "BatchReleaseIQ";
    public string Audience { get; set; } = "BatchReleaseIQ";
    public string SigningKey { get; set; } = "";
    /// <summary>Session timeout (Annex 11 §12: automatic logoff after inactivity).</summary>
    public int ExpiryMinutes { get; set; } = 60;
}

public class TokenService
{
    readonly JwtOptions _o;
    public TokenService(JwtOptions o) => _o = o;

    public (string Token, DateTime Expires) Issue(User u)
    {
        var expires = DateTime.UtcNow.AddMinutes(_o.ExpiryMinutes);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()), new Claim(JwtRegisteredClaimNames.UniqueName, u.Username) },
            expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

/// <summary>Filled per request by <see cref="CurrentUserMiddleware"/>; permissions come from the database so role edits apply immediately.</summary>
public class CurrentUserState : ICurrentUser
{
    public int? UserId { get; set; }
    public string Username { get; set; } = "anonymous";
    public string FullName { get; set; } = "";
    public int? DepartmentId { get; set; }
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
    public List<string> Roles { get; set; } = new();
    public bool Has(string permission) => Permissions.Contains(permission);
}

public class CurrentUserMiddleware
{
    readonly RequestDelegate _next;
    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, CurrentUserState state, AppDbContext db)
    {
        var sub = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(sub, out var id))
        {
            var u = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (u != null)
            {
                state.UserId = u.Id; state.Username = u.Username; state.FullName = u.FullName; state.DepartmentId = u.DepartmentId;
                state.Roles = u.UserRoles.Select(r => r.Role!.Name).ToList();
                state.Permissions = u.UserRoles.SelectMany(r => r.Role!.Permissions).Select(p => p.PermissionCode).ToHashSet();
            }
        }
        await _next(ctx);
    }
}

/// <summary>Requires the current user to hold at least one of the given permissions.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequiresAttribute : Attribute, IAuthorizationFilter
{
    readonly string[] _permissions;
    public RequiresAttribute(params string[] permissions) => _permissions = permissions;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<CurrentUserState>();
        if (user.UserId == null) { context.Result = new UnauthorizedResult(); return; }
        if (!_permissions.Any(user.Permissions.Contains))
            context.Result = new ObjectResult(new { message = $"You do not have permission for this activity ({string.Join(" / ", _permissions)})." }) { StatusCode = 403 };
    }
}

public class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DomainException d)
        {
            context.Result = new ObjectResult(new { message = d.Message }) { StatusCode = d.StatusCode };
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DbUpdateException { InnerException: not null } db && db.InnerException.Message.Contains("append-only"))
        {
            context.Result = new ObjectResult(new { message = "Audit records cannot be changed." }) { StatusCode = 409 };
            context.ExceptionHandled = true;
        }
    }
}
