using BatchReleaseIQ.Api.Dtos;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    readonly AppDbContext _db;
    readonly SignatureService _signatures;
    readonly TokenService _tokens;
    readonly PasswordService _passwords;
    readonly CurrentUserState _me;
    readonly IClock _clock;

    public AuthController(AppDbContext db, SignatureService signatures, TokenService tokens, PasswordService passwords, CurrentUserState me, IClock clock)
    {
        _db = db; _signatures = signatures; _tokens = tokens; _passwords = passwords; _me = me; _clock = clock;
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<LoginResponse> Login(LoginRequest r)
    {
        var user = await _signatures.VerifyCredentialsAsync(r.Username.Trim(), r.Password);
        user.LastLoginAt = _clock.Now;
        _db.AddAudit("Login", "User", user.Id.ToString());
        await _db.SaveChangesAsync();
        var (token, expires) = _tokens.Issue(user);
        return new LoginResponse(token, expires, await MeAsync(user.Id));
    }

    [HttpGet("me"), Authorize]
    public Task<MeDto> Me() => MeAsync(_me.UserId!.Value);

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest r)
    {
        var user = await _signatures.VerifyCredentialsAsync(_me.Username, r.CurrentPassword);
        PasswordService.ValidatePolicy(r.NewPassword);
        user.PasswordHash = _passwords.Hash(user, r.NewPassword);
        user.PasswordChangedAt = _clock.Now;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    async Task<MeDto> MeAsync(int id)
    {
        var u = await _db.Users.AsNoTracking().Include(x => x.Department).Include(x => x.UserRoles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
            .FirstAsync(x => x.Id == id);
        return new MeDto(u.Id, u.Username, u.FullName, u.DepartmentId, u.Department?.Name,
            u.UserRoles.Select(r => r.Role!.Name).ToList(),
            u.UserRoles.SelectMany(r => r.Role!.Permissions).Select(p => p.PermissionCode).Distinct().ToList());
    }
}
