using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using OpenTelemetry;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Validates the bearer token's audience against the API app registration (AzureAd config section below).
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
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
