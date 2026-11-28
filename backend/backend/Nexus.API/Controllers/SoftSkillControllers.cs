using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.API.Security;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Security;
using Nexus.Application.Services.Programmes;
using Nexus.Application.Services.SoftSkills;
using P = Nexus.Application.Security.Permissions.SoftSkill;

namespace Nexus.API.Controllers;

// UGC Soft Skill Training. Every action names its permission; actions on a university group also check, in the service,
// that the caller coordinates or trains it unless they hold the Commission's "all university groups" permissions.

/// <summary>The programmes the signed-in person can join, one card per module.</summary>
[ApiController]
public class ProgrammesController(IProgrammeCatalogService catalog, ISoftSkillSetupService setup) : ControllerBase
{
    [HttpGet("api/v1/programmes")]
    public async Task<ActionResult<List<ProgrammeCardDto>>> Mine() => Ok(await catalog.ListMineAsync());

    /// <summary>Active universities. Public, so the sign-up and profile forms can offer the list.</summary>
    [AllowAnonymous]
    [HttpGet("api/v1/universities")]
    public async Task<ActionResult<List<UniversityDto>>> Universities() => Ok(await setup.ListUniversitiesAsync());
}

[ApiController]
[Route("api/v1/softskill")]
public class SoftSkillSetupController(ISoftSkillSetupService setup, ISoftSkillStipendService stipends) : ControllerBase
{
    [HttpGet("setup")]
    public async Task<ActionResult<SoftSkillSetupDto>> Get() => Ok(await setup.GetSetupAsync());

    [HttpGet("overview")]
    [HasPermission(P.OverviewView)]
    public async Task<ActionResult<SoftSkillOverviewDto>> Overview() => Ok(await stipends.GetOverviewAsync());
}

[ApiController]
[Route("api/v1/softskill/batches")]
public class SoftSkillBatchesController(ISoftSkillBatchService batches) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.BatchView)]
    public async Task<ActionResult<List<SoftSkillBatchDto>>> List() => Ok(await batches.ListAsync());

    /// <summary>Over the batches-per-year limit the response is 400 with code "needs_override" until an override reason is sent.</summary>
    [HttpPost]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> Create(CreateSoftSkillBatchRequest request) => Ok(await batches.CreateAsync(request));

    [HttpGet("{id:long}")]
    [HasPermission(P.BatchView)]
    public async Task<ActionResult<SoftSkillBatchDto>> Get(long id) => Ok(await batches.GetAsync(id));

    /// <summary>Edit a batch. Same "needs_override" flow as create when the new start date lands in a full year.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> Update(long id, UpdateSoftSkillBatchRequest request) => Ok(await batches.UpdateAsync(id, request));

    [HttpPost("{id:long}/status")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> SetStatus(long id, SetBatchStatusRequest request) => Ok(await batches.SetStatusAsync(id, request));

    [HttpPost("{id:long}/university-groups")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> AddUniversityGroup(long id, UniversityGroupRequest request) => Ok(await batches.AddUniversityGroupAsync(id, request));

    [HttpPut("university-groups/{universityGroupId:long}")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> UpdateUniversityGroup(long universityGroupId, UniversityGroupRequest request) => Ok(await batches.UpdateUniversityGroupAsync(universityGroupId, request));

    [HttpDelete("university-groups/{universityGroupId:long}")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> RemoveUniversityGroup(long universityGroupId) => Ok(await batches.RemoveUniversityGroupAsync(universityGroupId));

    [HttpPut("university-groups/{universityGroupId:long}/coordinator")]
    [HasPermission(P.BatchManage)]
    public async Task<ActionResult<SoftSkillBatchDto>> SetCoordinator(long universityGroupId, SetCoordinatorRequest request) =>
        Ok(await batches.SetCoordinatorAsync(universityGroupId, request));
}

/// <summary>The signed-in student's own Soft Skill programme.</summary>
[ApiController]
[Route("api/v1/softskill/me")]
public class MySoftSkillController(IStudentSoftSkillService me) : ControllerBase
{
    [HttpGet("eligibility")]
    [HasPermission(P.ProgrammeView)]
    public async Task<ActionResult<SoftSkillEligibilityDto>> Eligibility() => Ok(await me.GetEligibilityAsync());

    [HttpGet("open-batches")]
    [HasPermission(P.ProgrammeView)]
    public async Task<ActionResult<List<OpenBatchDto>>> OpenBatches() => Ok(await me.ListOpenBatchesAsync());

    /// <summary>The latest application with the training, or 204 when the student has none.</summary>
    [HttpGet]
    [HasPermission(P.SelfView)]
    public async Task<IActionResult> Mine()
    {
        var mine = await me.GetMineAsync();
        return mine is null ? NoContent() : Ok(mine);
    }

    [HttpPost("apply")]
    [HasPermission(P.Apply)]
    public async Task<ActionResult<MySoftSkillDto>> Apply(SoftSkillApplyRequest request) => Ok(await me.ApplyAsync(request));

    [HttpPost("withdraw")]
    [HasPermission(P.Apply)]
    public async Task<ActionResult<MySoftSkillDto>> Withdraw() => Ok(await me.WithdrawAsync());

    [HttpPost("leave")]
    [HasPermission(P.SelfLeave)]
    public async Task<ActionResult<MySoftSkillDto>> Leave(SoftSkillLeaveRequest request) => Ok(await me.RequestLeaveAsync(request));

    [HttpPost("report")]
    [HasPermission(P.SelfReport)]
    public async Task<ActionResult<MySoftSkillDto>> Report(LearningReportRequest request) => Ok(await me.SubmitReportAsync(request));
}

[ApiController]
[Route("api/v1/softskill/applications")]
public class SoftSkillApplicationsController(ISoftSkillApplicationAdminService apps) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.ApplicationView)]
    public async Task<ActionResult<List<SoftSkillApplicationListItemDto>>> List([FromQuery] long? batchId, [FromQuery] long? universityGroupId, [FromQuery] string? status) =>
        Ok(await apps.ListAsync(batchId, universityGroupId, status));

    [HttpGet("{id:long}")]
    [HasPermission(P.ApplicationView)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Get(long id) => Ok(await apps.GetAsync(id));

    [HttpPost("{id:long}/shortlist")]
    [HasPermission(P.ApplicationScreen)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Shortlist(long id) => Ok(await apps.ShortlistAsync(id));

    [HttpPost("{id:long}/score")]
    [HasPermission(P.ApplicationScreen)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Score(long id, ScreeningScoreRequest request) => Ok(await apps.ScoreAsync(id, request));

    [HttpPost("{id:long}/waitlist")]
    [HasPermission(P.ApplicationScreen)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Waitlist(long id) => Ok(await apps.WaitlistAsync(id));

    [HttpPost("{id:long}/reject")]
    [HasPermission(P.ApplicationScreen)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Reject(long id, ReasonRequest request) => Ok(await apps.RejectAsync(id, request));

    [HttpPost("{id:long}/select")]
    [HasPermission(P.ApplicationSelect)]
    public async Task<ActionResult<SoftSkillApplicationDto>> Select(long id) => Ok(await apps.SelectAsync(id));
}

/// <summary>Trainer registration (the teacher's own) and its review by the Commission (cl.12).</summary>
[ApiController]
[Route("api/v1/softskill/trainers")]
public class SoftSkillTrainersController(ITrainerService trainers) : ControllerBase
{
    [HttpGet("me")]
    [HasPermission(P.TrainerApply)]
    public async Task<ActionResult<MyTrainerDto>> Mine() => Ok(await trainers.GetMineAsync());

    [HttpPost("me/apply")]
    [HasPermission(P.TrainerApply)]
    public async Task<ActionResult<MyTrainerDto>> Apply(TrainerApplyRequest request) => Ok(await trainers.ApplyAsync(request));

    [HttpGet]
    [HasPermission(P.TrainerReview, P.BatchManage)]
    public async Task<ActionResult<List<TrainerDto>>> List([FromQuery] string? status, [FromQuery] long? universityId) =>
        Ok(await trainers.ListAsync(status, universityId));

    [HttpGet("{id:long}")]
    [HasPermission(P.TrainerReview)]
    public async Task<ActionResult<TrainerDto>> Get(long id) => Ok(await trainers.GetAsync(id));

    [HttpPost("{id:long}/approve")]
    [HasPermission(P.TrainerReview)]
    public async Task<ActionResult<TrainerDto>> Approve(long id, TrainerDecisionRequest request) => Ok(await trainers.ApproveAsync(id, request));

    [HttpPost("{id:long}/reject")]
    [HasPermission(P.TrainerReview)]
    public async Task<ActionResult<TrainerDto>> Reject(long id, TrainerDecisionRequest request) => Ok(await trainers.RejectAsync(id, request));

    [HttpPost("{id:long}/suspend")]
    [HasPermission(P.TrainerReview)]
    public async Task<ActionResult<TrainerDto>> Suspend(long id, TrainerDecisionRequest request) => Ok(await trainers.SuspendAsync(id, request));

    [HttpPost("{id:long}/tot")]
    [HasPermission(P.TrainerReview)]
    public async Task<ActionResult<TrainerDto>> RecordTot(long id, TotRequest request) => Ok(await trainers.RecordTotAsync(id, request));
}

/// <summary>One university group's schedule, attendance, assessments and reports.</summary>
[ApiController]
[Route("api/v1/softskill/university-groups")]
public class SoftSkillUniversityGroupsController(ISoftSkillUniversityGroupService universityGroups) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.UniversityGroupViewAll, P.UniversityGroupViewAssigned, P.UniversityGroupManageAll)]
    public async Task<ActionResult<List<UniversityGroupSummaryDto>>> List([FromQuery] long? batchId) => Ok(await universityGroups.ListAsync(batchId));

    [HttpGet("{id:long}")]
    [HasPermission(P.UniversityGroupViewAll, P.UniversityGroupViewAssigned, P.UniversityGroupManageAll, P.MonitorVisit, P.MonitoringManage)]
    public async Task<ActionResult<UniversityGroupDetailDto>> Get(long id) => Ok(await universityGroups.GetAsync(id));

    [HttpPost("{id:long}/sessions")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> AddSession(long id, SessionRequest request) => Ok(await universityGroups.AddSessionAsync(id, request));

    [HttpPut("{id:long}/sessions/{sessionId:long}")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> UpdateSession(long id, long sessionId, SessionRequest request) =>
        Ok(await universityGroups.UpdateSessionAsync(id, sessionId, request));

    [HttpPost("{id:long}/sessions/{sessionId:long}/cancel")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> CancelSession(long id, long sessionId) => Ok(await universityGroups.CancelSessionAsync(id, sessionId));

    [HttpPost("{id:long}/sessions/{sessionId:long}/attendance")]
    [HasPermission(P.SessionAttendance, P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> Attendance(long id, long sessionId, AttendanceSheetRequest request) =>
        Ok(await universityGroups.SaveAttendanceAsync(id, sessionId, request));

    [HttpPost("{id:long}/leave/decide")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> DecideLeave(long id, LeaveDecisionRequest request) => Ok(await universityGroups.DecideLeaveAsync(id, request));

    [HttpPost("{id:long}/assessments")]
    [HasPermission(P.AssessmentEnter, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> CreateAssessment(long id, AssessmentRequest request) => Ok(await universityGroups.CreateAssessmentAsync(id, request));

    [HttpPost("{id:long}/assessments/{assessmentId:long}/results")]
    [HasPermission(P.AssessmentEnter, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> Results(long id, long assessmentId, ResultsRequest request) =>
        Ok(await universityGroups.SaveResultsAsync(id, assessmentId, request));

    [HttpPost("{id:long}/reports/review")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> ReviewReport(long id, ReportReviewRequest request) => Ok(await universityGroups.ReviewReportAsync(id, request));

    [HttpPost("{id:long}/final-report")]
    [HasPermission(P.UniversityGroupCoordinate, P.UniversityGroupManageAll)]
    public async Task<ActionResult<UniversityGroupDetailDto>> FinalReport(long id, FinalReportRequest request) => Ok(await universityGroups.SubmitFinalReportAsync(id, request));
}

/// <summary>The Commission's list of trainees, cancellation and certificates.</summary>
[ApiController]
[Route("api/v1/softskill/trainees")]
public class SoftSkillTraineesController(ISoftSkillTraineeService trainees) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.UniversityGroupViewAll)]
    public async Task<ActionResult<List<TraineeListItemDto>>> List([FromQuery] long? batchId, [FromQuery] long? universityGroupId) =>
        Ok(await trainees.ListAsync(batchId, universityGroupId));

    [HttpGet("{id:long}")]
    [HasPermission(P.UniversityGroupViewAll)]
    public async Task<ActionResult<TraineeDto>> Get(long id) => Ok(await trainees.GetAsync(id));

    [HttpPost("{id:long}/cancel")]
    [HasPermission(P.TraineeCancel)]
    public async Task<ActionResult<TraineeDto>> Cancel(long id, ReasonRequest request) => Ok(await trainees.CancelAsync(id, request));

    [HttpPost("{id:long}/outcome")]
    [HasPermission(P.CertificateIssue)]
    public async Task<ActionResult<TraineeDto>> Outcome(long id) => Ok(await trainees.IssueOutcomeAsync(id));
}

/// <summary>cl.10 stipends: paid or withheld by the finance section.</summary>
[ApiController]
[Route("api/v1/softskill/stipends")]
public class SoftSkillStipendsController(ISoftSkillStipendService stipends) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.StipendView)]
    public async Task<ActionResult<List<StipendRowDto>>> List([FromQuery] long? batchId, [FromQuery] string? status) =>
        Ok(await stipends.ListAsync(batchId, status));

    [HttpPost("{traineeId:long}/{no:int}/pay")]
    [HasPermission(P.StipendPay)]
    public async Task<ActionResult<StipendRowDto>> Pay(long traineeId, int no) => Ok(await stipends.PayAsync(traineeId, no));

    [HttpPost("{traineeId:long}/{no:int}/withhold")]
    [HasPermission(P.StipendPay)]
    public async Task<ActionResult<StipendRowDto>> Withhold(long traineeId, int no, ReasonRequest request) =>
        Ok(await stipends.WithholdAsync(traineeId, no, request));

    [HttpPost("{traineeId:long}/{no:int}/release")]
    [HasPermission(P.StipendPay)]
    public async Task<ActionResult<StipendRowDto>> Release(long traineeId, int no) => Ok(await stipends.ReleaseAsync(traineeId, no));
}

/// <summary>cl.14 (ঘ): monitoring visits by Commission officers and academia/industry experts, and every university group's health.</summary>
[ApiController]
[Route("api/v1/softskill/monitoring")]
public class SoftSkillMonitoringController(ISoftSkillMonitoringService monitoring) : ControllerBase
{
    [HttpGet("monitors")]
    [HasPermission(P.MonitoringManage)]
    public async Task<ActionResult<List<MonitorOptionDto>>> Monitors() => Ok(await monitoring.ListMonitorsAsync());

    [HttpGet("health")]
    [HasPermission(P.MonitoringManage)]
    public async Task<ActionResult<List<UniversityGroupHealthDto>>> Health([FromQuery] long? batchId) => Ok(await monitoring.GetHealthAsync(batchId));

    [HttpGet("visits")]
    [HasPermission(P.MonitoringManage)]
    public async Task<ActionResult<List<VisitDto>>> List([FromQuery] long? batchId, [FromQuery] long? universityGroupId, [FromQuery] string? status) =>
        Ok(await monitoring.ListAsync(batchId, universityGroupId, status));

    [HttpGet("visits/mine")]
    [HasPermission(P.MonitorVisit, P.MonitoringManage)]
    public async Task<ActionResult<List<VisitDto>>> Mine() => Ok(await monitoring.ListMineAsync());

    [HttpGet("visits/{id:long}")]
    [HasPermission(P.MonitorVisit, P.MonitoringManage)]
    public async Task<ActionResult<VisitDto>> Get(long id) => Ok(await monitoring.GetAsync(id));

    [HttpPost("visits")]
    [HasPermission(P.MonitoringManage)]
    public async Task<ActionResult<VisitDto>> Plan(PlanVisitRequest request) => Ok(await monitoring.PlanAsync(request));

    [HttpPost("visits/{id:long}/cancel")]
    [HasPermission(P.MonitoringManage)]
    public async Task<ActionResult<VisitDto>> Cancel(long id) => Ok(await monitoring.CancelAsync(id));

    /// <summary>Only the visit's monitor, or someone who manages monitoring, can file it.</summary>
    [HttpPost("visits/{id:long}/report")]
    [HasPermission(P.MonitorVisit, P.MonitoringManage)]
    public async Task<ActionResult<VisitDto>> Report(long id, VisitReportRequest request) => Ok(await monitoring.SubmitReportAsync(id, request));
}

/// <summary>cl.17 honoraria of trainers, coordinators and monitors.</summary>
[ApiController]
[Route("api/v1/softskill/honoraria")]
public class SoftSkillHonorariaController(ISoftSkillHonorariumService honoraria) : ControllerBase
{
    [HttpGet]
    [HasPermission(P.HonorariumView)]
    public async Task<ActionResult<List<HonorariumRowDto>>> List([FromQuery] long? batchId, [FromQuery] string? status, [FromQuery] string? kind) =>
        Ok(await honoraria.ListAsync(batchId, status, kind));

    [HttpGet("me")]
    [HasPermission(P.TrainerApply, P.MonitorVisit)]
    public async Task<ActionResult<List<HonorariumRowDto>>> Mine() => Ok(await honoraria.ListMineAsync());

    [HttpPost("{kind}/{sourceId:long}/pay")]
    [HasPermission(P.HonorariumPay)]
    public async Task<ActionResult<HonorariumRowDto>> Pay(string kind, long sourceId) => Ok(await honoraria.PayAsync(kind, sourceId));

    [HttpGet("rates")]
    [HasPermission(P.HonorariumView, P.HonorariumRates)]
    public async Task<ActionResult<HonorariumRatesDto>> Rates() => Ok(await honoraria.GetRatesAsync());

    [HttpPut("rates")]
    [HasPermission(P.HonorariumRates)]
    public async Task<ActionResult<HonorariumRatesDto>> SetRates(HonorariumRatesRequest request) => Ok(await honoraria.SetRatesAsync(request));
}
