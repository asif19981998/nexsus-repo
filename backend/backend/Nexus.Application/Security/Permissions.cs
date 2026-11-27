namespace Nexus.Application.Security;

/// <summary>Role names. A user holds exactly one.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Student = "Student";
    public const string Mentor = "Mentor";

    /// <summary>A university teacher who trains Soft Skill university groups; may also be a university group's coordinator.</summary>
    public const string Trainer = "Trainer";

    /// <summary>An academia or industry expert the Commission engages to monitor Soft Skill training (cl.14 ঘ). Made by an administrator.</summary>
    public const string Monitor = "Monitor";

    public static readonly string[] All = [Admin, Student, Mentor, Trainer, Monitor];

    /// <summary>Roles a person can choose on the public sign-up form.</summary>
    public static readonly string[] SelfSignUp = [Student, Trainer];
}

/// <summary>
/// Every permission code the API checks. The seeder writes these to the Permission table and grants
/// the defaults below; after that the RolePermission table is the source of truth and is edited in the database.
/// </summary>
public static class Permissions
{
    // ---- student self-service ----
    public const string ProgrammeView = "internship.programme.view";
    public const string ApplicationApply = "internship.application.apply";
    public const string SelfView = "internship.self.view";
    public const string SelfAttendance = "internship.self.attendance";
    public const string SelfBill = "internship.self.bill";
    public const string SelfReport = "internship.self.report";

    // ---- administration ----
    public const string OverviewView = "internship.overview.view";
    public const string BatchView = "internship.batch.view";
    public const string BatchManage = "internship.batch.manage";
    public const string BatchOverride = "internship.batch.override";
    public const string ApplicationView = "internship.application.view";
    public const string ApplicationScreen = "internship.application.screen";
    public const string ApplicationExam = "internship.application.exam";
    public const string ApplicationSelect = "internship.application.select";
    public const string InternPlace = "internship.intern.place";
    public const string InternViewAll = "internship.intern.view_all";
    public const string InternViewAssigned = "internship.intern.view_assigned";
    public const string InternTransfer = "internship.intern.transfer";
    public const string AttendanceManage = "internship.attendance.manage";
    public const string ReportReview = "internship.report.review";
    public const string EvaluationSubmit = "internship.evaluation.submit";
    public const string InternComplete = "internship.intern.complete";
    public const string CertificateIssue = "internship.certificate.issue";
    public const string InternTerminate = "internship.intern.terminate";
    public const string BillView = "internship.bill.view";
    public const string BillPay = "internship.bill.pay";

    // ---- user management ----
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";

    /// <summary>UGC Soft Skill Training. Codes start with "softskill.".</summary>
    public static class SoftSkill
    {
        // student
        public const string ProgrammeView = "softskill.programme.view";
        public const string Apply = "softskill.application.apply";
        public const string SelfView = "softskill.self.view";
        public const string SelfLeave = "softskill.self.leave";
        public const string SelfReport = "softskill.self.report";

        // trainer and coordinator; each call also checks the university group is theirs
        public const string TrainerApply = "softskill.trainer.apply";
        public const string UniversityGroupViewAssigned = "softskill.university_group.view_assigned";
        public const string SessionAttendance = "softskill.session.attendance";
        public const string AssessmentEnter = "softskill.assessment.enter";
        public const string UniversityGroupCoordinate = "softskill.university_group.coordinate";

        // Commission
        public const string OverviewView = "softskill.overview.view";
        public const string BatchView = "softskill.batch.view";
        public const string BatchManage = "softskill.batch.manage";
        public const string BatchOverride = "softskill.batch.override";
        public const string ApplicationView = "softskill.application.view";
        public const string ApplicationScreen = "softskill.application.screen";
        public const string ApplicationSelect = "softskill.application.select";
        public const string TrainerReview = "softskill.trainer.review";
        public const string UniversityGroupViewAll = "softskill.university_group.view_all";
        public const string UniversityGroupManageAll = "softskill.university_group.manage_all";
        public const string TraineeCancel = "softskill.trainee.cancel";
        public const string CertificateIssue = "softskill.certificate.issue";
        public const string StipendView = "softskill.stipend.view";
        public const string StipendPay = "softskill.stipend.pay";

        // monitoring (cl.14 ঘ) and honorarium (cl.17)
        public const string MonitoringManage = "softskill.monitoring.manage";
        public const string MonitorVisit = "softskill.monitor.visit";
        public const string HonorariumView = "softskill.honorarium.view";
        public const string HonorariumPay = "softskill.honorarium.pay";
        public const string HonorariumRates = "softskill.honorarium.rates";

        public static readonly string[] StudentCodes = [ProgrammeView, Apply, SelfView, SelfLeave, SelfReport];
        public static readonly string[] TrainerCodes = [TrainerApply, UniversityGroupViewAssigned, SessionAttendance, AssessmentEnter, UniversityGroupCoordinate];
        public static readonly string[] MonitorCodes = [MonitorVisit];
    }

    public sealed record Definition(string Code, string Name, string Module);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(ProgrammeView, "View the programme, eligibility and open batches", "Internship - Student"),
        new(ApplicationApply, "Apply to a batch and withdraw", "Internship - Student"),
        new(SelfView, "View own application and internship", "Internship - Student"),
        new(SelfAttendance, "Sign in, sign out and request leave", "Internship - Student"),
        new(SelfBill, "Submit own monthly report (the allowance bill)", "Internship - Student"),
        new(SelfReport, "Submit the final report", "Internship - Student"),

        new(OverviewView, "View the programme overview", "Internship - Administration"),
        new(BatchView, "View batches", "Internship - Administration"),
        new(BatchManage, "Create batches and change their status", "Internship - Administration"),
        new(BatchOverride, "Exceed the seat or batches-per-year limit (cl.6)", "Internship - Administration"),
        new(ApplicationView, "View applications", "Internship - Administration"),
        new(ApplicationScreen, "Shortlist, waitlist and reject applications", "Internship - Administration"),
        new(ApplicationExam, "Record selection test results", "Internship - Administration"),
        new(ApplicationSelect, "Select candidates for a batch", "Internship - Administration"),
        new(InternPlace, "Place selected candidates in a department", "Internship - Administration"),
        new(InternViewAll, "View every intern", "Internship - Administration"),
        new(InternViewAssigned, "View interns assigned to me as mentor", "Internship - Mentor"),
        new(InternTransfer, "Transfer an intern to another department", "Internship - Administration"),
        new(AttendanceManage, "Decide leave requests and mark absences", "Internship - Mentor"),
        new(ReportReview, "Accept a final report or ask for revision", "Internship - Mentor"),
        new(EvaluationSubmit, "Submit the monthly and final department evaluation", "Internship - Mentor"),
        new(InternComplete, "Mark an internship complete", "Internship - Administration"),
        new(CertificateIssue, "Issue the certificate or outcome", "Internship - Administration"),
        new(InternTerminate, "Terminate an internship", "Internship - Administration"),
        new(BillView, "View allowance bills", "Internship - Administration"),
        new(BillPay, "Pay allowance bills and issue payment slips", "Internship - Administration"),

        new(SoftSkill.ProgrammeView, "View the programme, eligibility and open batches", "Soft Skill - Student"),
        new(SoftSkill.Apply, "Apply to a batch and withdraw", "Soft Skill - Student"),
        new(SoftSkill.SelfView, "View own application, sessions, results and stipend", "Soft Skill - Student"),
        new(SoftSkill.SelfLeave, "Ask the coordinator for leave from a session", "Soft Skill - Student"),
        new(SoftSkill.SelfReport, "Submit the learning report or small project", "Soft Skill - Student"),

        new(SoftSkill.TrainerApply, "Register as a trainer", "Soft Skill - Trainer"),
        new(SoftSkill.UniversityGroupViewAssigned, "View university groups I coordinate or train", "Soft Skill - Trainer"),
        new(SoftSkill.SessionAttendance, "Take attendance at my sessions", "Soft Skill - Trainer"),
        new(SoftSkill.AssessmentEnter, "Enter assessment marks of my university group", "Soft Skill - Trainer"),
        new(SoftSkill.UniversityGroupCoordinate, "Coordinate my university group: schedule, leave, reports, final report", "Soft Skill - Trainer"),

        new(SoftSkill.OverviewView, "View the programme overview", "Soft Skill - Administration"),
        new(SoftSkill.BatchView, "View batches and university groups", "Soft Skill - Administration"),
        new(SoftSkill.BatchManage, "Create batches, add universities and change status", "Soft Skill - Administration"),
        new(SoftSkill.BatchOverride, "Exceed the seat or batches-per-year limit (cl.6 গ)", "Soft Skill - Administration"),
        new(SoftSkill.ApplicationView, "View applications", "Soft Skill - Administration"),
        new(SoftSkill.ApplicationScreen, "Shortlist, waitlist and reject applications", "Soft Skill - Administration"),
        new(SoftSkill.ApplicationSelect, "Select trainees", "Soft Skill - Administration"),
        new(SoftSkill.TrainerReview, "Approve trainers and record their ToT", "Soft Skill - Administration"),
        new(SoftSkill.UniversityGroupViewAll, "View every university group", "Soft Skill - Administration"),
        new(SoftSkill.UniversityGroupManageAll, "Act as coordinator of any university group", "Soft Skill - Administration"),
        new(SoftSkill.TraineeCancel, "Cancel a trainee's participation (cl.11 ঞ)", "Soft Skill - Administration"),
        new(SoftSkill.CertificateIssue, "Issue certificates and outcomes (cl.15)", "Soft Skill - Administration"),
        new(SoftSkill.StipendView, "View stipends", "Soft Skill - Administration"),
        new(SoftSkill.StipendPay, "Pay or withhold stipends and issue slips (cl.10)", "Soft Skill - Administration"),
        new(SoftSkill.MonitoringManage, "Plan monitoring visits and see every university group's health (cl.14 ঘ)", "Soft Skill - Administration"),
        new(SoftSkill.HonorariumView, "View honoraria (cl.17)", "Soft Skill - Administration"),
        new(SoftSkill.HonorariumPay, "Pay honoraria and issue slips (cl.17)", "Soft Skill - Administration"),
        new(SoftSkill.HonorariumRates, "Set honorarium rates (cl.17)", "Soft Skill - Administration"),
        new(SoftSkill.MonitorVisit, "Visit assigned university groups and file monitoring reports", "Soft Skill - Monitor"),

        new(UsersView, "View users", "Administration"),
        new(UsersManage, "Create and edit users", "Administration"),
    ];

    private static readonly string[] InternshipStudentCodes = [ProgrammeView, ApplicationApply, SelfView, SelfAttendance, SelfBill, SelfReport];
    private static readonly string[] MentorCodes = [InternViewAssigned, AttendanceManage, ReportReview, EvaluationSubmit];

    /// <summary>
    /// Default grants. A role with no permissions yet gets all of its list; a role that already has some gets only the
    /// codes that are new to the catalogue, so grants removed in the database stay removed.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Defaults = new Dictionary<string, string[]>
    {
        [Roles.Admin] = All.Select(p => p.Code)
            .Except(InternshipStudentCodes).Except(SoftSkill.StudentCodes).Except(SoftSkill.TrainerCodes).Except(SoftSkill.MonitorCodes)
            .Where(c => c != InternViewAssigned)
            .ToArray(),
        [Roles.Student] = [.. InternshipStudentCodes, .. SoftSkill.StudentCodes],
        [Roles.Mentor] = MentorCodes,
        [Roles.Trainer] = SoftSkill.TrainerCodes,
        [Roles.Monitor] = SoftSkill.MonitorCodes,
    };
}
