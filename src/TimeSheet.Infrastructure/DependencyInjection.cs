using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.ExternalServices;
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
        services.AddScoped<IProjectRateRepository, ProjectRateRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectAssignmentRepository, ProjectAssignmentRepository>();
        services.AddScoped<ITimesheetEntryRepository, TimesheetEntryRepository>();
        services.AddScoped<IExpenseEntryRepository, ExpenseEntryRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IEscalationRepository, EscalationRepository>();

        services.AddScoped<IRateResolver, RateResolver>();
        services.AddScoped<IUserWorkloadService, UserWorkloadService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IEmailSender, GraphEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBudgetMonitoringService, BudgetMonitoringService>();
        services.AddScoped<IEscalationService, EscalationService>();

        services.AddHttpClient<ICurrencyRateProvider, FrankfurterCurrencyRateProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.frankfurter.dev/");
        });

        return services;
    }
}
