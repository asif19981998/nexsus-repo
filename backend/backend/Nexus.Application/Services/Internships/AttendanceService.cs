using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

// ---------- DTOs (JSON is camelCase; the mobile app reads these names) ----------

/// <summary>One day of attendance. Used for "today", every history item and the body of a punch result.</summary>
public sealed record AttendanceDayDto(
    string WorkDate, string TimeZoneId, string DayType,
    string? ShiftName, DateTime? ScheduledStartAtUtc, DateTime? ScheduledEndAtUtc,
    string LocationMode, bool RequiresCurrentLocation, string? LocationDisplayName,
    string SessionStatus, string AllowedAction,
    DateTime? FirstCheckIn, DateTime? LastCheckOut,
    int? LateMinutes, int? EarlyLeaveMinutes, int? WorkedMinutes,
    bool Manual = false);

public sealed record AttendancePunchResultDto(
    bool Accepted, string? RejectionReasonCode, string? Message, AttendanceDayDto Today);

public sealed record AttendanceHistoryDto(
    IReadOnlyList<AttendanceDayDto> Items, int Page, int PageSize, int TotalCount, bool HasNextPage);

public sealed class PunchLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
}

public sealed class PunchRequest
{
    public string? IdempotencyKey { get; set; }
    public DateTime? ClientCapturedAt { get; set; }
    public PunchLocation? Location { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>Server path returned by <c>POST /api/v1/file/upload</c> for the front-camera selfie. Required.</summary>
    public string? PhotoPath { get; set; }
}

public sealed record HolidayDto(long Id, string Date, string Name);

/// <summary>The weekly off days, Sunday first, e.g. ["Friday", "Saturday"].</summary>
public sealed record WeekendDto(IReadOnlyList<string> Days);
public sealed class WeekendRequest { public List<string>? Days { get; set; } }
public sealed class HolidayRequest { public DateOnly? Date { get; set; } public string? Name { get; set; } }
public sealed record DepartmentLocationDto(long Id, string Name, double? Latitude, double? Longitude, int RadiusMeters);
public sealed class DepartmentLocationRequest { public double? Latitude { get; set; } public double? Longitude { get; set; } public int? RadiusMeters { get; set; } }

/// <summary>One intern on one day, as the admin Attendance page lists them.</summary>
public sealed record AdminAttendanceRowDto(
    long ApplicationId, string Name, string Email, string Department,
    string Kind, string Label, string? CheckIn, string? CheckOut, int? LateMinutes, int? WorkedMinutes,
    string? CheckInPhoto, string? CheckOutPhoto, double? CheckInDistanceMeters, string? Note, string? Reason,
    bool Manual, string? EditedBy);

public sealed record AdminAttendanceDayDto(
    string Date, string DayType, string? HolidayName,
    int Total, int Present, int Absent, int Leave, int LeaveRequested, int NotSignedIn,
    IReadOnlyList<AdminAttendanceRowDto> Rows);

/// <summary>One calendar day of one intern, for the admin monthly view.</summary>
public sealed record AdminMonthDayDto(
    string Date, string DayType, string? HolidayName, string Kind, string Label,
    string? CheckIn, string? CheckOut, int? LateMinutes, int? WorkedMinutes,
    string? CheckInPhoto, string? CheckOutPhoto, double? CheckInDistanceMeters, string? Note, string? Reason,
    double? CheckInLatitude, double? CheckInLongitude, double? CheckOutLatitude, double? CheckOutLongitude,
    bool Manual, string? EditedBy);

public sealed record AdminInternMonthDto(
    long ApplicationId, string Name, string Email, string Department, string Month,
    string ShiftStart, string ShiftEnd,
    int WorkingDays, int Present, int Late, int Absent, int Leave, int WorkedMinutes,
    IReadOnlyList<AdminMonthDayDto> Days);

public interface IAttendanceService
{
    /// <summary>One intern's whole month, day by day (admin) - the same picture the intern sees in the app.</summary>
    Task<AdminInternMonthDto> GetInternMonthAsync(long applicationId, string? month);

    /// <summary>The signed-in intern's own month, the same picture staff see. Works during and after the internship.</summary>
    Task<AdminInternMonthDto> GetMyMonthAsync(string? month);

    /// <summary>Every active intern for one day (admin). A day off lists everyone with no status.</summary>
    Task<AdminAttendanceDayDto> ListDayAsync(DateOnly? date, long? departmentId);

    Task<AttendanceDayDto> GetTodayAsync();
    Task<AttendancePunchResultDto> PunchAsync(PunchRequest request);
    Task<AttendanceHistoryDto> GetHistoryAsync(DateOnly? from, DateOnly? to, int page, int pageSize);

    Task<List<HolidayDto>> ListHolidaysAsync();
    Task<HolidayDto> AddHolidayAsync(HolidayRequest request);
    Task RemoveHolidayAsync(long id);
    Task<WeekendDto> GetWeekendAsync();

    /// <summary>Sets the weekly off days. At least one day of the week must stay a working day.</summary>
    Task<WeekendDto> SetWeekendAsync(WeekendRequest request);
    Task<List<DepartmentLocationDto>> ListDepartmentLocationsAsync();
    Task<DepartmentLocationDto> SetDepartmentLocationAsync(long id, DepartmentLocationRequest request);
}

/// <summary>
/// Mobile attendance for interns. Records go into the existing AttendanceRecords table; the shift comes from the policy row,
/// holidays from the Holidays table, weekend and leave from the existing intern rules. A punch counts only when the phone is
/// within the department's radius.
/// </summary>
public sealed class AttendanceService(IUnitOfWork uow, ICurrentUserService me, IClock clock, IAuditService audit, IPermissionService permissions) : IAttendanceService
{
    private const double MaxAccuracyMeters = 150;

    // ---------- mobile ----------

    public async Task<AttendanceDayDto> GetTodayAsync()
    {
        var ctx = await LoadAsync();
        return await BuildDayAsync(ctx, clock.Today);
    }

    public async Task<AttendancePunchResultDto> PunchAsync(PunchRequest r)
    {
        var key = r.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 64)
            throw new BadRequestException("This attendance attempt could not be identified.", "ATTENDANCE_INVALID_IDEMPOTENCY_KEY", null);

        var photo = r.PhotoPath?.Trim();
        if (!UploadedFile.IsValidPath(photo))
            throw new BadRequestException("Take a photo of yourself to record attendance.", "ATTENDANCE_PHOTO_REQUIRED", null);

        var ctx = await LoadAsync();
        var today = clock.Today;
        var rec = ctx.Internship.Attendance.FirstOrDefault(a => a.Date == today);

        // The same key again means the app retried: report the state, change nothing.
        if (rec is not null && (rec.CheckInKey == key || rec.CheckOutKey == key))
            return new AttendancePunchResultDto(true, null, null, await BuildDayAsync(ctx, today));

        var before = await BuildDayAsync(ctx, today);
        if (before.DayType != "WORKING")
            throw new BadRequestException("There is no attendance to record today.", "NO_ACTIVE_SCHEDULE", null);
        if (before.AllowedAction == "NONE")
            throw new BadRequestException("Your attendance for today is already complete.", before.SessionStatus == "CHECKED_OUT" ? "ALREADY_CHECKED_OUT" : "PUNCH_WINDOW_CLOSED", null);

        // ---- location ----
        double? distance = null;
        var dept = ctx.Department;
        if (dept.Latitude is { } dLat && dept.Longitude is { } dLng)
        {
            var loc = r.Location ?? throw new BadRequestException("Your location is needed to record attendance.", "ATTENDANCE_INVALID_LOCATION", null);
            if (!Geo.IsValid(loc.Latitude, loc.Longitude))
                throw new BadRequestException("The location could not be validated.", "ATTENDANCE_INVALID_LOCATION", null);
            if (loc.AccuracyMeters is > MaxAccuracyMeters)
                return Rejected("LOW_ACCURACY", "Your location is not accurate enough yet. Move to an open area and retry.", before);

            distance = Geo.DistanceMeters(loc.Latitude, loc.Longitude, dLat, dLng);
            if (distance > dept.RadiusMeters)
                return Rejected("OUTSIDE_GEOFENCE",
                    "You are not at your center. Please punch from your center location.", before);
        }

        var nowUtc = clock.UtcNow;
        var local = clock.LocalTime;
        if (before.AllowedAction == "CHECK_IN")
        {
            if (rec is null)
            {
                rec = new AttendanceRecord { Date = today };
                ctx.Internship.Attendance.Add(rec);
            }
            // Signing in on a day with a pending leave request cancels the request (same as the web app).
            rec.Status = AttendanceStatus.Present;
            rec.Approved = false;
            rec.Note = null;
            rec.CheckIn = local;
            rec.CheckInAtUtc = nowUtc;
            rec.CheckInLatitude = r.Location?.Latitude;
            rec.CheckInLongitude = r.Location?.Longitude;
            rec.CheckInAccuracyMeters = r.Location?.AccuracyMeters;
            rec.CheckInDistanceMeters = distance;
            rec.CheckInKey = key;
            rec.CheckInPhotoPath = photo;
            rec.DeviceId = Trim(r.DeviceId, 100);
            rec.AppVersion = Trim(r.AppVersion, 30);
            await audit.LogAsync("attendance.check_in", "InternshipApplication", ctx.App.Id, $"{today:yyyy-MM-dd} mobile");
        }
        else
        {
            rec!.CheckOut = local;
            rec.CheckOutAtUtc = nowUtc;
            rec.CheckOutLatitude = r.Location?.Latitude;
            rec.CheckOutLongitude = r.Location?.Longitude;
            rec.CheckOutAccuracyMeters = r.Location?.AccuracyMeters;
            rec.CheckOutDistanceMeters = distance;
            rec.CheckOutKey = key;
            rec.CheckOutPhotoPath = photo;
            rec.DeviceId ??= Trim(r.DeviceId, 100);
            await audit.LogAsync("attendance.check_out", "InternshipApplication", ctx.App.Id, $"{today:yyyy-MM-dd} mobile");
        }

        await uow.SaveChangesAsync();
        return new AttendancePunchResultDto(true, null, null, await BuildDayAsync(ctx, today));
    }

    public async Task<AttendanceHistoryDto> GetHistoryAsync(DateOnly? from, DateOnly? to, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new BadRequestException("The paging values are invalid.", "ATTENDANCE_INVALID_PAGING", null);

        var ctx = await LoadAsync();
        var today = clock.Today;
        var start = Max(from ?? today.AddDays(-30), ctx.Internship.StartDate);
        var end = Min(to ?? today, Min(today, ctx.Internship.EndDate));
        if (start > end) return new AttendanceHistoryDto([], page, pageSize, 0, false);

        var total = end.DayNumber - start.DayNumber + 1;
        var days = Enumerable.Range(0, total).Select(i => end.AddDays(-i)) // newest first
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var calendar = await CalendarAsync(ctx.Policy, days.Min(), days.Max());
        var items = new List<AttendanceDayDto>();
        foreach (var d in days) items.Add(await BuildDayAsync(ctx, d, calendar));
        return new AttendanceHistoryDto(items, page, pageSize, total, page * pageSize < total);
    }

    // ---------- admin ----------

    public async Task<AdminAttendanceDayDto> ListDayAsync(DateOnly? date, long? departmentId)
    {
        var today = clock.Today;
        var day = date ?? today;
        var policy = await uow.Setup.GetPolicyAsync();
        var holiday = await uow.Holidays.GetSingleAsync(h => h.Date == day);
        var weekend = WorkCalendar.ParseWeekend(policy.WeekendDays);
        var dayType = holiday is not null ? "HOLIDAY" : weekend.Contains(day.DayOfWeek) ? "WEEKLY_OFF" : "WORKING";

        var apps = await uow.Applications.ListInternsAsync([ApplicationStatus.Active], await MentorScopeAsync());
        var names = await EditorNamesAsync(apps.SelectMany(a => a.Internship!.Attendance).Where(r => r.Date == day).Select(r => r.DecidedById));
        var rows = new List<AdminAttendanceRowDto>();
        foreach (var app in apps.OrderBy(a => a.Student.FullName))
        {
            var it = app.Internship!;
            if (day < it.StartDate || day > it.EndDate) continue;
            if (departmentId is { } d && it.DepartmentId != d) continue;

            var cell = BuildCell(it, day, dayType, holiday?.Name, policy, today, names);
            rows.Add(new AdminAttendanceRowDto(
                app.Id, app.Student.FullName, app.Student.Email ?? "", it.Department.Name, cell.Kind, cell.Label,
                cell.CheckIn, cell.CheckOut, cell.LateMinutes, cell.WorkedMinutes,
                cell.CheckInPhoto, cell.CheckOutPhoto, cell.CheckInDistanceMeters, cell.Note, cell.Reason, cell.Manual, cell.EditedBy));
        }

        int Count(Func<string, bool> f) => rows.Count(x => f(x.Kind));
        return new AdminAttendanceDayDto(
            day.ToString("yyyy-MM-dd"), dayType, holiday?.Name,
            rows.Count, Count(k => k == "present"), Count(k => k.StartsWith("absent")), Count(k => k == "leave"),
            Count(k => k == "leave-requested"), Count(k => k == "open"), rows);
    }

    public async Task<AdminInternMonthDto> GetInternMonthAsync(long applicationId, string? month)
    {
        var apps = await uow.Applications.ListInternsAsync(InternshipRules.InternStatuses.ToList(), await MentorScopeAsync());
        var app = apps.FirstOrDefault(a => a.Id == applicationId)
            ?? throw new NotFoundException("That intern could not be found.");
        return await BuildMonthAsync(app, month);
    }

    public async Task<AdminInternMonthDto> GetMyMonthAsync(string? month)
    {
        var app = await uow.Applications.GetLatestForStudentAsync(me.RequireUserId());
        if (app?.Internship is null || !InternshipRules.InternStatuses.Contains(app.Status))
            throw new BadRequestException("Your attendance starts once you are placed in a department.", "ATTENDANCE_NOT_INTERNING", null);
        return await BuildMonthAsync(app, month);
    }

    private async Task<AdminInternMonthDto> BuildMonthAsync(InternshipApplication app, string? month)
    {
        var today = clock.Today;
        var first = new DateOnly(today.Year, today.Month, 1);
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out first))
                throw new BadRequestException("The month is invalid. Use yyyy-MM.", "ATTENDANCE_INVALID_MONTH", null);
        }
        var last = first.AddMonths(1).AddDays(-1);

        var it = app.Internship!;
        var policy = await uow.Setup.GetPolicyAsync();
        var holidayList = await uow.Holidays.FindAsync(h => h.Date >= first && h.Date <= last);
        var holidays = holidayList.ToDictionary(h => h.Date, h => h.Name);
        var weekend = WorkCalendar.ParseWeekend(policy.WeekendDays);

        var names = await EditorNamesAsync(it.Attendance.Where(r => r.Date >= first && r.Date <= last).Select(r => r.DecidedById));
        var days = new List<AdminMonthDayDto>();
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            if (d < it.StartDate || d > it.EndDate) continue;
            holidays.TryGetValue(d, out var holidayName);
            var dayType = holidayName is not null ? "HOLIDAY" : weekend.Contains(d.DayOfWeek) ? "WEEKLY_OFF" : "WORKING";
            days.Add(BuildCell(it, d, dayType, holidayName, policy, today, names));
        }

        // Future days are shown as scheduled but are not counted.
        var counted = days.Where(x => string.CompareOrdinal(x.Date, today.ToString("yyyy-MM-dd")) <= 0).ToList();
        return new AdminInternMonthDto(
            app.Id, app.Student.FullName, app.Student.Email ?? "", it.Department.Name, first.ToString("yyyy-MM"),
            policy.ShiftStart.ToString("HH:mm"), policy.ShiftEnd.ToString("HH:mm"),
            counted.Count(x => x.DayType == "WORKING"),
            counted.Count(x => x.Kind == "present"),
            counted.Count(x => x.Kind == "present" && x.LateMinutes > 0),
            counted.Count(x => x.Kind.StartsWith("absent")),
            counted.Count(x => x.Kind == "leave"),
            counted.Sum(x => x.WorkedMinutes ?? 0),
            days.OrderByDescending(x => x.Date).ToList());
    }

    /// <summary>One intern on one day: status, times, lateness, worked time and photos.</summary>
    private static AdminMonthDayDto BuildCell(Internship it, DateOnly day, string dayType, string? holidayName, InternshipPolicy policy, DateOnly today, IReadOnlyDictionary<long, string> names)
    {
        var rec = it.Attendance.FirstOrDefault(a => a.Date == day);
        string kind, label;
        if (dayType != "WORKING" && rec is null) (kind, label) = ("off", holidayName ?? "Weekly off");
        else
        {
            var st = InternshipRules.DayStatus(rec, day, today);
            (kind, label) = st.Kind == "open" ? ("open", day > today ? "Upcoming" : "Not signed in") : (st.Kind, st.Label);
        }

        int? late = InternshipRules.LateMinutes(rec, policy), worked = null;
        if (rec is { CheckIn: not null } r)
        {
            var inAt = r.CheckInAtUtc ?? BangladeshClock.ToUtc(day, r.CheckIn!.Value);
            if (r.CheckOut is not null)
            {
                var outAt = r.CheckOutAtUtc ?? BangladeshClock.ToUtc(day, r.CheckOut.Value);
                worked = Math.Max(0, (int)Math.Round((outAt - inAt).TotalMinutes));
            }
        }

        // A day an admin set by hand: DecidedById is written only by the admin actions, never by a punch.
        var manual = rec is { DecidedById: not null, Status: AttendanceStatus.Present or AttendanceStatus.Absent };
        return new AdminMonthDayDto(
            day.ToString("yyyy-MM-dd"), dayType, holidayName, kind, label,
            rec?.CheckIn?.ToString("HH:mm"), rec?.CheckOut?.ToString("HH:mm"), late, worked,
            rec?.CheckInPhotoPath, rec?.CheckOutPhotoPath, rec?.CheckInDistanceMeters, rec?.Note, rec?.Reason,
            rec?.CheckInLatitude, rec?.CheckInLongitude, rec?.CheckOutLatitude, rec?.CheckOutLongitude,
            manual, manual && rec!.DecidedById is { } by && names.TryGetValue(by, out var n) ? n : null);
    }

    /// <summary>Names of the admins who changed records, so the pages can say who edited a day.</summary>
    private async Task<Dictionary<long, string>> EditorNamesAsync(IEnumerable<long?> ids)
    {
        var result = new Dictionary<long, string>();
        foreach (var id in ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct())
            if (await uow.Users.GetAsync(id) is { } u) result[id] = u.User.FullName;
        return result;
    }

    public async Task<List<HolidayDto>> ListHolidaysAsync() =>
        (await uow.Holidays.GetAllAsync()).OrderBy(h => h.Date).Select(ToDto).ToList();

    public async Task<HolidayDto> AddHolidayAsync(HolidayRequest r)
    {
        if (r.Date is not { } date) throw new BadRequestException("Choose a date.");
        var name = r.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new BadRequestException("Give the holiday a name.");
        if (await uow.Holidays.AnyAsync(h => h.Date == date)) throw new ConflictException("That date is already a holiday.");
        var holiday = new Holiday { Date = date, Name = name.Length > 150 ? name[..150] : name };
        await uow.Holidays.AddAsync(holiday);
        await audit.LogAsync("holiday.add", "Holiday", null, $"{date:yyyy-MM-dd} {holiday.Name}");
        await uow.SaveChangesAsync();
        return ToDto(holiday);
    }

    public async Task RemoveHolidayAsync(long id)
    {
        var holiday = await uow.Holidays.GetByIdAsync(id) ?? throw new NotFoundException("Holiday", id);
        uow.Holidays.Remove(holiday);
        await audit.LogAsync("holiday.remove", "Holiday", id, $"{holiday.Date:yyyy-MM-dd} {holiday.Name}");
        await uow.SaveChangesAsync();
    }

    public async Task<WeekendDto> GetWeekendAsync() =>
        ToDto(WorkCalendar.ParseWeekend((await uow.Setup.GetPolicyAsync()).WeekendDays));

    public async Task<WeekendDto> SetWeekendAsync(WeekendRequest r)
    {
        var days = new HashSet<DayOfWeek>();
        foreach (var name in r.Days ?? [])
        {
            if (!Enum.TryParse<DayOfWeek>(name?.Trim(), ignoreCase: true, out var day) || !Enum.IsDefined(day) || int.TryParse(name, out _))
                throw new BadRequestException($"\"{name}\" is not a day of the week.");
            days.Add(day);
        }
        if (days.Count == 0) throw new BadRequestException("Choose at least one weekly off day.");
        if (days.Count == 7) throw new BadRequestException("At least one day of the week must be a working day.");

        var policy = await uow.Setup.GetPolicyForUpdateAsync();
        var before = policy.WeekendDays;
        policy.WeekendDays = WorkCalendar.FormatWeekend(days);
        await audit.LogAsync("weekend.set", "InternshipPolicy", policy.Id, $"{before} -> {policy.WeekendDays}");
        await uow.SaveChangesAsync();
        return ToDto(days);
    }

    private static WeekendDto ToDto(IReadOnlySet<DayOfWeek> days) => new(days.Order().Select(d => d.ToString()).ToList());

    public async Task<List<DepartmentLocationDto>> ListDepartmentLocationsAsync() =>
        (await uow.Setup.GetDepartmentsAsync(activeOnly: false)).Select(ToDto).ToList();

    public async Task<DepartmentLocationDto> SetDepartmentLocationAsync(long id, DepartmentLocationRequest r)
    {
        var dept = await uow.Departments.GetByIdAsync(id) ?? throw new NotFoundException("Department", id);
        // Both coordinates or neither; neither switches the location check off for this department.
        if ((r.Latitude is null) != (r.Longitude is null)) throw new BadRequestException("Give both latitude and longitude, or neither.");
        if (r.Latitude is { } lat && r.Longitude is { } lng && !Geo.IsValid(lat, lng)) throw new BadRequestException("The coordinates are not valid.");
        if (r.RadiusMeters is < 20 or > 5000) throw new BadRequestException("The radius must be between 20 and 5000 metres.");

        dept.Latitude = r.Latitude;
        dept.Longitude = r.Longitude;
        if (r.RadiusMeters is { } radius) dept.RadiusMeters = radius;
        await audit.LogAsync("department.location", "Department", id, $"{dept.Latitude},{dept.Longitude} r={dept.RadiusMeters}");
        await uow.SaveChangesAsync();
        return ToDto(dept);
    }

    // ---------- the day ----------

    private sealed record Ctx(InternshipApplication App, Internship Internship, Department Department, InternshipPolicy Policy);

    private async Task<Ctx> LoadAsync()
    {
        var app = await uow.Applications.GetLatestForStudentAsync(me.RequireUserId());
        if (app is null || app.Status != ApplicationStatus.Active || app.Internship is null)
            throw new BadRequestException("Your employee account could not be found.", "ATTENDANCE_EMPLOYEE_NOT_FOUND", null);
        var it = app.Internship;
        // After a transfer the latest placement is where the intern works now.
        var dept = it.Placements.OrderByDescending(p => p.From).ThenByDescending(p => p.Id).FirstOrDefault()?.Department ?? it.Department;
        return new Ctx(app, it, dept, await uow.Setup.GetPolicyAsync());
    }

    /// <summary>The policy's weekly off days and the holidays declared between the two dates.</summary>
    private async Task<WorkCalendar> CalendarAsync(InternshipPolicy policy, DateOnly from, DateOnly to) =>
        WorkCalendar.From(policy, (await uow.Holidays.FindAsync(h => h.Date >= from && h.Date <= to)).Select(h => h.Date));

    private async Task<AttendanceDayDto> BuildDayAsync(Ctx c, DateOnly date, WorkCalendar? calendar = null)
    {
        calendar ??= await CalendarAsync(c.Policy, date, date);
        var today = clock.Today;
        var rec = c.Internship.Attendance.FirstOrDefault(a => a.Date == date);
        var inPeriod = date >= c.Internship.StartDate && date <= c.Internship.EndDate;

        var dayType =
            !inPeriod ? "UNSCHEDULED" :
            calendar.IsHoliday(date) ? "HOLIDAY" :
            calendar.IsWeekend(date) ? "WEEKLY_OFF" :
            rec?.Status == AttendanceStatus.Leave ? "LEAVE" : "WORKING";

        string status, action = "NONE";
        if (dayType != "WORKING")
        {
            status = rec?.Status == AttendanceStatus.Present ? (rec.CheckOut is null ? "CHECKED_IN" : "CHECKED_OUT") : "SCHEDULED";
        }
        else if (rec?.Status == AttendanceStatus.Present)
        {
            if (rec.CheckOut is null)
            {
                // A day left open: past days are INCOMPLETE and can no longer be punched.
                status = date < today ? "INCOMPLETE" : "CHECKED_IN";
                if (date == today) action = "CHECK_OUT";
            }
            else status = "CHECKED_OUT";
        }
        else if (rec?.Status == AttendanceStatus.Absent) status = "ABSENT";
        else if (date < today) status = "ABSENT";
        else
        {
            status = "SCHEDULED";
            // Today only. A pending leave request on today is cancelled by signing in.
            if (date == today) action = "CHECK_IN";
        }

        var p = c.Policy;
        var shiftStart = BangladeshClock.ToUtc(date, p.ShiftStart);
        var shiftEnd = BangladeshClock.ToUtc(date, p.ShiftEnd);
        var firstIn = rec is { CheckIn: not null } ? rec.CheckInAtUtc ?? BangladeshClock.ToUtc(date, rec.CheckIn.Value) : (DateTime?)null;
        var lastOut = rec is { CheckOut: not null } ? rec.CheckOutAtUtc ?? BangladeshClock.ToUtc(date, rec.CheckOut.Value) : (DateTime?)null;

        int? late = InternshipRules.LateMinutes(rec, p);
        int? early = lastOut is { } lo ? Math.Max(0, (int)Math.Round((shiftEnd - lo).TotalMinutes)) : null;
        int? worked = firstIn is { } a && lastOut is { } b ? Math.Max(0, (int)Math.Round((b - a).TotalMinutes)) : null;

        var hasLocation = c.Department.Latitude is not null && c.Department.Longitude is not null;
        return new AttendanceDayDto(
            date.ToString("yyyy-MM-dd"), BangladeshClock.ZoneId, dayType,
            p.ShiftName, shiftStart, shiftEnd,
            hasLocation ? "REQUIRED" : "ANY", hasLocation, hasLocation ? c.Department.Name : null,
            status, action, firstIn, lastOut, late, early, worked,
            rec is { DecidedById: not null, Status: AttendanceStatus.Present or AttendanceStatus.Absent });
    }

    // ---------- helpers ----------

    private static AttendancePunchResultDto Rejected(string code, string message, AttendanceDayDto today) =>
        new(false, code, message, today);

    /// <summary>Same scope as the Interns pages: null sees every intern, a mentor's own id sees only the interns they mentor.</summary>
    private async Task<long?> MentorScopeAsync()
    {
        if (await permissions.HasAsync(Permissions.InternViewAll)) return null;
        if (await permissions.HasAsync(Permissions.InternViewAssigned)) return me.RequireUserId();
        throw new ForbiddenException("You do not have permission to view interns.");
    }

    private static string? Trim(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static HolidayDto ToDto(Holiday h) => new(h.Id, h.Date.ToString("yyyy-MM-dd"), h.Name);
    private static DepartmentLocationDto ToDto(Department d) => new(d.Id, d.Name, d.Latitude, d.Longitude, d.RadiusMeters);
}

/// <summary>The shape of a path the upload endpoint hands out: <c>/files/{guid}.jpg</c>.</summary>
public static class UploadedFile
{
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 100 || !path.StartsWith("/files/", StringComparison.Ordinal)) return false;
        var name = path["/files/".Length..];
        var dot = name.LastIndexOf('.');
        if (dot < 1 || !Guid.TryParseExact(name[..dot], "D", out _) && !Guid.TryParseExact(name[..dot], "N", out _)) return false;
        return ImageExtensions.Contains(name[dot..].ToLowerInvariant());
    }
}

/// <summary>Distance between two points on the earth (haversine).</summary>
public static class Geo
{
    private const double EarthRadiusMeters = 6_371_000;

    public static bool IsValid(double lat, double lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && !double.IsNaN(lat) && !double.IsNaN(lng);

    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}
