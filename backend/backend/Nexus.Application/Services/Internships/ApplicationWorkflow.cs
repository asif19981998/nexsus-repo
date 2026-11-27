using Nexus.Application.Common;
using Nexus.Application.Security;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

/// <summary>Status changes on an application always go through here so the history row is never forgotten.</summary>
public sealed class ApplicationWorkflow(ICurrentUserService me, IClock clock)
{
    public void SetStatus(InternshipApplication app, ApplicationStatus status, string? note)
    {
        if (app.Status == status) return;
        app.Status = status;
        app.History.Add(new ApplicationStatusHistory
        {
            Status = status,
            At = clock.UtcNow,
            ById = me.UserId,
            ByName = me.FullName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });
    }
}
