using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OpenBrew.Api.Clients;
using OpenBrew.Api.Data;
using OpenBrew.Api.Middleware;
using OpenBrew.Api.Options;
using OpenBrew.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMemoryCache();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
    options.IncludeSubDomains = true;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredCors", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddOptions<OpenBreweryOptions>()
    .Bind(builder.Configuration.GetSection("OpenBrewery"))
    .Validate(options => options.CacheDurationMinutes > 0, "CacheDurationMinutes must be greater than zero.")
    .Validate(options => options.PerPage is > 0 and <= 200, "PerPage must be between 1 and 200.")
    .Validate(options => options.MaxPagesToFetch > 0, "MaxPagesToFetch must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Audience is required.")
    .Validate(options => options.SigningKey.Length >= 32, "SigningKey must be at least 32 characters.")
    .Validate(options => options.ExpiryMinutes > 0, "ExpiryMinutes must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<DemoUserOptions>()
    .Bind(builder.Configuration.GetSection("DemoUser"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Username is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "Password is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("BreweryApi", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value ??
            httpContext.Connection.RemoteIpAddress?.ToString() ??
            "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Too Many Requests",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Rate limit exceeded. Try again later."
            },
            cancellationToken);
    };
});

var connectionString = builder.Configuration.GetConnectionString("OpenBrewery")
    ?? "Data Source=openbrewery.db";
builder.Services.AddDbContext<OpenBreweryDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<BreweryDatabaseInitializer>();
builder.Services.AddSingleton<IBreweryMemoryCache, BreweryMemoryCache>();
builder.Services.AddScoped<IBreweryCacheStore, SqliteBreweryStore>();
builder.Services.AddScoped<IBreweryRefreshService, BreweryRefreshService>();
builder.Services.AddScoped<IBreweryService, BreweryService>();
builder.Services.AddHttpClient<IOpenBreweryDbClient, OpenBreweryDbClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenBreweryOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<BreweryRefreshBackgroundService>();
}

builder.Services.AddControllers();
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenBrew API",
        Version = "v1",
        Description = "OpenBrew brewery catalog API"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by the JWT token obtained from /api/v1/auth/login."
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<BreweryDatabaseInitializer>();
    await initializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }

        await next();
    });

    app.UseSwagger(c =>
{
        c.SerializeAsV2 = true;
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenBrew API v1");
});
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("ConfiguredCors");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program;
