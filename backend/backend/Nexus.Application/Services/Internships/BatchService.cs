using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Application.Dtos.Internships;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Application.Services.Common;
using Nexus.BuildingBlocks.Exceptions;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Enums;

namespace Nexus.Application.Services.Internships;

public interface IBatchService
{
    Task<List<BatchDto>> ListAsync();

    /// <summary>Batches a student can apply to now.</summary>
    Task<List<BatchDto>> ListOpenAsync();

    Task<BatchDto> CreateAsync(CreateBatchRequest request);
    Task<BatchDto> SetStatusAsync(long id, SetBatchStatusRequest request);
}

public sealed class BatchService(
    IUnitOfWork uow, ISetupService setup, IPermissionService permissions, ICurrentUserService me, IClock clock, IAuditService audit) : IBatchService
{
    public async Task<List<BatchDto>> ListAsync()
    {
        var batches = await uow.Batches.ListAsync();
        var taken = await uow.Batches.TakenSeatsAsync();
        return batches.Select(b => ToDto(b, taken.GetValueOrDefault(b.Id))).ToList();
    }

    public async Task<List<BatchDto>> ListOpenAsync() =>
        (await ListAsync()).Where(b => b.Status == BatchStatus.ApplicationsOpen.Label()).ToList();

    public async Task<BatchDto> CreateAsync(CreateBatchRequest r)
    {
        var policy = (await setup.GetSnapshotAsync()).Policy;

        // Count batches per year up front, since the check is synchronous.
        var years = new Dictionary<int, int>();
        if (r.StartDate is { } sd) years[sd.Year] = await uow.Batches.CountInYearAsync(sd.Year, includeCancelled: false);

        var check = InternshipRules.ValidateNewBatch(
            new InternshipRules.BatchDraft(r.Name, r.Capacity, r.DurationMonths, r.ApplyFrom, r.ApplyTo, r.StartDate),
            y => years.GetValueOrDefault(y), policy);

        if (check.Errors.Count > 0) throw new BadRequestException(check.Errors[0]);

        if (check.NeedsOverride.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(r.OverrideReason))
                throw new BadRequestException(check.NeedsOverride[0], "needs_override", check.NeedsOverride);
            if (!await permissions.HasAsync(Permissions.BatchOverride))
                throw new ForbiddenException("You do not have permission to override the cl.6 limits.");
        }

        var year = check.Year!.Value;
        var seq = await uow.Batches.CountInYearAsync(year, includeCancelled: true) + 1;
        var batch = new InternshipBatch
        {
            Code = $"UGC-INT-{year}-B{seq}",
            Name = r.Name!.Trim(),
            Year = year,
            Status = BatchStatus.Draft,
            Capacity = r.Capacity!.Value,
            DurationMonths = r.DurationMonths!.Value,
            ApplyFrom = r.ApplyFrom!.Value,
            ApplyTo = r.ApplyTo!.Value,
            StartDate = r.StartDate!.Value,
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
        };
        if (check.NeedsOverride.Count > 0)
        {
            batch.OverrideReason = r.OverrideReason!.Trim();
            batch.OverrideLimits = string.Join('\n', check.NeedsOverride);
            batch.OverrideById = me.UserId;
            batch.OverrideAt = clock.UtcNow;
        }

        await uow.Batches.AddAsync(batch);
        await uow.SaveChangesAsync();
        await audit.LogAsync(batch.OverrideReason is null ? "batch.create" : "batch.create_override", "InternshipBatch", batch.Id,
            batch.OverrideReason is null ? batch.Code : $"{batch.Code} — override: {batch.OverrideReason}");
        await uow.SaveChangesAsync();
        return ToDto(batch, 0);
    }

    public async Task<BatchDto> SetStatusAsync(long id, SetBatchStatusRequest r)
    {
        if (!StatusLabels.TryParseBatch(r.Status, out var status)) throw new BadRequestException("Unknown batch status.");
        var batch = await uow.Batches.GetByIdAsync(id) ?? throw new NotFoundException("Batch", id);

        var allowed = batch.Status switch
        {
            BatchStatus.Draft => new[] { BatchStatus.ApplicationsOpen, BatchStatus.Cancelled },
            BatchStatus.ApplicationsOpen => [BatchStatus.Selection, BatchStatus.Cancelled],
            BatchStatus.Selection => [BatchStatus.Running],
            BatchStatus.Running => [BatchStatus.Completed],
            _ => [],
        };
        if (!allowed.Contains(status))
            throw new BadRequestException($"A batch in \"{batch.Status.Label()}\" cannot move to \"{status.Label()}\".");

        var from = batch.Status;
        batch.Status = status;
        await audit.LogAsync("batch.status", "InternshipBatch", batch.Id, $"{batch.Code}: {from.Label()} → {status.Label()}");
        await uow.SaveChangesAsync();
        return ToDto(batch, await uow.Batches.TakenSeatsAsync(batch.Id));
    }

    internal static BatchDto ToDto(InternshipBatch b, int taken) =>
        new(b.Id, b.Code, b.Name, b.Year, b.Status.Label(), b.Capacity, b.DurationMonths, b.ApplyFrom, b.ApplyTo, b.StartDate, b.Note,
            b.OverrideReason is null ? null : new BatchOverrideDto(b.OverrideReason, InternshipMapper.SplitLines(b.OverrideLimits), b.OverrideAt),
            new SeatsDto(b.Capacity, taken, Math.Max(0, b.Capacity - taken)));
}
