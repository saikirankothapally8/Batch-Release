using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Creates the schema (if missing) and installs triggers that make the audit trail and e-signature tables
    /// append-only at database level, so not even a direct SQL session can rewrite history.
    /// </summary>
    public static async Task EnsureCreatedAsync(AppDbContext db)
    {
        var created = await db.Database.EnsureCreatedAsync();
        if (!created) return;
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("Npgsql"))
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION brq_block_modification() RETURNS trigger AS $$
                BEGIN
                  RAISE EXCEPTION 'Table % is append-only (GxP audit trail)', TG_TABLE_NAME;
                END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_audit_append_only BEFORE UPDATE OR DELETE ON "AuditEntries"
                  FOR EACH ROW EXECUTE FUNCTION brq_block_modification();
                CREATE TRIGGER trg_signature_append_only BEFORE UPDATE OR DELETE ON "Signatures"
                  FOR EACH ROW EXECUTE FUNCTION brq_block_modification();
                """);
        }
        else if (provider.Contains("SqlServer"))
        {
            foreach (var table in new[] { "AuditEntries", "Signatures" })
                await db.Database.ExecuteSqlRawAsync($"""
                    CREATE TRIGGER trg_{table}_append_only ON [{table}] INSTEAD OF UPDATE, DELETE AS
                    BEGIN THROW 51000, 'Table {table} is append-only (GxP audit trail)', 1; END
                    """);
        }
    }
}
