using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api;

/// <summary>
/// Command-line data commands, run with the same configuration as the web site, e.g. from the IIS folder:
///   dotnet BatchReleaseIQ.Api.dll --remove-sample-data   (empty system: roles, departments, settings, admin)
///   dotnet BatchReleaseIQ.Api.dll --load-sample-data     (full demo data)
/// Both delete ALL data in the database, audit trail included, so they ask for the database name as confirmation
/// (or take it from --confirm &lt;name&gt; for scripts). The web site should be stopped while they run.
/// </summary>
public static class DataCommands
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        var remove = args.Contains("--remove-sample-data");
        var load = args.Contains("--load-sample-data");
        if (!remove && !load) return false;

        using var scope = app.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbName = db.Database.GetDbConnection().Database;

        Console.WriteLine();
        Console.WriteLine(remove
            ? "REMOVE SAMPLE DATA: every table of Batch Release IQ is emptied and recreated, then only roles, departments, settings and the 'admin' user are added."
            : "LOAD SAMPLE DATA: every table of Batch Release IQ is emptied and recreated, then the demo batches, users and master data are loaded.");
        Console.WriteLine($"Database: {dbName} ({db.Database.ProviderName})");
        Console.WriteLine("ALL existing data, including the audit trail and signatures, will be deleted. Stop the web site first.");

        var confirm = Value(args, "--confirm");
        if (confirm == null)
        {
            Console.Write($"Type the database name '{dbName}' to continue: ");
            confirm = Console.ReadLine();
        }
        if (!string.Equals(confirm?.Trim(), dbName, StringComparison.Ordinal))
        {
            Console.WriteLine("Not confirmed - nothing was changed.");
            Environment.ExitCode = 1;
            return true;
        }

        string? adminPassword = null;
        if (remove)
        {
            adminPassword = Value(args, "--admin-password") ?? config["Bootstrap:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                Console.Write("Password for the 'admin' user (8+ characters, letters and digits): ");
                adminPassword = Console.ReadLine();
            }
            try { PasswordService.ValidatePolicy(adminPassword ?? ""); }
            catch (DomainException e) { Console.WriteLine(e.Message + " Nothing was changed."); Environment.ExitCode = 1; return true; }
        }

        Console.WriteLine("Recreating tables...");
        await DatabaseSetup.RecreateTablesAsync(db);
        var seeder = scope.ServiceProvider.GetRequiredService<SampleData>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditScope>();
        if (load)
        {
            audit.Reason = "Sample data loaded from the command line";
            Console.WriteLine("Loading sample data (this takes a little while)...");
            await seeder.SeedAsync();
            Console.WriteLine($"Done: {await db.Batches.CountAsync()} batches, {await db.Users.CountAsync()} users. Demo password: {SampleData.DemoPassword}");
        }
        else
        {
            audit.Reason = "Sample data removed from the command line";
            await seeder.SeedEssentialsAsync(adminPassword!);
            Console.WriteLine("Done: the database is empty except roles, departments, settings and the 'admin' user.");
            Console.WriteLine("Next: sign in as 'admin', then add master data, checklist templates and users.");
        }
        return true;
    }

    static string? Value(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
