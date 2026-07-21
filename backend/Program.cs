using System.Text;
using ForumApi.Data;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(opt =>
{
    opt.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // images as base64 JSON
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BS Forum API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var contentRoot = builder.Environment.ContentRootPath;
var appData = Path.Combine(contentRoot, "App_Data");
Directory.CreateDirectory(appData);
var connStr = builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/forum.db";
if (connStr.Contains("App_Data", StringComparison.OrdinalIgnoreCase)
    && !Path.IsPathRooted(connStr.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim()))
{
    var file = connStr.Split("Data Source=", StringSplitOptions.None).Last().Trim();
    var fullDb = Path.GetFullPath(Path.Combine(contentRoot, file));
    Directory.CreateDirectory(Path.GetDirectoryName(fullDb)!);
    connStr = $"Data Source={fullDb}";
}
// Shared cache + busy timeout reduces SQLite lock failures under concurrent requests / IIS recycle.
if (!connStr.Contains("Cache=", StringComparison.OrdinalIgnoreCase))
    connStr += ";Cache=Shared";
if (!connStr.Contains("Mode=", StringComparison.OrdinalIgnoreCase))
    connStr += ";Mode=ReadWriteCreate";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));

var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    Console.Error.WriteLine("FATAL: JWT_SECRET environment variable is not set or is too short (min 32 chars). Set JWT_SECRET and restart.");
    Environment.Exit(1);
    return;
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ForumApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ForumApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://127.0.0.1:5173" };
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("Frontend", p =>
    {
        if (corsOrigins.Length == 0)
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddSingleton<CaptchaService>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<LevelService>();
builder.Services.AddScoped<RewardService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ForumQueryService>();
builder.Services.AddScoped<ThreadService>();
builder.Services.AddScoped<HotService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<LotteryService>();
builder.Services.AddScoped<CommunityService>();
builder.Services.AddScoped<RetentionService>();
builder.Services.AddScoped<RechargeService>();
builder.Services.AddScoped<ImageMigrationService>();
builder.Services.AddScoped<SiteSettingsService>();
builder.Services.AddScoped<MessageService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        // Fail fast with a clear message if the process cannot write SQLite files (common on IIS/Linux deploys)
        var dbPathMatch = System.Text.RegularExpressions.Regex.Match(connStr, @"Data Source=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (dbPathMatch.Success)
        {
            var dbFile = dbPathMatch.Groups[1].Value.Trim().Trim('"');
            var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbFile)) ?? appData;
            Directory.CreateDirectory(dbDir);
            var probe = Path.Combine(dbDir, ".write_probe");
            await File.WriteAllTextAsync(probe, DateTime.UtcNow.ToString("o"));
            File.Delete(probe);
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
        await DbSeeder.SeedAsync(db);
        var settings = scope.ServiceProvider.GetRequiredService<SiteSettingsService>();
        await settings.EnsureDefaultsAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "数据库初始化失败，站点仍会启动；请检查 App_Data 写权限与 forum.db（SQLite Error 8 = 目录只读）");
    }
}

// Surface real errors as { message } so the SPA alert is useful (not just axios status text)
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var feat = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feat?.Error;
        var msg = ex?.GetBaseException().Message ?? "服务器内部错误";
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new ForumApi.Dtos.ApiMessage(msg));
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiException");
        logger.LogError(ex, "Unhandled API exception {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        if (ctx.Response.HasStarted) throw;
        ctx.Response.Clear();
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var msg = ex.InnerException?.Message ?? ex.Message;
        await ctx.Response.WriteAsJsonAsync(new ForumApi.Dtos.ApiMessage(msg));
    }
});

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads"));

app.UseCors("Frontend");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var indexHtml = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
if (File.Exists(indexHtml))
    app.MapFallbackToFile("index.html");

app.Run();
