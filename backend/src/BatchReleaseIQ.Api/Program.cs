using System.Text;
using System.Text.Json.Serialization;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// `dotnet run -- --export-schema <folder>` writes the SQL scripts for DBAs and exits.
var exportIndex = Array.IndexOf(args, "--export-schema");
if (exportIndex >= 0)
{
    var folder = args.Length > exportIndex + 1 ? args[exportIndex + 1] : "database";
    DatabaseSetup.ExportSchemaScripts(folder);
    Console.WriteLine($"Schema scripts written to {Path.GetFullPath(folder)}");
    return;
}

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- database: PostgreSQL (cloud / Linux on-prem) or SQL Server (Windows on-prem), chosen by configuration
var provider = config["Database:Provider"] ?? "PostgreSQL";
var connection = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(connection);
    else o.UseNpgsql(connection);
});

builder.Services.AddSingleton<IClock>(new SystemClock(config["Site:TimeZone"] ?? "Asia/Kolkata"));
builder.Services.AddScoped<CurrentUserState>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserState>());
builder.Services.AddScoped<AuditScope>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<SignatureService>();
builder.Services.AddScoped<BatchStateService>();
builder.Services.AddScoped<BatchService>();
builder.Services.AddScoped<InsightService>();
builder.Services.AddScoped(sp => new SampleData(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<PasswordService>(),
    sp.GetRequiredService<IClock>(), config.GetValue("Database:SampleBatchCount", 250)));

// ---- authentication
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, ClockSkew = TimeSpan.FromMinutes(1),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
    };
});
builder.Services.AddAuthorization();

builder.Services.AddControllers(o => o.Filters.Add<DomainExceptionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" })
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseSetup.EnsureCreatedAsync(db);
    var seeder = scope.ServiceProvider.GetRequiredService<SampleData>();
    if (config.GetValue("Database:SeedSampleData", true))
    {
        scope.ServiceProvider.GetRequiredService<AuditScope>().Reason = "Initial sample data load";
        await seeder.SeedAsync();
    }
    else
    {
        // Real installation: an empty database gets roles, departments, settings and the 'admin' account.
        scope.ServiceProvider.GetRequiredService<AuditScope>().Reason = "Initial system setup";
        await seeder.SeedEssentialsAsync(config["Bootstrap:AdminPassword"] ?? SampleData.DemoPassword);
    }
}

// IIS virtual directories set the path base automatically; behind a reverse proxy, set PathBase (e.g. /batchrelease).
if (!string.IsNullOrWhiteSpace(config["PathBase"])) app.UsePathBase(config["PathBase"]);
app.UseRouting(); // explicit, so routes are matched after the path base is removed
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// The Angular app is served from wwwroot once built (`npm run publish:api`). Until then, explain instead of a bare 404.
var indexFile = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
if (File.Exists(indexFile)) app.MapFallbackToFile("index.html");
else app.MapFallback(() => Results.Content(FrontendNotBuiltPage, "text/html"));
app.Run();

public partial class Program
{
    const string FrontendNotBuiltPage = """
        <!doctype html><html><head><meta charset="utf-8"><title>Batch Release IQ - API running</title>
        <style>body{font-family:system-ui,sans-serif;max-width:720px;margin:60px auto;padding:0 16px;line-height:1.5;color:#1a1a19}
        code,pre{background:#f0efec;border-radius:4px;padding:2px 6px}pre{padding:12px;overflow:auto}</style></head><body>
        <h1>Batch Release IQ API is running</h1>
        <p>The database is connected and the API answers at <code>/api</code>. The web application has not been built into this API yet.</p>
        <h3>Option 1 - serve the app from this API</h3>
        <pre>cd frontend
        npm install
        npm run publish:api</pre>
        <p>Then restart the API and refresh this page.</p>
        <h3>Option 2 - run the Angular dev server</h3>
        <pre>cd frontend
        npm install
        npm start</pre>
        <p>Open <a href="http://localhost:4200">http://localhost:4200</a>. It calls the API through <code>frontend/proxy.conf.json</code>
        (default <code>http://localhost:5080</code>; change it if Visual Studio runs the API on another port).</p>
        </body></html>
        """;
}
