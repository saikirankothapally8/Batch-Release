using System.Security.Cryptography;
using System.Text;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Services;

public class PasswordService
{
    readonly PasswordHasher<User> _hasher = new();
    public string Hash(User u, string password) => _hasher.HashPassword(u, password);
    public bool Verify(User u, string password) =>
        _hasher.VerifyHashedPassword(u, u.PasswordHash, password) != PasswordVerificationResult.Failed;

    public static void ValidatePolicy(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsDigit) || !password.Any(char.IsLetter))
            throw new DomainException("Password must be at least 8 characters and contain letters and digits.");
    }
}

/// <summary>
/// Authentication and electronic signatures. A signature requires the signer to re-enter their password at the
/// moment of signing (both signature components for the first signing in a session per Part 11 §11.200).
/// Repeated failures lock the account.
/// </summary>
public class SignatureService
{
    public const int MaxFailedAttempts = 5;
    static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    readonly AppDbContext _db;
    readonly PasswordService _passwords;
    readonly IClock _clock;
    readonly ICurrentUser _current;

    public SignatureService(AppDbContext db, PasswordService passwords, IClock clock, ICurrentUser current)
    {
        _db = db; _passwords = passwords; _clock = clock; _current = current;
    }

    public async Task<User> VerifyCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !user.IsActive)
        {
            _db.AddAudit("LoginFailed", nameof(User), username, reason: "Unknown or inactive user");
            await _db.SaveChangesAsync();
            throw new DomainException("Invalid username or password.", 401);
        }
        if (user.LockedUntil != null && user.LockedUntil > _clock.Now)
            throw new DomainException($"Account is locked until {user.LockedUntil:dd-MM-yyyy HH:mm} UTC after repeated failed attempts.", 423);

        if (!_passwords.Verify(user, password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = _clock.Now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _db.AddAudit("AccountLocked", nameof(User), user.Id.ToString(), reason: "Too many failed attempts");
            }
            _db.AddAudit("LoginFailed", nameof(User), user.Id.ToString());
            await _db.SaveChangesAsync();
            throw new DomainException("Invalid username or password.", 401);
        }
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        return user;
    }

    /// <summary>Signs as the logged-in user. Password re-entry is mandatory; signing as someone else is impossible.</summary>
    public async Task<ElectronicSignature> SignAsync(string password, string meaning, string entityType, string entityId,
        int? batchId, string signedContent)
    {
        if (_current.UserId == null) throw new DomainException("Not authenticated.", 401);
        if (string.IsNullOrWhiteSpace(meaning)) throw new DomainException("Signature meaning is required.");
        var me = await _db.Users.FirstAsync(u => u.Id == _current.UserId);
        await VerifyCredentialsAsync(me.Username, password);

        var signedAt = _clock.Now;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{entityType}|{entityId}|{me.Id}|{meaning}|{signedAt:O}|{signedContent}")));
        var sig = new ElectronicSignature
        {
            UserId = me.Id, Username = me.Username, FullName = me.FullName, Meaning = meaning, SignedAt = signedAt,
            EntityType = entityType, EntityId = entityId, BatchId = batchId, RecordHash = hash
        };
        _db.Signatures.Add(sig);
        _db.AddAudit("Sign", entityType, entityId, batchId, "Signature", null, $"{me.FullName} ({me.Username}) – {meaning}");
        return sig;
    }
}
