using System.Text;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Identity.Web;
using OpenTelemetry;
using QuestPDF.Infrastructure;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure;

// Community license - free under an annual gross revenue threshold (~USD 1M); confirm current terms against
// SVG IT's actual revenue before shipping (see the plan's Invoicing section).
QuestPDF.Settings.License = LicenseType.Community;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Validates the bearer token's audience against the API app registration (AzureAd config section below).
// A second scheme, "LocalBearer", validates self-issued tokens for local (username/password) accounts -
// CurrentUserMiddleware tries the Entra scheme first, then falls back to this one. This is what lets a local
// account sign in even if Entra SSO is unreachable/misconfigured (see LocalAuthService/AuthFunctions).
var localAuthSigningKey = builder.Configuration["LocalAuth:JwtSigningKey"];
if (string.IsNullOrWhiteSpace(localAuthSigningKey))
{
    // Local dev resilience: some launch paths (e.g. Visual Studio's F5 debugger) don't always surface
    // local.settings.json values the same way `func start` does. Rather than refuse to start, generate an
    // ephemeral key for this process instead - local-account sign-in still works, tokens just won't survive
    // a restart until a real value is set in local.settings.json. Written back into Configuration so
    // LocalAuthService (which reads the same key at token-issuance time) uses this identical value.
    localAuthSigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    builder.Configuration["LocalAuth:JwtSigningKey"] = localAuthSigningKey;
    Console.WriteLine(
        "WARNING: LocalAuth:JwtSigningKey was not found in configuration - generated a temporary key for " +
        "this run. Set a real value in local.settings.json so local-account sessions survive a restart.");
}

var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
authBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
authBuilder.AddJwtBearer(LocalAuthConstants.SchemeName, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = LocalAuthConstants.Issuer,
        ValidateAudience = true,
        ValidAudience = LocalAuthConstants.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(localAuthSigningKey)),
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

builder.Services.AddInfrastructure(builder.Configuration);

// Functions isolated-worker middleware is its own message pipeline, not a conventional ASP.NET Core
// IApplicationBuilder one - there is no app.UseAuthentication()/UseCors() here. CurrentUserMiddleware invokes
// authentication manually per-request against context.GetHttpContext(). CORS for local dev is handled by the
// Core Tools "Host:CORS" setting in local.settings.json (see local.settings.json.example) plus the Angular
// proxy.conf.json making local requests same-origin; deployed environments configure CORS at the Function App
// resource level.
builder.UseMiddleware<CurrentUserMiddleware>();

builder.Build().Run();
