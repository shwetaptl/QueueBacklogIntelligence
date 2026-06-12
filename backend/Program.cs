using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Monitor.Query;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using QueueBacklogIntelligence.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// ── OpenAPI / Swagger config ───────────────────────────────────────────────
builder.Services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
    new OpenApiConfigurationOptions
    {
        Info = new OpenApiInfo
        {
            Version     = "1.0.0",
            Title       = "QBIS API",
            Description = "Queue Backlog Intelligence System — REST API",
        },
        OpenApiVersion              = OpenApiVersionType.V3,
        IncludeRequestingHostName   = true,
        ForceHttps                  = false,
        ForceHttp                   = false,
    });

// Read connection strings
var sbConnection = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
    ?? throw new InvalidOperationException(
        "ServiceBusConnectionString missing from local.settings.json.");

var storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString")
    ?? "UseDevelopmentStorage=true";

// Azure SDK clients — singletons
builder.Services.AddSingleton(
    new ServiceBusAdministrationClient(sbConnection));

builder.Services.AddSingleton(
    new MetricsQueryClient(new DefaultAzureCredential()));

builder.Services.AddSingleton(
    new TableServiceClient(storageConnection));

// Application services
builder.Services.AddSingleton<IRepository>(sp =>
    new Repository(
        storageConnection,
        sp.GetRequiredService<ILogger<Repository>>()));

builder.Services.AddSingleton<CollectorService>();
builder.Services.AddSingleton<AnalyzerService>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Startup tasks
await app.Services
    .GetRequiredService<IRepository>()
    .EnsureTablesExistAsync();

await app.RunAsync();