using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Data;
using Nexus.Application.Security;
using Nexus.Application.Services.Users;
using Nexus.Domain.Entities.Identity;
using Nexus.Infrastructure.Identity;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Repositories;
using Nexus.Infrastructure.Seeders;

namespace Nexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<NexusDbContext>((sp, options) =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly(typeof(NexusDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        // Identity core only: the API authenticates with JWT bearer, not cookies.
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<NexusDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IInternshipBatchRepository, InternshipBatchRepository>();
        services.AddScoped<IInternshipApplicationRepository, InternshipApplicationRepository>();
        services.AddScoped<ISetupRepository, SetupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUniversityRepository, UniversityRepository>();
        services.AddScoped<ISoftSkillSetupRepository, SoftSkillSetupRepository>();
        services.AddScoped<ISoftSkillBatchRepository, SoftSkillBatchRepository>();
        services.AddScoped<ISoftSkillUniversityGroupRepository, SoftSkillUniversityGroupRepository>();
        services.AddScoped<ISoftSkillApplicationRepository, SoftSkillApplicationRepository>();
        services.AddScoped<ISoftSkillTrainerRepository, SoftSkillTrainerRepository>();
        services.AddScoped<ISoftSkillTraineeRepository, SoftSkillTraineeRepository>();
        services.AddScoped<ISoftSkillVisitRepository, SoftSkillVisitRepository>();
        services.AddScoped<ISoftSkillHonorariumRepository, SoftSkillHonorariumRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
