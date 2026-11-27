using Nexus.Application.Data;
using Nexus.Application.Dtos.Documents;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;
using Nexus.Application.Services.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

/// <summary>Entity → DTO. Computed views (attendance, bills, outcome) are worked out here from the rules.</summary>
public static class InternshipMapper
{
    public static PersonDto Person(User u) => new(u.Id, u.FullName, u.Email, u.MainEducation()?.Institution);

    public static ExamDto? Exam(InternshipApplication a) =>
        a is { ExamMode: { } m, ExamDate: { } d, ExamScore: { } s } ? new ExamDto(m.ToString(), d, s) : null;

    public static IReadOnlyList<string> Preferred(InternshipApplication a) =>
        a.PreferredDepartments.OrderBy(p => p.Rank).Select(p => p.Department.Name).ToList();

    public static ApplicationListItemDto ListItem(InternshipApplication a) =>
        new(a.Id, a.BatchId, a.Batch.Name, a.Batch.Code, Person(a.Student), a.Status.Label(), a.AppliedAt, Preferred(a), Exam(a), a.Basis);

    /// <summary>The document versions the application was sent with.</summary>
    public static async Task<IReadOnlyList<MyDocumentDto>> AttachedAsync(IUnitOfWork uow, long applicationId) =>
        (await uow.Documents.ListAttachedAsync(Modules.Internship, applicationId)).Select(DocumentService.ToDto).ToList();

    public static ApplicationDetailDto Detail(InternshipApplication a, PolicySnapshot p, DateOnly today, IReadOnlyList<MyDocumentDto>? attached = null) =>
        new(a.Id, a.BatchId, a.Batch.Name, a.Batch.Code, Person(a.Student), a.Status.Label(), a.AppliedAt, Preferred(a), Exam(a), a.Basis,
            a.Statement,
            // Applications from before the document library carry only names.
            attached is { Count: > 0 } ? attached.Select(d => d.TypeName).ToList() : a.Documents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            a.RejectionReason,
            a.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new HistoryDto(h.Status.Label(), h.At, h.ByName, h.Note)).ToList(),
            a.Internship is null ? null : Internship(a.Internship, p, today),
            attached ?? []);

    public static InternshipDto Internship(Domain.Entities.Internships.Internship it, PolicySnapshot p, DateOnly today)
    {
        var byDate = it.Attendance.ToDictionary(r => r.Date);
        var recent = InternshipRules.ClosedDays(it, today, p.Holidays).AsEnumerable().Reverse().Take(10)
            .Select(d =>
            {
                var s = InternshipRules.DayStatus(byDate.GetValueOrDefault(d), d, today);
                var rec = byDate.GetValueOrDefault(d);
                return new DayDto(d, s.Kind, s.Label, Time(rec?.CheckIn), Time(rec?.CheckOut), InternshipRules.LateMinutes(rec, p.Policy));
            }).ToList();

        var todayRec = byDate.GetValueOrDefault(today);
        var inPeriod = today >= it.StartDate && today <= it.EndDate && InternshipRules.IsWorkingDay(today, p.Holidays);
        var deadline = InternshipRules.ReportDeadline(it, p);
        var outcome = InternshipRules.ComputeOutcome(it, today, p);
        var keyById = p.Criteria.ToDictionary(c => c.Id, c => c.Key);

        return new InternshipDto(
            new DepartmentDto(it.DepartmentId, it.Department.Name),
            it.Mentor is null ? null : new MentorDto(it.Mentor.Id, it.Mentor.FullName, it.Mentor.Designation),
            it.StartDate, it.EndDate, it.Tasks,
            it.Placements.OrderBy(x => x.From).ThenBy(x => x.Id)
                .Select(x => new PlacementDto(x.Department.Name, x.Mentor?.FullName, x.From, x.Note)).ToList(),
            outcome.Attendance,
            recent,
            new TodayDto(today, inPeriod, todayRec is null ? null : new TodayRecordDto(AttendanceLabel(todayRec.Status), Time(todayRec.CheckIn), Time(todayRec.CheckOut), InternshipRules.LateMinutes(todayRec, p.Policy))),
            InternshipRules.LeaveRequests(it),
            InternshipRules.MonthlyBills(it, today, p).OrderByDescending(b => b.Month).ToList(),
            deadline,
            it.Report is null ? null : new ReportDto(it.Report.Text, it.Report.SubmittedAt, it.Report.Status.Label(), it.Report.ReviewNote,
                DateOnly.FromDateTime(it.Report.SubmittedAt.AddHours(6)) > deadline),
            it.Evaluation is null ? null : new EvaluationDto(
                it.Evaluation.Scores.Where(s => keyById.ContainsKey(s.CriterionId)).ToDictionary(s => keyById[s.CriterionId], s => s.Score),
                it.Evaluation.Total, it.Evaluation.Recommendation, it.Evaluation.Evaluator?.FullName, it.Evaluation.EvaluatedAt),
            InternshipRules.EvaluationBlockedReason(it, today, p.Policy),
            it is { CertificateNumber: { } n, CertificateGrade: { } g, CertificateIssuedAt: { } at } ? new CertificateDto(n, g, at) : null,
            it.OutcomeResult is null ? null : new OutcomeDto(it.OutcomeResult, SplitLines(it.OutcomeReasons)),
            new ComputedOutcomeDto(outcome.Ready, outcome.Certifiable, outcome.Grade, outcome.Total, outcome.Reasons));
    }

    public static string AttendanceLabel(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.LeaveRequested => "Leave requested",
        AttendanceStatus.Leave => "Leave",
        _ => "Absent",
    };

    public static string? Time(TimeOnly? t) => t?.ToString("HH:mm");

    public static IReadOnlyList<string> SplitLines(string? s) =>
        string.IsNullOrWhiteSpace(s) ? [] : s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
