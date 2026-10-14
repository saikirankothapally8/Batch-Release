using BatchReleaseIQ.Api.Dtos;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/admin")]
public class AdminController : ControllerBase
{
    readonly AppDbContext _db;
    readonly PasswordService _passwords;
    readonly BatchStateService _state;
    readonly AuditScope _scope;
    readonly CurrentUserState _me;
    readonly IClock _clock;

    public AdminController(AppDbContext db, PasswordService passwords, BatchStateService state, AuditScope scope, CurrentUserState me, IClock clock)
    {
        _db = db; _passwords = passwords; _state = state; _scope = scope; _me = me; _clock = clock;
    }

    // ---------------- roles & activities

    [HttpGet("permissions"), Requires(Permissions.RoleManage)]
    public object PermissionCatalogue() => Permissions.All;

    [HttpGet("roles"), Requires(Permissions.RoleManage, Permissions.UserManage)]
    public async Task<List<RoleDto>> Roles()
    {
        var roles = await _db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync();
        var counts = await _db.UserRoles.GroupBy(u => u.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return roles.Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Select(p => p.PermissionCode).ToList(), counts.GetValueOrDefault(r.Id))).ToList();
    }

    [HttpPost("roles"), Requires(Permissions.RoleManage)]
    public async Task<object> CreateRole(RoleInput r)
    {
        Validate(r);
        if (await _db.Roles.AnyAsync(x => x.Name == r.Name)) throw new DomainException("A role with this name already exists.");
        var role = new Role { Name = r.Name.Trim(), Description = r.Description, Permissions = r.Permissions.Distinct().Select(p => new RolePermission { PermissionCode = p }).ToList() };
        _scope.Reason = "Role created";
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return new { role.Id };
    }

    [HttpPut("roles/{id:int}"), Requires(Permissions.RoleManage)]
    public async Task<object> UpdateRole(int id, RoleInput r)
    {
        Validate(r);
        var role = await _db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Role not found.", 404);
        if (role.IsSystem && !r.Permissions.Contains(Permissions.RoleManage))
            throw new DomainException("The Administrator role must keep role management, or nobody could administer roles.");
        _scope.Reason = "Role activities changed";
        role.Name = r.Name.Trim(); role.Description = r.Description;
        _db.RolePermissions.RemoveRange(role.Permissions.Where(p => !r.Permissions.Contains(p.PermissionCode)));
        foreach (var p in r.Permissions.Distinct().Where(p => role.Permissions.All(x => x.PermissionCode != p)))
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = p });
        await _db.SaveChangesAsync();
        return new { role.Id };
    }

    [HttpDelete("roles/{id:int}"), Requires(Permissions.RoleManage)]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Role not found.", 404);
        if (role.IsSystem) throw new DomainException("System roles cannot be deleted.");
        if (await _db.UserRoles.AnyAsync(u => u.RoleId == id)) throw new DomainException("Remove this role from all users before deleting it.");
        _scope.Reason = "Role deleted";
        _db.RolePermissions.RemoveRange(role.Permissions);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static void Validate(RoleInput r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Role name is required.");
        var unknown = r.Permissions.Except(Permissions.All.Select(p => p.Code)).ToList();
        if (unknown.Count > 0) throw new DomainException($"Unknown activities: {string.Join(", ", unknown)}");
    }

    // ---------------- users

    [HttpGet("users"), Requires(Permissions.UserManage)]
    public async Task<List<UserDto>> Users()
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Department).Include(u => u.UserRoles).ThenInclude(r => r.Role).OrderBy(u => u.Username).ToListAsync();
        return users.Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Email, u.DepartmentId, u.Department?.Name, u.IsActive,
            u.LockedUntil > _clock.Now, u.LastLoginAt, u.UserRoles.Select(r => r.RoleId).ToList(), u.UserRoles.Select(r => r.Role!.Name).ToList())).ToList();
    }

    [HttpPost("users"), Requires(Permissions.UserManage)]
    public async Task<object> CreateUser(UserInput r)
    {
        if (string.IsNullOrWhiteSpace(r.Username) || string.IsNullOrWhiteSpace(r.FullName)) throw new DomainException("Username and full name are required.");
        if (await _db.Users.AnyAsync(u => u.Username == r.Username)) throw new DomainException("Username already exists. User IDs are never reused.");
        PasswordService.ValidatePolicy(r.Password ?? "");
        var u = new User { Username = r.Username.Trim(), FullName = r.FullName, Email = r.Email, DepartmentId = r.DepartmentId, IsActive = r.IsActive,
            PasswordChangedAt = _clock.Now, UserRoles = r.RoleIds.Distinct().Select(id => new UserRole { RoleId = id }).ToList() };
        u.PasswordHash = _passwords.Hash(u, r.Password!);
        _scope.Reason = "User created";
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return new { u.Id };
    }

    [HttpPut("users/{id:int}"), Requires(Permissions.UserManage)]
    public async Task<object> UpdateUser(int id, UserInput r)
    {
        var u = await _db.Users.Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("User not found.", 404);
        if (u.Id == _me.UserId && !r.IsActive) throw new DomainException("You cannot deactivate your own account.");
        _scope.Reason = "User updated";
        u.FullName = r.FullName; u.Email = r.Email; u.DepartmentId = r.DepartmentId; u.IsActive = r.IsActive;
        _db.UserRoles.RemoveRange(u.UserRoles.Where(x => !r.RoleIds.Contains(x.RoleId)));
        foreach (var rid in r.RoleIds.Distinct().Where(rid => u.UserRoles.All(x => x.RoleId != rid))) u.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = rid });
        if (!string.IsNullOrEmpty(r.Password))
        {
            PasswordService.ValidatePolicy(r.Password);
            u.PasswordHash = _passwords.Hash(u, r.Password);
            u.PasswordChangedAt = _clock.Now;
            u.LockedUntil = null; u.FailedLoginCount = 0;
        }
        await _db.SaveChangesAsync();
        return new { u.Id };
    }

    // ---------------- prioritisation settings

    [HttpGet("settings"), Requires(Permissions.QueueView, Permissions.SettingsManage)]
    public async Task<PrioritySettings> Settings() => await _db.PrioritySettings.AsNoTracking().FirstAsync();

    [HttpPut("settings"), Requires(Permissions.SettingsManage)]
    public async Task<PrioritySettings> UpdateSettings(PrioritySettings s)
    {
        if (s.ReadinessBandSize < 1 || s.ReadinessBandSize > 50) throw new DomainException("Readiness band must be between 1 and 50.");
        if (s.MinStageWeight <= 0 || s.MaxStageWeight < s.MinStageWeight) throw new DomainException("Stage weights must be positive and max ≥ min.");
        var current = await _db.PrioritySettings.FirstAsync();
        _scope.Reason = "Prioritisation settings changed";
        _db.Entry(current).CurrentValues.SetValues(s);
        current.Id = 1;
        await _db.SaveChangesAsync();
        await _state.RecalculateAllAsync();
        return current;
    }

    // ---------------- audit trail

    [HttpGet("audit"), Requires(Permissions.AuditView)]
    public async Task<object> Audit([FromQuery] string? entityType, [FromQuery] string? username, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int take = 300)
    {
        var q = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(username)) q = q.Where(a => a.Username == username);
        if (from != null) { var f = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); q = q.Where(a => a.Timestamp >= f); }
        if (to != null) { var t = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); q = q.Where(a => a.Timestamp < t); }
        return await q.OrderByDescending(a => a.Id).Take(Math.Clamp(take, 1, 2000)).ToListAsync();
    }
}
