using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Common;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.Application.Services.Documents;
using Nexus.Application.Services.Internships;
using Nexus.Application.Services.Programmes;
using Nexus.Application.Services.SoftSkills;
using Nexus.Application.Services.Users;

namespace Nexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, BangladeshClock>();
        services.AddScoped<IPermissionService, PermissionService>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IRequirementService, RequirementService>();

        services.AddScoped<ApplicationWorkflow>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IStudentInternshipService, StudentInternshipService>();
        services.AddScoped<IApplicationAdminService, ApplicationAdminService>();
        services.AddScoped<IInternService, InternService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<IOverviewService, OverviewService>();

        services.AddScoped<SoftSkillWorkflow>();
        services.AddScoped<ISoftSkillSetupService, SoftSkillSetupService>();
        services.AddScoped<ISoftSkillBatchService, SoftSkillBatchService>();
        services.AddScoped<IStudentSoftSkillService, StudentSoftSkillService>();
        services.AddScoped<ISoftSkillApplicationAdminService, SoftSkillApplicationAdminService>();
        services.AddScoped<ITrainerService, TrainerService>();
        services.AddScoped<ISoftSkillUniversityGroupService, SoftSkillUniversityGroupService>();
        services.AddScoped<ISoftSkillTraineeService, SoftSkillTraineeService>();
        services.AddScoped<ISoftSkillStipendService, SoftSkillStipendService>();
        services.AddScoped<ISoftSkillMonitoringService, SoftSkillMonitoringService>();
        services.AddScoped<ISoftSkillHonorariumService, SoftSkillHonorariumService>();

        services.AddScoped<IProgrammeCatalogService, ProgrammeCatalogService>();

        return services;
    }
}
