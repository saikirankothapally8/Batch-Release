namespace BatchReleaseIQ.Infrastructure.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
    /// <summary>Business date in the site's time zone.</summary>
    DateOnly Today { get; }
}

public class SystemClock : IClock
{
    readonly TimeZoneInfo _zone;
    public SystemClock(string timeZoneId)
    {
        _zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var z) ? z : TimeZoneInfo.Utc;
    }
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zone).Date);
}

public interface ICurrentUser
{
    int? UserId { get; }
    string Username { get; }
    IReadOnlySet<string> Permissions { get; }
    int? DepartmentId { get; }
    bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>Per-request audit context: the "why" recorded against every change saved in this request.</summary>
public class AuditScope
{
    public string? Reason { get; set; }
}

public class SystemUser : ICurrentUser
{
    public int? UserId => null;
    public string Username => "system";
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    public int? DepartmentId => null;
}

public class DomainException : Exception
{
    public int StatusCode { get; }
    public DomainException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}
