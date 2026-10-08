using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using OpenBrew.Api.Clients;
using OpenBrew.Api.Data;
using OpenBrew.Api.Middleware;
using OpenBrew.Api.Options;
using OpenBrew.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMemoryCache();
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
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT token obtained from /api/v1/auth/login."
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
