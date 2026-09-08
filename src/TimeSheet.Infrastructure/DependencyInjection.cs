using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.ExternalServices;
using TimeSheet.Infrastructure.Pdf;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;

namespace TimeSheet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TimesheetDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:TimesheetDb configuration.");

        services.AddDbContext<TimesheetDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TimesheetDbContext>());

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IStaffCostRepository, StaffCostRepository>();
        services.AddScoped<IRateCardRepository, RateCardRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStaffProjectRepository, StaffProjectRepository>();
        services.AddScoped<IEntryTypeRepository, EntryTypeRepository>();
        services.AddScoped<ITimesheetEntryRepository, TimesheetEntryRepository>();
        services.AddScoped<IExpenseEntryRepository, ExpenseEntryRepository>();
        services.AddScoped<IExpenseAttachmentRepository, ExpenseAttachmentRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IProjectAttachmentRepository, ProjectAttachmentRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IEntryFlagRepository, EntryFlagRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
        services.AddScoped<IPayrollPeriodRepository, PayrollPeriodRepository>();

        services.AddScoped<IRateResolver, RateResolver>();
        services.AddScoped<IProjectEstimateService, ProjectEstimateService>();
        services.AddScoped<IUserWorkloadService, UserWorkloadService>();
        services.AddScoped<IFileStorageService, AzureBlobFileStorageService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddSingleton<GraphClientFactory>();
        services.AddScoped<IEmailSender, GraphEmailSender>();
        services.AddScoped<IAdminUserService, GraphAdminUserService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBudgetMonitoringService, BudgetMonitoringService>();
        services.AddScoped<IEntryFlagService, EntryFlagService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IInvoiceGenerationService, InvoiceGenerationService>();
        services.AddScoped<IInvoicingService, InvoicingService>();
        services.AddScoped<IPdfInvoiceRenderer, QuestPdfInvoiceRenderer>();
        services.AddScoped<IRevenueRecognitionService, RevenueRecognitionService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IPayrollAggregationService, PayrollAggregationService>();
        services.AddScoped<IBillingRollForwardService, BillingRollForwardService>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<ILocalUserPasswordService, LocalUserPasswordService>();

        services.AddSingleton<IConfidentialClientApplication>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var tenantId = config["AzureAd:TenantId"] ?? throw new InvalidOperationException("Missing AzureAd:TenantId.");
            var clientId = config["AzureAd:ClientId"] ?? throw new InvalidOperationException("Missing AzureAd:ClientId.");
            var clientSecret = config["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("Missing AzureAd:ClientSecret.");
            var instance = config["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
            var redirectUri = config["AzureAd:RedirectUri"] ?? throw new InvalidOperationException("Missing AzureAd:RedirectUri.");

            return ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"{instance.TrimEnd('/')}/{tenantId}"))
                .WithRedirectUri(redirectUri)
                .Build();
        });
        services.AddScoped<IEntraAuthService, EntraAuthService>();

        services.AddHttpClient<ICurrencyRateProvider, FrankfurterCurrencyRateProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.frankfurter.dev/");
        });

        return services;
    }
}
