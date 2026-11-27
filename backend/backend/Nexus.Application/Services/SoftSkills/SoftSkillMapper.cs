using System.Globalization;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Services.Documents;
using Nexus.Application.Services.Internships;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>Entities to DTOs for the Soft Skill screens. Needs the graphs the repositories load.</summary>
public static class SoftSkillMapper
{
    public static UniversityDto University(University u) => new(u.Id, u.Name, u.ShortName, u.NameBn, u.IsPublic);

    public static TopicDto Topic(SoftSkillTopic t) => new(t.Id, t.Code, t.Name, t.NameBn, t.Hours, t.AssessmentNo);

    public static PersonRefDto PersonRef(User u) => new(u.Id, u.FullName, u.Email, u.Designation);

    public static PersonDto Person(User u) => InternshipMapper.Person(u);

    public static string? Time(TimeOnly? t) => t?.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static TimeOnly? ParseTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    // ---------- batches and university groups ----------

    public static UniversityGroupSummaryDto UniversityGroup(SoftSkillUniversityGroup c, int taken) =>
        new(c.Id, c.BatchId, c.Batch.Code, c.Batch.Name, c.Batch.Status.Label(), c.Batch.StartDate, c.Batch.EndDate,
            University(c.University), c.Capacity, taken, Math.Max(0, c.Capacity - taken),
            c.Coordinator is null ? null : PersonRef(c.Coordinator), c.Venue, c.OverrideReason, c.FinalReportAt is not null);

    public static SoftSkillBatchDto Batch(SoftSkillBatch b, IReadOnlyDictionary<long, int> taken)
    {
        var universityGroups = b.UniversityGroups.OrderBy(c => c.University.SortOrder).ThenBy(c => c.University.Name)
            .Select(c => UniversityGroup(c, taken.GetValueOrDefault(c.Id))).ToList();
        return new(b.Id, b.Code, b.Name, b.Year, b.Status.Label(), b.ApplyFrom, b.ApplyTo, b.StartDate, b.EndDate, b.Note, b.OverrideReason,
            universityGroups, universityGroups.Sum(c => c.Capacity), universityGroups.Sum(c => c.Taken));
    }

    // ---------- applications ----------

    public static SoftSkillApplicationListItemDto ListItem(SoftSkillApplication a) =>
        new(a.Id, a.BatchId, a.Batch.Code, a.Batch.Name, a.UniversityGroupId, a.UniversityGroup.University.Name, Person(a.Student),
            a.Status.Label(), a.AppliedAt, a.Basis, a.ScreeningScore);

    public static SoftSkillApplicationDto Application(SoftSkillApplication a, IReadOnlyList<MyDocumentDto>? attached = null) =>
        new(a.Id, a.BatchId, a.Batch.Code, a.Batch.Name, a.Batch.Status.Label(), a.UniversityGroupId, a.UniversityGroup.University.Name,
            Person(a.Student), a.Status.Label(), a.AppliedAt, a.Statement, a.Basis, a.RejectionReason, a.ScreeningScore,
            attached is { Count: > 0 } ? attached.Select(d => d.TypeName).ToList() : a.Documents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            a.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new HistoryDto(h.Status.Label(), h.At, h.ByName, h.Note)).ToList(),
            a.Trainee?.Id, attached);

    /// <summary>The document versions an application (or trainer registration) was sent with.</summary>
    public static async Task<IReadOnlyList<MyDocumentDto>> AttachedAsync(IUnitOfWork uow, string module, long applicationId) =>
        (await uow.Documents.ListAttachedAsync(module, applicationId)).Select(DocumentService.ToDto).ToList();

    // ---------- delivery ----------

    public static SessionDto Session(SoftSkillSession s) =>
        new(s.Id, s.UniversityGroupId, s.TopicId, s.Topic.Name, s.Topic.AssessmentNo, PersonRef(s.Trainer), s.Date,
            Time(s.StartTime)!, Time(s.EndTime)!, s.Hours, s.Status.ToString(), s.Note,
            s.Attendance.Count(a => a.Status == AttendanceStatus.Present), s.Attendance.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Absent or AttendanceStatus.Leave));

    public static string AttendanceLabel(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.LeaveRequested => "Leave requested",
        AttendanceStatus.Leave => "Leave",
        _ => "Absent",
    };

    public static List<AssessmentResultDto> Results(SoftSkillTrainee t, SoftSkillUniversityGroup c)
    {
        var list = new List<AssessmentResultDto>();
        for (var no = 1; no <= SoftSkillRules.AssessmentCount; no++)
        {
            var a = c.Assessments.FirstOrDefault(x => x.No == no);
            var r = SoftSkillRules.ResultFor(t, a);
            list.Add(new AssessmentResultDto(no, a?.Date, r?.Marks, a?.MaxMarks, a?.PassMarks, r?.Absent ?? false, r?.Malpractice ?? false,
                r is null || a is null ? null : r.PassedFor(a), r?.Note));
        }
        return list;
    }

    public static LearningReportDto? Report(SoftSkillLearningReport? r, DateOnly deadline) =>
        r is null ? null : new(r.Type.ToString(), r.Title, r.Text, r.SubmittedAt, r.Status.Label(), r.ReviewNote,
            DateOnly.FromDateTime(r.SubmittedAt.AddHours(6)) > deadline);   // Bangladesh time

    public static TraineeDto Trainee(SoftSkillTrainee t, SoftSkillSnapshot s, DateOnly today)
    {
        var c = t.UniversityGroup;
        var app = t.Application;
        var byId = t.Attendance.ToDictionary(a => a.SessionId);
        var sessions = c.Sessions.OrderBy(x => x.Date).ThenBy(x => x.StartTime)
            .Select(x =>
            {
                var r = byId.GetValueOrDefault(x.Id);
                return new TraineeSessionDto(Session(x), r is null ? null : AttendanceLabel(r.Status), Time(r?.CheckIn), Time(r?.CheckOut),
                    r?.ReasonType?.ToString(), r?.Reason, r?.Note);
            }).ToList();
        var deadline = SoftSkillRules.ReportDeadline(c.Batch.EndDate, s.Policy.ReportWorkingDays, s.Calendar);
        var cert = t.CertificateNumber is null ? null
            : new SoftSkillCertificateDto(t.CertificateNumber, t.CertificateGrade!, t.AverageScore, t.CertificateIssuedAt!.Value);
        return new TraineeDto(
            t.Id, app.Id, Person(app.Student), app.Status.Label(), c.Id, University(c.University),
            c.Batch.Code, c.Batch.Name, c.Batch.Status.Label(), c.Batch.StartDate, c.Batch.EndDate,
            c.Coordinator is null ? null : PersonRef(c.Coordinator), c.Venue,
            sessions, SoftSkillRules.Attendance(t, c.Sessions, s.Policy),
            Results(t, c), SoftSkillRules.Stipends(t, c, s.Policy),
            deadline, Report(t.Report, deadline),
            cert, InternshipMapper.SplitLines(t.OutcomeReasons), t.CancelReason,
            SoftSkillRules.Outcome(t, c, today, s));
    }

    // ---------- trainers ----------

    // ---------- monitoring ----------

    public static VisitDto Visit(SoftSkillMonitoringVisit v) =>
        new(v.Id, v.UniversityGroupId, v.UniversityGroup.University.Name, v.UniversityGroup.Batch.Code, v.UniversityGroup.Batch.Name, PersonRef(v.Monitor),
            v.SessionId, v.Session is null ? null : $"{v.Session.Topic.Name}, {Time(v.Session.StartTime)}–{Time(v.Session.EndTime)}",
            v.PlannedOn, v.Status.ToString(), v.Purpose, v.VisitedOn, v.DeliveryRating, v.EngagementRating, v.AttendanceRating, v.FacilitiesRating,
            v.AverageRating, v.Findings, v.Recommendations, v.SubmittedAt);

    public static bool CanTrain(SoftSkillTrainer t) => t.Status == TrainerStatus.Approved && t.TotCompleted;

    public static TrainerDto Trainer(SoftSkillTrainer t, User user, IReadOnlyList<MyDocumentDto>? attached = null) =>
        new(t.Id, PersonRef(user), user.TrainerProfile?.AcademicDepartment, University(t.University), t.Status.ToString(), t.Statement,
            t.AppliedAt, t.DecidedAt, t.DecisionNote, t.TotCompleted, t.TotCompletedOn, t.TotReference, CanTrain(t), attached);
}
