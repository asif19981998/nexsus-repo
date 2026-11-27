using Microsoft.AspNetCore.Mvc;
using Nexus.API.Security;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Security;
using Nexus.Application.Services.Internships;

namespace Nexus.API.Controllers;

/// <summary>Policy numbers and lookups every screen needs. Any signed-in user.</summary>
[ApiController]
[Route("api/v1/internship/setup")]
public class InternshipSetupController(ISetupService setup) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SetupDto>> Get() => Ok(await setup.GetSetupAsync());
}

[ApiController]
[Route("api/v1/internship/overview")]
public class InternshipOverviewController(IOverviewService overview) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OverviewView)]
    public async Task<ActionResult<OverviewDto>> Get() => Ok(await overview.GetAsync());
}

[ApiController]
[Route("api/v1/internship/batches")]
public class InternshipBatchesController(IBatchService batches) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.BatchView)]
    public async Task<ActionResult<List<BatchDto>>> List() => Ok(await batches.ListAsync());

    /// <summary>Create a batch. Over the cl.6 limits the response is 400 with code "needs_override" until an override reason is sent.</summary>
    [HttpPost]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<BatchDto>> Create(CreateBatchRequest request) => Ok(await batches.CreateAsync(request));

    [HttpPost("{id:long}/status")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<BatchDto>> SetStatus(long id, SetBatchStatusRequest request) => Ok(await batches.SetStatusAsync(id, request));
}

/// <summary>The signed-in student's own programme, application and internship.</summary>
[ApiController]
[Route("api/v1/internship/me")]
public class MyInternshipController(IStudentInternshipService me, IBatchService batches) : ControllerBase
{
    [HttpGet("eligibility")]
    [HasPermission(Permissions.ProgrammeView)]
    public async Task<ActionResult<EligibilityDto>> Eligibility() => Ok(await me.GetEligibilityAsync());

    [HttpGet("open-batches")]
    [HasPermission(Permissions.ProgrammeView)]
    public async Task<ActionResult<List<BatchDto>>> OpenBatches() => Ok(await batches.ListOpenAsync());

    /// <summary>The latest application with its internship, or 204 when the student has none.</summary>
    [HttpGet("application")]
    [HasPermission(Permissions.SelfView)]
    public async Task<IActionResult> Application()
    {
        var app = await me.GetMyApplicationAsync();
        return app is null ? NoContent() : Ok(app);
    }

    [HttpPost("apply")]
    [HasPermission(Permissions.ApplicationApply)]
    public async Task<ActionResult<ApplicationDetailDto>> Apply(ApplyRequest request) => Ok(await me.ApplyAsync(request));

    [HttpPost("withdraw")]
    [HasPermission(Permissions.ApplicationApply)]
    public async Task<ActionResult<ApplicationDetailDto>> Withdraw() => Ok(await me.WithdrawAsync());

    // No check-in/check-out here: interns punch from the mobile app (api/v1/attendance/me/punch).

    [HttpPost("leave")]
    [HasPermission(Permissions.SelfAttendance)]
    public async Task<ActionResult<ApplicationDetailDto>> RequestLeave(LeaveRequestBody request) => Ok(await me.RequestLeaveAsync(request));

    [HttpPost("bills/{month:regex(^\\d{{4}}-\\d{{2}}$)}/submit")]
    [HasPermission(Permissions.SelfBill)]
    public async Task<ActionResult<ApplicationDetailDto>> SubmitBill(string month, SubmitMonthlyReportRequest request) =>
        Ok(await me.SubmitBillAsync(month, request));

    [HttpPost("report")]
    [HasPermission(Permissions.SelfReport)]
    public async Task<ActionResult<ApplicationDetailDto>> SubmitReport(SubmitReportRequest request) => Ok(await me.SubmitReportAsync(request));
}

[ApiController]
[Route("api/v1/internship/applications")]
public class InternshipApplicationsController(IApplicationAdminService apps) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ApplicationView)]
    public async Task<ActionResult<List<ApplicationListItemDto>>> List([FromQuery] long? batchId, [FromQuery] string? status) =>
        Ok(await apps.ListAsync(batchId, status));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.ApplicationView)]
    public async Task<ActionResult<ApplicationDetailDto>> Get(long id) => Ok(await apps.GetAsync(id));

    [HttpPost("{id:long}/shortlist")]
    [HasPermission(Permissions.ApplicationScreen)]
    public async Task<ActionResult<ApplicationDetailDto>> Shortlist(long id) => Ok(await apps.ShortlistAsync(id));

    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.ApplicationScreen)]
    public async Task<ActionResult<ApplicationDetailDto>> Reject(long id, ReasonRequest request) => Ok(await apps.RejectAsync(id, request));

    [HttpPost("{id:long}/waitlist")]
    [HasPermission(Permissions.ApplicationScreen)]
    public async Task<ActionResult<ApplicationDetailDto>> Waitlist(long id) => Ok(await apps.WaitlistAsync(id));

    [HttpPost("{id:long}/exam")]
    [HasPermission(Permissions.ApplicationExam)]
    public async Task<ActionResult<ApplicationDetailDto>> RecordExam(long id, RecordExamRequest request) => Ok(await apps.RecordExamAsync(id, request));

    [HttpPost("{id:long}/select")]
    [HasPermission(Permissions.ApplicationSelect)]
    public async Task<ActionResult<ApplicationDetailDto>> Select(long id) => Ok(await apps.SelectAsync(id));

    [HttpPost("{id:long}/place")]
    [HasPermission(Permissions.InternPlace)]
    public async Task<ActionResult<ApplicationDetailDto>> Place(long id, PlaceRequest request) => Ok(await apps.PlaceAsync(id, request));
}

/// <summary>Running and finished internships. Mentors reach only the interns assigned to them.</summary>
[ApiController]
[Route("api/v1/internship/interns")]
public class InternsController(IInternService interns) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.InternViewAll, Permissions.InternViewAssigned)]
    public async Task<ActionResult<List<InternListItemDto>>> List([FromQuery] string? status) => Ok(await interns.ListAsync(status));

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.InternViewAll, Permissions.InternViewAssigned)]
    public async Task<ActionResult<ApplicationDetailDto>> Get(long id) => Ok(await interns.GetAsync(id));

    [HttpPost("{id:long}/transfer")]
    [HasPermission(Permissions.InternTransfer)]
    public async Task<ActionResult<ApplicationDetailDto>> Transfer(long id, TransferRequest request) => Ok(await interns.TransferAsync(id, request));

    [HttpPost("{id:long}/leave/decide")]
    [HasPermission(Permissions.AttendanceManage)]
    public async Task<ActionResult<ApplicationDetailDto>> DecideLeave(long id, DecideLeaveRequest request) => Ok(await interns.DecideLeaveAsync(id, request));

    [HttpPost("{id:long}/absence")]
    [HasPermission(Permissions.AttendanceManage)]
    public async Task<ActionResult<ApplicationDetailDto>> MarkAbsence(long id, MarkAbsenceRequest request) => Ok(await interns.MarkAbsenceAsync(id, request));

    /// <summary>Admin only (checked in the service): correct a day's sign-in and sign-out times.</summary>
    [HttpPost("{id:long}/attendance/times")]
    [HasPermission(Permissions.AttendanceManage)]
    public async Task<ActionResult<ApplicationDetailDto>> SetAttendanceTimes(long id, SetAttendanceTimesRequest request) => Ok(await interns.SetAttendanceTimesAsync(id, request));

    [HttpPost("{id:long}/report/review")]
    [HasPermission(Permissions.ReportReview)]
    public async Task<ActionResult<ApplicationDetailDto>> ReviewReport(long id, ReviewReportRequest request) => Ok(await interns.ReviewReportAsync(id, request));

    [HttpPost("{id:long}/evaluation")]
    [HasPermission(Permissions.EvaluationSubmit)]
    public async Task<ActionResult<ApplicationDetailDto>> SubmitEvaluation(long id, EvaluationRequest request) => Ok(await interns.SubmitEvaluationAsync(id, request));

    /// <summary>Score the month's report (cl.10). The month's allowance is paid only after this.</summary>
    [HttpPost("{id:long}/bills/{month:regex(^\\d{{4}}-\\d{{2}}$)}/evaluate")]
    [HasPermission(Permissions.EvaluationSubmit)]
    public async Task<ActionResult<ApplicationDetailDto>> EvaluateMonth(long id, string month, EvaluateMonthRequest request) =>
        Ok(await interns.EvaluateMonthAsync(id, month, request));

    [HttpPost("{id:long}/complete")]
    [HasPermission(Permissions.InternComplete)]
    public async Task<ActionResult<ApplicationDetailDto>> Complete(long id) => Ok(await interns.CompleteAsync(id));

    /// <summary>Issue the certificate, or record "Not certified" with reasons (cl.15).</summary>
    [HttpPost("{id:long}/outcome")]
    [HasPermission(Permissions.CertificateIssue)]
    public async Task<IActionResult> IssueOutcome(long id)
    {
        var (detail, result) = await interns.IssueOutcomeAsync(id);
        return Ok(new { application = detail, result });
    }

    [HttpPost("{id:long}/terminate")]
    [HasPermission(Permissions.InternTerminate)]
    public async Task<ActionResult<ApplicationDetailDto>> Terminate(long id, ReasonRequest request) => Ok(await interns.TerminateAsync(id, request));
}

[ApiController]
[Route("api/v1/internship/bills")]
public class InternshipBillsController(IBillService bills) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.BillView)]
    public async Task<ActionResult<List<BillRowDto>>> List([FromQuery] string? status) => Ok(await bills.ListAsync(status));

    /// <summary>Mark a submitted bill paid. Returns the refreshed list for the given status filter.</summary>
    [HttpPost("{applicationId:long}/{month:regex(^\\d{{4}}-\\d{{2}}$)}/pay")]
    [HasPermission(Permissions.BillPay)]
    public async Task<ActionResult<List<BillRowDto>>> Pay(long applicationId, string month, [FromQuery] string? status) =>
        Ok(await bills.PayAsync(applicationId, month, status));
}

/// <summary>Mobile attendance for the signed-in intern: today, punch and history.</summary>
[ApiController]
[Route("api/v1/attendance/me")]
[HasPermission(Permissions.SelfAttendance)]
public class MyAttendanceController(IAttendanceService attendance, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("today")]
    public async Task<ActionResult<AttendanceDayDto>> Today() => Ok(await attendance.GetTodayAsync());

    [HttpPost("punch")]
    public async Task<ActionResult<AttendancePunchResultDto>> Punch(PunchRequest request)
    {
        // The selfie must be a file this server really holds, not just a well-formed path.
        if (UploadedFile.IsValidPath(request.PhotoPath)
            && !System.IO.File.Exists(Path.Combine(env.ContentRootPath, "files", Path.GetFileName(request.PhotoPath!))))
            throw new Nexus.BuildingBlocks.Exceptions.BadRequestException("The photo could not be found. Take it again.", "ATTENDANCE_PHOTO_REQUIRED", null);
        return Ok(await attendance.PunchAsync(request));
    }

    /// <summary>One month of the intern's own register, day by day. <c>month</c> is yyyy-MM (default this month).</summary>
    [HttpGet("month")]
    public async Task<ActionResult<AdminInternMonthDto>> Month([FromQuery] string? month) => Ok(await attendance.GetMyMonthAsync(month));

    [HttpGet("history")]
    public async Task<ActionResult<AttendanceHistoryDto>> History([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await attendance.GetHistoryAsync(from, to, page, pageSize));
}

/// <summary>
/// Attendance for staff. Reading the register follows the intern scope: <see cref="Permissions.InternViewAll"/> sees every
/// intern, <see cref="Permissions.InternViewAssigned"/> only the interns they mentor. Holidays and punch locations need
/// <see cref="Permissions.BatchManage"/>.
/// </summary>
[ApiController]
[Route("api/v1/attendance/admin")]
public class AttendanceAdminController(IAttendanceService attendance) : ControllerBase
{
    /// <summary>Every active intern in the caller's scope for one day (default today), optionally one department.</summary>
    [HttpGet("day")]
    [HasPermission(Permissions.InternViewAll, Permissions.InternViewAssigned)]
    public async Task<ActionResult<AdminAttendanceDayDto>> Day([FromQuery] DateOnly? date, [FromQuery] long? departmentId) =>
        Ok(await attendance.ListDayAsync(date, departmentId));

    /// <summary>One intern's month, day by day. <c>month</c> is yyyy-MM (default this month).</summary>
    [HttpGet("interns/{applicationId:long}")]
    [HasPermission(Permissions.InternViewAll, Permissions.InternViewAssigned)]
    public async Task<ActionResult<AdminInternMonthDto>> InternMonth(long applicationId, [FromQuery] string? month) =>
        Ok(await attendance.GetInternMonthAsync(applicationId, month));

    [HttpGet("holidays")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<List<HolidayDto>>> Holidays() => Ok(await attendance.ListHolidaysAsync());

    [HttpPost("holidays")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<HolidayDto>> AddHoliday(HolidayRequest request) => Ok(await attendance.AddHolidayAsync(request));

    [HttpDelete("holidays/{id:long}")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<IActionResult> RemoveHoliday(long id)
    {
        await attendance.RemoveHolidayAsync(id);
        return NoContent();
    }

    [HttpGet("departments")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<List<DepartmentLocationDto>>> Departments() => Ok(await attendance.ListDepartmentLocationsAsync());

    [HttpPut("departments/{id:long}/location")]
    [HasPermission(Permissions.BatchManage)]
    public async Task<ActionResult<DepartmentLocationDto>> SetLocation(long id, DepartmentLocationRequest request) =>
        Ok(await attendance.SetDepartmentLocationAsync(id, request));
}
