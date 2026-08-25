using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
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

        services.AddScoped<IRateResolver, RateResolver>();
        services.AddScoped<IUserWorkloadService, UserWorkloadService>();

        return services;
    }
}
