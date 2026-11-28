using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>
/// The work inside one university group: the schedule (cl.8, cl.13), attendance and leave (cl.11 চ, ছ), assessments (cl.14 ক),
/// the learning reports (cl.11 ঝ) and the university's final report (cl.14 ঙ).
/// The coordinator (cl.12 গ) does all of it; a trainer takes attendance at their own sessions and enters marks;
/// the Commission can act on any university group.
/// </summary>
public interface ISoftSkillUniversityGroupService
{
    /// <summary>Every university group for the Commission; otherwise those the caller coordinates or trains.</summary>
    Task<List<UniversityGroupSummaryDto>> ListAsync(long? batchId);
    Task<UniversityGroupDetailDto> GetAsync(long id);

    Task<UniversityGroupDetailDto> AddSessionAsync(long id, SessionRequest request);
    Task<UniversityGroupDetailDto> UpdateSessionAsync(long id, long sessionId, SessionRequest request);
    Task<UniversityGroupDetailDto> CancelSessionAsync(long id, long sessionId);
    Task<UniversityGroupDetailDto> SaveAttendanceAsync(long id, long sessionId, AttendanceSheetRequest request);
    Task<UniversityGroupDetailDto> DecideLeaveAsync(long id, LeaveDecisionRequest request);

    Task<UniversityGroupDetailDto> CreateAssessmentAsync(long id, AssessmentRequest request);
    Task<UniversityGroupDetailDto> SaveResultsAsync(long id, long assessmentId, ResultsRequest request);

    Task<UniversityGroupDetailDto> ReviewReportAsync(long id, ReportReviewRequest request);
    Task<UniversityGroupDetailDto> SubmitFinalReportAsync(long id, FinalReportRequest request);
}

public sealed class SoftSkillUniversityGroupService(
    IUnitOfWork uow, ISoftSkillSetupService setup, IPermissionService permissions, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify) : ISoftSkillUniversityGroupService
{
    private sealed record Access(long UserId, bool Coordinate, bool EnterMarks, bool AttendanceAll, bool IsTrainer, bool Monitor, bool ManageMonitoring);

    public async Task<List<UniversityGroupSummaryDto>> ListAsync(long? batchId)
    {
        var userId = me.RequireUserId();
        var all = await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupViewAll) || await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupManageAll);
        var universityGroups = await uow.SoftSkillUniversityGroups.ListAsync(batchId, all ? null : userId);
        var taken = await uow.SoftSkillBatches.TakenSeatsByUniversityGroupAsync();
        return universityGroups.Select(c => SoftSkillMapper.UniversityGroup(c, taken.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<UniversityGroupDetailDto> GetAsync(long id)
    {
        var (c, access) = await LoadAsync(id);
        return await DetailAsync(c, access);
    }

    // ---------- schedule ----------

    public async Task<UniversityGroupDetailDto> AddSessionAsync(long id, SessionRequest r)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        var session = new SoftSkillSession { UniversityGroupId = c.Id };
        await ApplySessionAsync(c, session, r);
        c.Sessions.Add(session);
        await audit.LogAsync("softskill.session.add", "SoftSkillUniversityGroup", c.Id, Describe(session));
        if (session.TrainerId != access.UserId)
            await notify.NotifyAsync(session.TrainerId, "Soft Skill session assigned",
                $"{session.Topic.Name} on {session.Date:dd MMM yyyy}, {SoftSkillMapper.Time(session.StartTime)} at {c.University.Name}.", Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    public async Task<UniversityGroupDetailDto> UpdateSessionAsync(long id, long sessionId, SessionRequest r)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        var session = SessionOf(c, sessionId);
        if (session.Status != SessionStatus.Scheduled) throw new BadRequestException("Only a scheduled session can be changed.");
        await ApplySessionAsync(c, session, r);
        await audit.LogAsync("softskill.session.update", "SoftSkillUniversityGroup", c.Id, Describe(session));
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    public async Task<UniversityGroupDetailDto> CancelSessionAsync(long id, long sessionId)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        var session = SessionOf(c, sessionId);
        if (session.Status != SessionStatus.Scheduled) throw new BadRequestException("Only a scheduled session can be cancelled.");
        session.Status = SessionStatus.Cancelled;
        await audit.LogAsync("softskill.session.cancel", "SoftSkillUniversityGroup", c.Id, Describe(session));
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    // ---------- attendance (cl.11 চ, ছ) ----------

    public async Task<UniversityGroupDetailDto> SaveAttendanceAsync(long id, long sessionId, AttendanceSheetRequest r)
    {
        var (c, access) = await LoadAsync(id);
        var session = SessionOf(c, sessionId);
        var mayTake = access.AttendanceAll
            || (session.TrainerId == access.UserId && await permissions.HasAsync(Permissions.SoftSkill.SessionAttendance));
        if (!mayTake) throw new ForbiddenException("Only the session's trainer or the coordinator takes attendance.");
        if (session.Status == SessionStatus.Cancelled) throw new BadRequestException("This session was cancelled.");
        if (session.Date > clock.Today) throw new BadRequestException("Attendance is taken on or after the session's date.");
        if (c.Batch.Status != BatchStatus.Running) throw new BadRequestException("Attendance is taken while the batch is running.");

        var trainees = ActiveTrainees(c).ToDictionary(t => t.Id);
        var marks = r.Marks.GroupBy(m => m.TraineeId).ToDictionary(g => g.Key, g => g.Last());
        foreach (var m in marks.Values)
            if (!trainees.ContainsKey(m.TraineeId)) throw new BadRequestException("A mark is for someone who is not a trainee of this university group.");

        foreach (var t in trainees.Values)
        {
            var rec = session.Attendance.FirstOrDefault(a => a.TraineeId == t.Id);
            var m = marks.GetValueOrDefault(t.Id);
            // Not on the sheet: an approved leave or pending request stays; anyone else is absent.
            var status = m?.Status?.Trim().ToLowerInvariant() switch
            {
                "present" => AttendanceStatus.Present,
                "leave" => AttendanceStatus.Leave,
                "absent" => AttendanceStatus.Absent,
                null => rec?.Status is AttendanceStatus.Leave or AttendanceStatus.LeaveRequested ? rec.Status : AttendanceStatus.Absent,
                _ => throw new BadRequestException("Mark each trainee Present, Absent or Leave."),
            };
            if (status == AttendanceStatus.Leave && rec?.Status is not (AttendanceStatus.Leave or AttendanceStatus.LeaveRequested) && !access.Coordinate)
                throw new BadRequestException("Only the coordinator grants leave (cl.11 ছ).");

            TimeOnly? inT = null, outT = null;
            if (status == AttendanceStatus.Present)
            {
                inT = string.IsNullOrWhiteSpace(m?.CheckIn) ? session.StartTime : SoftSkillMapper.ParseTime(m.CheckIn) ?? throw new BadRequestException("Arrival time must look like 09:30.");
                outT = string.IsNullOrWhiteSpace(m?.CheckOut) ? session.EndTime : SoftSkillMapper.ParseTime(m.CheckOut) ?? throw new BadRequestException("Departure time must look like 13:30.");
                if (outT <= inT) throw new BadRequestException("Departure must be after arrival.");
            }

            if (rec is null)
            {
                rec = new SoftSkillAttendance { SessionId = session.Id, TraineeId = t.Id };
                session.Attendance.Add(rec);
            }
            if (status == AttendanceStatus.Leave && rec.ReasonType is null) rec.ReasonType = LeaveReasonType.Other;
            rec.Status = status;
            rec.CheckIn = inT;
            rec.CheckOut = outT;
            rec.MarkedById = access.UserId;
            rec.MarkedAt = clock.UtcNow;
        }
        session.Status = SessionStatus.Held;
        var present = session.Attendance.Count(a => a.Status == AttendanceStatus.Present);
        await audit.LogAsync("softskill.attendance.save", "SoftSkillUniversityGroup", c.Id, $"{Describe(session)}: {present}/{trainees.Count} present");
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    public async Task<UniversityGroupDetailDto> DecideLeaveAsync(long id, LeaveDecisionRequest r)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        var rec = c.Sessions.SelectMany(s => s.Attendance).FirstOrDefault(a => a.Id == r.AttendanceId)
            ?? throw new NotFoundException("Leave request", r.AttendanceId);
        if (rec.Status != AttendanceStatus.LeaveRequested) throw new BadRequestException("This leave request is already decided.");
        rec.Status = r.Approve ? AttendanceStatus.Leave : AttendanceStatus.Absent;
        var note = string.IsNullOrWhiteSpace(r.Note) ? "Leave rejected" : $"Leave rejected: {r.Note.Trim()}";
        rec.Note = r.Approve ? null : note.Length <= 200 ? note : note[..200];
        rec.MarkedById = access.UserId;
        rec.MarkedAt = clock.UtcNow;
        var trainee = c.Trainees.First(t => t.Id == rec.TraineeId);
        var session = c.Sessions.First(s => s.Id == rec.SessionId);
        await audit.LogAsync(r.Approve ? "softskill.leave.approve" : "softskill.leave.reject", "SoftSkillTrainee", trainee.Id, Describe(session));
        await notify.NotifyAsync(trainee.Application.StudentId, r.Approve ? "Leave approved" : "Leave not approved",
            $"Your leave for {session.Date:dd MMM yyyy} was {(r.Approve ? "approved" : "not approved")}.", Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    // ---------- assessments (cl.14) ----------

    public async Task<UniversityGroupDetailDto> CreateAssessmentAsync(long id, AssessmentRequest r)
    {
        var (c, access) = await LoadAsync(id);
        if (!access.EnterMarks) throw new ForbiddenException("You cannot set assessments for this university group.");
        if (r.No is < 1 or > SoftSkillRules.AssessmentCount) throw new BadRequestException($"The assessment number is 1–{SoftSkillRules.AssessmentCount} (cl.14 ক).");
        if (c.Assessments.Any(a => a.No == r.No)) throw new ConflictException($"Assessment {r.No} already exists.");
        if (r.Date is not { } date) throw new BadRequestException("Set the assessment date.");
        if (date < c.Batch.StartDate) throw new BadRequestException("The assessment cannot be before the training starts.");
        var policy = (await setup.GetSnapshotAsync()).Policy;
        c.Assessments.Add(new SoftSkillAssessment { No = r.No, Date = date, MaxMarks = policy.AssessmentMaxMarks, PassMarks = policy.AssessmentPassMarks });
        await audit.LogAsync("softskill.assessment.create", "SoftSkillUniversityGroup", c.Id, $"Assessment {r.No} on {date:yyyy-MM-dd}");
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    public async Task<UniversityGroupDetailDto> SaveResultsAsync(long id, long assessmentId, ResultsRequest r)
    {
        var (c, access) = await LoadAsync(id);
        if (!access.EnterMarks) throw new ForbiddenException("You cannot enter marks for this university group.");
        var a = c.Assessments.FirstOrDefault(x => x.Id == assessmentId) ?? throw new NotFoundException("Assessment", assessmentId);
        if (a.Date > clock.Today) throw new BadRequestException("Marks are entered on or after the assessment date.");
        var trainees = c.Trainees.Where(t => t.Application.Status is ApplicationStatus.Active or ApplicationStatus.Completed && t.CancelledAt is null)
            .ToDictionary(t => t.Id);

        foreach (var m in r.Results)
        {
            if (!trainees.TryGetValue(m.TraineeId, out var t)) throw new BadRequestException("A result is for someone who is not a trainee of this university group.");
            if (t.Stipends.Any(s => s.AssessmentNo == a.No && s.PaidAt is not null))
                throw new ConflictException($"{t.Application.Student.FullName}'s stipend for month {a.No} is paid, so the result cannot change.");
            if (!m.Absent && (m.Marks is not { } marks || marks < 0 || marks > a.MaxMarks))
                throw new BadRequestException($"Marks must be 0–{a.MaxMarks}, or mark the trainee absent.");
            var note = string.IsNullOrWhiteSpace(m.Note) ? null : m.Note.Trim();
            if (note is { Length: > SoftSkillAssessmentResult.MaxNoteLength }) throw new BadRequestException($"A note can have at most {SoftSkillAssessmentResult.MaxNoteLength} characters.");

            var res = a.Results.FirstOrDefault(x => x.TraineeId == t.Id);
            if (res is null)
            {
                res = new SoftSkillAssessmentResult { TraineeId = t.Id };
                a.Results.Add(res);
            }
            res.Absent = m.Absent;
            res.Marks = m.Absent ? null : m.Marks;
            res.Malpractice = m.Malpractice;
            res.Note = note;
            res.EnteredById = access.UserId;
            res.EnteredAt = clock.UtcNow;
        }
        await audit.LogAsync("softskill.assessment.results", "SoftSkillUniversityGroup", c.Id, $"Assessment {a.No}: {r.Results.Count} results");
        if (r.Results.Count > 0)
            await notify.NotifyHoldersAsync(Permissions.SoftSkill.StipendPay, "Soft Skill results entered",
                $"{c.University.Name}: assessment {a.No} results are in; stipends can be checked.", Links.SoftSkillStipends(c.BatchId));
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    // ---------- reports ----------

    public async Task<UniversityGroupDetailDto> ReviewReportAsync(long id, ReportReviewRequest r)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        var t = c.Trainees.FirstOrDefault(x => x.Id == r.TraineeId) ?? throw new NotFoundException("Trainee", r.TraineeId);
        if (t.Report is not { Status: ReportStatus.Submitted } report) throw new BadRequestException("There is no submitted report to review.");
        var note = r.Note?.Trim();
        if (!r.Accept && string.IsNullOrWhiteSpace(note)) throw new BadRequestException("Say what needs to change.");
        if (note is { Length: > SoftSkillLearningReport.MaxReviewNoteLength }) throw new BadRequestException($"At most {SoftSkillLearningReport.MaxReviewNoteLength} characters.");
        report.Status = r.Accept ? ReportStatus.Accepted : ReportStatus.RevisionRequested;
        report.ReviewNote = string.IsNullOrEmpty(note) ? null : note;
        report.ReviewedById = access.UserId;
        report.ReviewedAt = clock.UtcNow;
        await audit.LogAsync(r.Accept ? "softskill.report.accept" : "softskill.report.revise", "SoftSkillTrainee", t.Id, note);
        await notify.NotifyAsync(t.Application.StudentId, r.Accept ? "Learning report accepted" : "Learning report needs changes",
            r.Accept ? "Your coordinator accepted your report." : note!, Links.SoftSkillStudent);
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    public async Task<UniversityGroupDetailDto> SubmitFinalReportAsync(long id, FinalReportRequest r)
    {
        var (c, access) = await LoadAsync(id);
        RequireCoordinate(access);
        if (clock.Today < c.Batch.EndDate) throw new BadRequestException($"The final report is sent after the training ends on {c.Batch.EndDate:dd MMM yyyy} (cl.14 ঙ).");
        var policy = (await setup.GetSnapshotAsync()).Policy;
        var report = r.Report?.Trim() ?? "";
        var recommendation = r.Recommendation?.Trim() ?? "";
        if (report.Length < policy.MinFinalReportLength) throw new BadRequestException($"Write at least {policy.MinFinalReportLength} characters evaluating the batch.");
        if (recommendation.Length < 10) throw new BadRequestException("Add the university's recommendations (cl.14 ঙ).");
        if (report.Length > SoftSkillUniversityGroup.MaxFinalReportLength || recommendation.Length > SoftSkillUniversityGroup.MaxFinalReportLength)
            throw new BadRequestException($"Each part can have at most {SoftSkillUniversityGroup.MaxFinalReportLength} characters.");
        c.FinalReport = report;
        c.FinalRecommendation = recommendation;
        c.FinalReportAt = clock.UtcNow;
        c.FinalReportById = access.UserId;
        await audit.LogAsync("softskill.university_group.final_report", "SoftSkillUniversityGroup", c.Id, $"{c.University.Name}, {c.Batch.Code}");
        await notify.NotifyHoldersAsync(Permissions.SoftSkill.CertificateIssue, "Soft Skill final report received",
            $"{c.University.Name} sent its final evaluation report for {c.Batch.Name}.", Links.SoftSkillUniversityGroup(c.Id, "final"));
        await uow.SaveChangesAsync();
        return await DetailAsync(c, access);
    }

    // ---------- helpers ----------

    private async Task<(SoftSkillUniversityGroup UniversityGroup, Access Access)> LoadAsync(long id)
    {
        var userId = me.RequireUserId();
        var c = await uow.SoftSkillUniversityGroups.GetDetailAsync(id) ?? throw new NotFoundException("University group", id);
        var manageAll = await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupManageAll);
        var viewAll = manageAll || await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupViewAll);
        var coordinate = manageAll || (c.CoordinatorId == userId && await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupCoordinate));
        var isTrainer = c.Sessions.Any(s => s.TrainerId == userId);
        var manageMonitoring = await permissions.HasAsync(Permissions.SoftSkill.MonitoringManage);
        // cl.14 (ঘ): a monitor sees, read-only, the university groups they are sent to.
        var monitor = await permissions.HasAsync(Permissions.SoftSkill.MonitorVisit)
            && await uow.SoftSkillVisits.AnyAsync(v => v.UniversityGroupId == id && v.MonitorId == userId && v.Status != VisitStatus.Cancelled);
        var view = viewAll || coordinate || ((isTrainer || c.CoordinatorId == userId) && await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupViewAssigned));
        view = view || manageMonitoring || monitor;
        // Someone else's university group looks the same as a missing one.
        if (!view) throw new NotFoundException("University group", id);
        var enterMarks = manageAll || ((coordinate || isTrainer) && await permissions.HasAsync(Permissions.SoftSkill.AssessmentEnter));
        return (c, new Access(userId, coordinate, enterMarks, coordinate, isTrainer, monitor, manageMonitoring));
    }

    private static void RequireCoordinate(Access a)
    {
        if (!a.Coordinate) throw new ForbiddenException("Only the university group's coordinator can do this.");
    }

    private static SoftSkillSession SessionOf(SoftSkillUniversityGroup c, long sessionId) =>
        c.Sessions.FirstOrDefault(s => s.Id == sessionId) ?? throw new NotFoundException("Session", sessionId);

    private static IEnumerable<SoftSkillTrainee> ActiveTrainees(SoftSkillUniversityGroup c) =>
        c.Trainees.Where(t => t.Application.Status == ApplicationStatus.Active && t.CancelledAt is null);

    private static string Describe(SoftSkillSession s) =>
        $"{s.Topic?.Name ?? $"topic {s.TopicId}"}, {s.Date:yyyy-MM-dd} {SoftSkillMapper.Time(s.StartTime)}–{SoftSkillMapper.Time(s.EndTime)}";

    private async Task ApplySessionAsync(SoftSkillUniversityGroup c, SoftSkillSession session, SessionRequest r)
    {
        if (c.Batch.Status is BatchStatus.Completed or BatchStatus.Cancelled) throw new BadRequestException("The batch is closed.");
        var snap = await setup.GetSnapshotAsync();
        var topic = snap.Topics.FirstOrDefault(t => t.Id == r.TopicId) ?? throw new BadRequestException("Choose one of the six topics (cl.13).");
        if (r.Date is not { } date) throw new BadRequestException("Choose the date.");
        if (date < c.Batch.StartDate || date > c.Batch.EndDate)
            throw new BadRequestException($"Sessions fall within the training, {c.Batch.StartDate:dd MMM} to {c.Batch.EndDate:dd MMM yyyy} (cl.8).");
        var start = SoftSkillMapper.ParseTime(r.StartTime) ?? throw new BadRequestException("Start time must look like 09:30.");
        var end = SoftSkillMapper.ParseTime(r.EndTime) ?? throw new BadRequestException("End time must look like 13:30.");
        if (end <= start) throw new BadRequestException("The session must end after it starts.");
        var hours = (decimal)(end - start).TotalHours;

        // cl.13: each topic has 8 hours; cancelled sessions do not count.
        var planned = c.Sessions.Where(s => s.TopicId == topic.Id && s.Id != session.Id && s.Status != SessionStatus.Cancelled).Sum(s => s.Hours);
        if (planned + hours > topic.Hours)
            throw new BadRequestException($"{topic.Name} has {topic.Hours} hours; {planned:0.#} are already planned, so at most {Math.Max(0, topic.Hours - planned):0.#} more.");

        // cl.12 (ক, খ): one of the university's own teachers who has taken the ToT.
        if (r.TrainerId is not { } trainerId) throw new BadRequestException("Choose the trainer.");
        var trainer = await uow.SoftSkillTrainers.GetForUserAsync(trainerId);
        if (trainer is null || trainer.UniversityId != c.UniversityId) throw new BadRequestException("The trainer must be registered at this university group's university.");
        if (trainer.Status != TrainerStatus.Approved) throw new BadRequestException("The trainer's registration is not approved.");
        if (!trainer.TotCompleted) throw new BadRequestException("The trainer has not completed the Training of Trainers (cl.12 খ).");

        if (c.Sessions.Any(s => s.Id != session.Id && s.Status != SessionStatus.Cancelled && s.Date == date && s.StartTime < end && start < s.EndTime))
            throw new ConflictException("Another session of this university group overlaps that time.");

        var note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        if (note is { Length: > SoftSkillSession.MaxNoteLength }) throw new BadRequestException($"The note can have at most {SoftSkillSession.MaxNoteLength} characters.");

        session.TopicId = topic.Id;
        session.Topic = topic;
        session.TrainerId = trainerId;
        session.Trainer = (await uow.Users.GetTrackedWithProfileAsync(trainerId))!;
        session.Date = date;
        session.StartTime = start;
        session.EndTime = end;
        session.Note = note;
    }

    private async Task<UniversityGroupDetailDto> DetailAsync(SoftSkillUniversityGroup c, Access access)
    {
        var snap = await setup.GetSnapshotAsync();
        var taken = await uow.SoftSkillBatches.TakenSeatsAsync(c.Id);
        var deadline = SoftSkillRules.ReportDeadline(c.Batch.EndDate, snap.Policy.ReportWorkingDays, snap.Calendar);

        var topics = snap.Topics.Select(t =>
        {
            var sessions = c.Sessions.Where(s => s.TopicId == t.Id).ToList();
            return new TopicProgressDto(t.Id, t.Name, t.AssessmentNo, t.Hours,
                sessions.Where(s => s.Status != SessionStatus.Cancelled).Sum(s => s.Hours),
                sessions.Where(s => s.Status == SessionStatus.Held).Sum(s => s.Hours));
        }).ToList();

        var trainees = c.Trainees
            .Where(t => SoftSkillRules.TraineeStatuses.Contains(t.Application.Status))
            .OrderBy(t => t.Application.Student.FullName)
            .Select(t => new UniversityGroupTraineeDto(t.Id, SoftSkillMapper.Person(t.Application.Student), t.Application.Status.Label(),
                SoftSkillRules.Attendance(t, c.Sessions, snap.Policy), SoftSkillMapper.Results(t, c),
                SoftSkillMapper.Report(t.Report, deadline), t.CertificateGrade, t.CancelReason))
            .ToList();

        var names = c.Trainees.ToDictionary(t => t.Id, t => t.Application.Student.FullName);
        var leave = c.Sessions
            .SelectMany(s => s.Attendance.Select(a => (s, a)))
            .Where(x => x.a.ReasonType is not null || x.a.Status == AttendanceStatus.LeaveRequested)
            .OrderBy(x => x.a.Status == AttendanceStatus.LeaveRequested ? 0 : 1).ThenBy(x => x.s.Date)
            .Select(x => new LeaveRequestDto(x.a.Id, x.s.Id, x.a.TraineeId, names.GetValueOrDefault(x.a.TraineeId, "?"), x.s.Date, x.s.Topic.Name,
                x.a.ReasonType?.ToString(), x.a.Reason,
                x.a.Status switch { AttendanceStatus.LeaveRequested => "Pending", AttendanceStatus.Leave => "Approved", AttendanceStatus.Present => "Attended", _ => "Rejected" }))
            .ToList();

        var assessments = c.Assessments.OrderBy(a => a.No)
            .Select(a => new AssessmentDto(a.Id, a.No, a.Date, a.MaxMarks, a.PassMarks, a.Results.Count, a.Results.Count(x => x.PassedFor(a))))
            .ToList();

        // Monitoring reports go back to the university group, so the coordinator can act on them; a monitor sees their own.
        var visits = access.Coordinate || access.ManageMonitoring || await permissions.HasAsync(Permissions.SoftSkill.UniversityGroupViewAll)
            ? (await uow.SoftSkillVisits.ListAsync(null, c.Id, null, null)).Where(v => v.Status != VisitStatus.Cancelled).Select(SoftSkillMapper.Visit).ToList()
            : (await uow.SoftSkillVisits.ListAsync(null, c.Id, access.UserId, null)).Select(SoftSkillMapper.Visit).ToList();

        var trainers = (await uow.SoftSkillTrainers.ListAsync(TrainerStatus.Approved, c.UniversityId))
            .Select(t => new TrainerOptionDto(t.UserId, t.User.FullName, t.User.Designation, SoftSkillMapper.CanTrain(t)))
            .ToList();

        return new UniversityGroupDetailDto(
            SoftSkillMapper.UniversityGroup(c, taken), topics,
            c.Sessions.OrderBy(s => s.Date).ThenBy(s => s.StartTime).Select(SoftSkillMapper.Session).ToList(),
            trainees, leave, assessments,
            c.FinalReport, c.FinalRecommendation, c.FinalReportAt, deadline,
            trainers, new UniversityGroupAccessDto(access.Coordinate, access.EnterMarks, access.AttendanceAll, access.Monitor, access.ManageMonitoring),
            visits);
    }
}
