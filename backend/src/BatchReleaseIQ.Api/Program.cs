using System.Text;
using System.Text.Json.Serialization;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
    if (config.GetValue("Database:SeedSampleData", true))
    {
        scope.ServiceProvider.GetRequiredService<AuditScope>().Reason = "Initial sample data load";
        await scope.ServiceProvider.GetRequiredService<SampleData>().SeedAsync();
    }
}

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
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
