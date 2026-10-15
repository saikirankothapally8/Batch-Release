using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BatchReleaseIQ.Infrastructure.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Creates the tables (if the database has none yet – it may already exist, e.g. created by a DBA) and installs
    /// triggers that make the audit trail and e-signature tables append-only at database level, so not even a
    /// direct SQL session can rewrite history.
    /// </summary>
    public static async Task EnsureCreatedAsync(AppDbContext db)
    {
        var created = await db.Database.EnsureCreatedAsync();
        if (!created) return;
        foreach (var sql in AppendOnlyTriggers(db.Database.ProviderName ?? ""))
            await db.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// Drops this application's tables (and the audit trigger function) and creates them again, empty. Only the
    /// tables of this model are touched, so other objects in a shared database are left alone. Destroys all data,
    /// including the audit trail: used only by the explicit, confirmed command-line data commands.
    /// </summary>
    public static async Task RecreateTablesAsync(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(n => n != null).Distinct().ToList();
        // Table names come from this application's model, never from user input.
#pragma warning disable EF1002
        if (provider.Contains("Npgsql"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "DROP TABLE IF EXISTS " + string.Join(", ", tables.Select(t => $"\"{t}\"")) + " CASCADE; " +
                "DROP FUNCTION IF EXISTS brq_block_modification() CASCADE;");
        }
        else if (provider.Contains("SqlServer"))
        {
            var list = string.Join(", ", tables.Select(t => $"'{t}'"));
            await db.Database.ExecuteSqlRawAsync($"""
                DECLARE @sql nvarchar(max) = N'';
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
                  FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id = t.object_id WHERE t.name IN ({list});
                EXEC sp_executesql @sql;
                """);
            foreach (var t in tables) await db.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS [{t}]");
        }
        else throw new InvalidOperationException($"Unsupported database provider {provider}.");
#pragma warning restore EF1002

        db.ChangeTracker.Clear();
        await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
        foreach (var sql in AppendOnlyTriggers(provider)) await db.Database.ExecuteSqlRawAsync(sql);
    }

    public static IReadOnlyList<string> AppendOnlyTriggers(string provider)
    {
        if (provider.Contains("Npgsql"))
            return new[]
            {
                """
                CREATE OR REPLACE FUNCTION brq_block_modification() RETURNS trigger AS $$
                BEGIN
                  RAISE EXCEPTION 'Table % is append-only (GxP audit trail)', TG_TABLE_NAME;
                END; $$ LANGUAGE plpgsql
                """,
                """CREATE TRIGGER trg_audit_append_only BEFORE UPDATE OR DELETE ON "AuditEntries" FOR EACH ROW EXECUTE FUNCTION brq_block_modification()""",
                """CREATE TRIGGER trg_signature_append_only BEFORE UPDATE OR DELETE ON "Signatures" FOR EACH ROW EXECUTE FUNCTION brq_block_modification()""",
            };
        if (provider.Contains("SqlServer"))
            return new[] { "AuditEntries", "Signatures" }.Select(table => $"""
                CREATE TRIGGER trg_{table}_append_only ON [{table}] INSTEAD OF UPDATE, DELETE AS
                BEGIN THROW 51000, 'Table {table} is append-only (GxP audit trail)', 1; END
                """).ToList();
        return Array.Empty<string>();
    }

    /// <summary>
    /// Writes the full DDL (tables, keys, indexes, append-only triggers) for PostgreSQL and SQL Server, for DBAs who
    /// create the schema by hand. No database connection is needed.
    /// </summary>
    public static void ExportSchemaScripts(string directory)
    {
        Directory.CreateDirectory(directory);
        var targets = new (string File, string Separator, Action<DbContextOptionsBuilder<AppDbContext>> Configure)[]
        {
            ("postgresql-schema.sql", ";", o => o.UseNpgsql("Host=localhost")),
            ("sqlserver-schema.sql", "\nGO", o => o.UseSqlServer("Server=localhost")),
        };
        foreach (var (file, separator, configure) in targets)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            configure(builder);
            using var db = new AppDbContext(builder.Options, new SystemUser(), new SystemClock("UTC"), new AuditScope());
            var header = $"""
                -- Batch Release IQ – database schema ({(file.StartsWith("postgres") ? "PostgreSQL" : "SQL Server")})
                -- Run this inside the empty database you created, then start the API with Database:SeedSampleData
                -- true (demo data) or false (empty system). Generated from the EF Core model; do not edit by hand.

                """;
            var ddl = db.Database.GenerateCreateScript();
            var triggers = string.Join("\n", AppendOnlyTriggers(db.Database.ProviderName ?? "").Select(t => t.Trim() + separator + "\n"));
            File.WriteAllText(Path.Combine(directory, file), header + ddl + "\n\n-- Append-only audit trail and e-signatures\n" + triggers);
        }
    }
}
