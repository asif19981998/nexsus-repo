using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.SoftSkills;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.SoftSkills;

/// <summary>
/// cl.17: honoraria of trainers (per session held), coordinators (per university group, once the final report is sent) and
/// monitors (per visit reported), at the rates the Commission sets from the government's notices.
/// What is due is worked out from those records; paying fixes the amount and issues a slip.
/// </summary>
public interface ISoftSkillHonorariumService
{
    Task<List<HonorariumRowDto>> ListAsync(long? batchId, string? status, string? kind);

    /// <summary>The caller's own honoraria (trainer, coordinator or monitor).</summary>
    Task<List<HonorariumRowDto>> ListMineAsync();

    Task<HonorariumRowDto> PayAsync(string kind, long sourceId);
    Task<HonorariumRatesDto> GetRatesAsync();
    Task<HonorariumRatesDto> SetRatesAsync(HonorariumRatesRequest request);
}

public static class HonorariumStatus
{
    public const string RateNotSet = "Rate not set";
    public const string Due = "Due";
    public const string Paid = "Paid";
}

public sealed class SoftSkillHonorariumService(
    IUnitOfWork uow, IRepository<SoftSkillPolicy> policies, ICurrentUserService me, IClock clock,
    IAuditService audit, INotificationService notify) : ISoftSkillHonorariumService
{
    private const decimal MaxRate = 1_000_000;

    public async Task<List<HonorariumRowDto>> ListAsync(long? batchId, string? status, string? kind)
    {
        var rows = await BuildAsync(batchId, null);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") rows = rows.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(kind) && kind != "all") rows = rows.Where(r => string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase)).ToList();
        return rows;
    }

    public async Task<List<HonorariumRowDto>> ListMineAsync() => await BuildAsync(null, me.RequireUserId());

    public async Task<HonorariumRowDto> PayAsync(string kind, long sourceId)
    {
        if (!Enum.TryParse<HonorariumKind>(kind, true, out var k) || !Enum.IsDefined(k)) throw new BadRequestException("Unknown honorarium kind.");
        var row = (await BuildAsync(null, null)).FirstOrDefault(r => r.Kind == k.ToString() && r.SourceId == sourceId)
            ?? throw new NotFoundException("Honorarium", $"{k} {sourceId}");
        if (row.Status == HonorariumStatus.Paid) throw new ConflictException("This honorarium is already paid.");
        if (row.Status == HonorariumStatus.RateNotSet) throw new BadRequestException("Set the honorarium rate first (cl.17).");

        var paid = new SoftSkillHonorarium
        {
            Kind = k,
            SourceId = sourceId,
            PersonId = row.Person.Id,
            UniversityGroupId = row.UniversityGroupId,
            Amount = row.Amount,
            PaidAt = clock.UtcNow,
            PaidById = me.UserId,
            PaidByName = me.FullName,
            SlipNumber = SoftSkillHonorarium.SlipNumberFor(k, sourceId),
        };
        await uow.SoftSkillHonoraria.AddAsync(paid);
        await audit.LogAsync("softskill.honorarium.pay", "SoftSkillUniversityGroup", row.UniversityGroupId, $"{k} {sourceId}: {row.Person.Name}, {row.Amount} BDT, slip {paid.SlipNumber}");
        await notify.NotifyAsync(row.Person.Id, "Honorarium paid",
            $"{row.Amount:N0} BDT for {row.Description} ({row.University}). Slip {paid.SlipNumber}.",
            k == HonorariumKind.MonitoringVisit ? Links.SoftSkillMonitoring : Links.SoftSkillTrainer);
        await uow.SaveChangesAsync();
        return (await BuildAsync(null, row.Person.Id)).First(r => r.Kind == row.Kind && r.SourceId == sourceId);
    }

    public async Task<HonorariumRatesDto> GetRatesAsync()
    {
        var p = await uow.SoftSkillSetup.GetPolicyAsync();
        return new HonorariumRatesDto(p.TrainerRatePerSession, p.CoordinatorRatePerUniversityGroup, p.ExpertRatePerVisit);
    }

    public async Task<HonorariumRatesDto> SetRatesAsync(HonorariumRatesRequest r)
    {
        decimal Rate(decimal? value, string name) =>
            value is { } v && v >= 0 && v <= MaxRate ? Math.Round(v, 2) : throw new BadRequestException($"Enter the {name} rate, 0 or more.");
        var current = await uow.SoftSkillSetup.GetPolicyAsync();
        var policy = current.Id == 0 ? null : await policies.GetByIdAsync(current.Id);
        if (policy is null)
        {
            policy = new SoftSkillPolicy();
            await policies.AddAsync(policy);
        }
        policy.TrainerRatePerSession = Rate(r.TrainerPerSession, "trainer");
        policy.CoordinatorRatePerUniversityGroup = Rate(r.CoordinatorPerUniversityGroup, "coordinator");
        policy.ExpertRatePerVisit = Rate(r.ExpertPerVisit, "monitor");
        await audit.LogAsync("softskill.honorarium.rates", "SoftSkillPolicy", policy.Id,
            $"Trainer {policy.TrainerRatePerSession}/session, coordinator {policy.CoordinatorRatePerUniversityGroup}/university group, monitor {policy.ExpertRatePerVisit}/visit");
        await uow.SaveChangesAsync();
        return new HonorariumRatesDto(policy.TrainerRatePerSession, policy.CoordinatorRatePerUniversityGroup, policy.ExpertRatePerVisit);
    }

    // ---------- helpers ----------

    /// <summary>Every honorarium due or paid, from held sessions, reported university groups and visits, plus the paid rows.</summary>
    private async Task<List<HonorariumRowDto>> BuildAsync(long? batchId, long? personId)
    {
        var policy = await uow.SoftSkillSetup.GetPolicyAsync();
        var paid = (await uow.SoftSkillHonoraria.ListPaidAsync(batchId, personId)).ToDictionary(h => (h.Kind, h.SourceId));
        var used = new HashSet<(HonorariumKind, long)>();
        var rows = new List<HonorariumRowDto>();

        HonorariumRowDto Row(HonorariumKind kind, long sourceId, Domain.Entities.Identity.User person, string role, SoftSkillUniversityGroup c, DateOnly date, string description, decimal rate)
        {
            var p = paid.GetValueOrDefault((kind, sourceId));
            if (p is not null) used.Add((kind, sourceId));
            var status = p is not null ? HonorariumStatus.Paid : rate <= 0 ? HonorariumStatus.RateNotSet : HonorariumStatus.Due;
            return new HonorariumRowDto(kind.ToString(), sourceId, SoftSkillMapper.PersonRef(person), role, c.Id, c.University.Name, c.Batch.Code,
                date, description, p?.Amount ?? (int)Math.Round(rate), status, p?.PaidAt, p?.PaidByName, p?.SlipNumber);
        }

        foreach (var s in await uow.SoftSkillHonoraria.ListHeldSessionsAsync(batchId, personId))
            rows.Add(Row(HonorariumKind.Session, s.Id, s.Trainer, "Trainer", s.UniversityGroup, s.Date,
                $"{s.Topic.Name}, {SoftSkillMapper.Time(s.StartTime)}–{SoftSkillMapper.Time(s.EndTime)}", policy.TrainerRatePerSession));
        foreach (var c in await uow.SoftSkillHonoraria.ListReportedUniversityGroupsAsync(batchId, personId))
            rows.Add(Row(HonorariumKind.Coordination, c.Id, c.Coordinator!, "Coordinator", c, DateOnly.FromDateTime(c.FinalReportAt!.Value.AddHours(6)),
                $"Coordinated {c.Batch.Name}", policy.CoordinatorRatePerUniversityGroup));
        foreach (var v in await uow.SoftSkillHonoraria.ListSubmittedVisitsAsync(batchId, personId))
            rows.Add(Row(HonorariumKind.MonitoringVisit, v.Id, v.Monitor, "Monitor", v.UniversityGroup, v.VisitedOn ?? v.PlannedOn,
                "Monitoring visit", policy.ExpertRatePerVisit));

        // Paid to someone the source no longer names (a coordinator replaced later): still the person's payment.
        foreach (var p in paid.Values.Where(p => !used.Contains((p.Kind, p.SourceId))))
            rows.Add(new HonorariumRowDto(p.Kind.ToString(), p.SourceId, SoftSkillMapper.PersonRef(p.Person), p.Kind switch
                {
                    HonorariumKind.Session => "Trainer",
                    HonorariumKind.Coordination => "Coordinator",
                    _ => "Monitor",
                }, p.UniversityGroupId, p.UniversityGroup.University.Name, p.UniversityGroup.Batch.Code, DateOnly.FromDateTime(p.PaidAt.AddHours(6)), "Paid earlier",
                p.Amount, HonorariumStatus.Paid, p.PaidAt, p.PaidByName, p.SlipNumber));

        return rows.OrderBy(r => r.Status == HonorariumStatus.Paid ? 1 : 0).ThenByDescending(r => r.Date).ThenBy(r => r.Person.Name).ToList();
    }
}
