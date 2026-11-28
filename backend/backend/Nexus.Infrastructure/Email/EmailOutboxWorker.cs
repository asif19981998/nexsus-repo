using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Services.Common;
using Nexus.Domain.Entities.Common;

namespace Nexus.Infrastructure.Email;

/// <summary>Sends one email over SMTP.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var mail = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(o.FromAddress) ? o.Smtp.UserName : o.FromAddress, o.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody ?? "",
            IsBodyHtml = true,
        };
        mail.To.Add(new MailAddress(message.ToAddress, message.ToName));

        using var client = new SmtpClient(o.Smtp.Host, o.Smtp.Port)
        {
            EnableSsl = o.Smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(o.Smtp.UserName, o.Smtp.Password),
        };
        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// Sends queued emails from the EmailMessages table. A failed email is tried again later (1, 5, 15, 60, 240 minutes),
/// then marked Failed. Requests never wait for SMTP, and an SMTP outage never fails a request.
/// </summary>
public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopes, IEmailSender sender, IOptionsMonitor<EmailOptions> options, ILogger<EmailOutboxWorker> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly int[] RetryMinutes = [1, 5, 15, 60, 240];
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var warned = false;
        while (!ct.IsCancellationRequested)
        {
            var o = options.CurrentValue;
            if (o.Enabled && !string.IsNullOrWhiteSpace(o.Smtp.Host))
            {
                try { await SendDueAsync(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { log.LogError(ex, "Email outbox pass failed."); }
            }
            else if (!warned)
            {
                log.LogWarning("Email sending is off (Email:Enabled is false or Email:Smtp:Host is empty). Emails stay queued.");
                warned = true;
            }

            try { await Task.Delay(Interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SendDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var now = DateTime.UtcNow;

        var due = await db.EmailMessages
            .Where(e => e.SentAt == null && !e.Failed && e.NextAttemptAt <= now)
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var e in due)
        {
            e.Attempts++;
            try
            {
                await sender.SendAsync(e, ct);
                e.SentAt = DateTime.UtcNow;
                e.LastError = null;
                if (e.IsSensitive) e.HtmlBody = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                e.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (e.Attempts >= EmailMessage.MaxAttempts)
                {
                    e.Failed = true;
                    if (e.IsSensitive) e.HtmlBody = null;
                    log.LogError(ex, "Email {Id} to {To} failed for good after {Attempts} attempts.", e.Id, e.ToAddress, e.Attempts);
                }
                else
                {
                    e.NextAttemptAt = DateTime.UtcNow.AddMinutes(RetryMinutes[Math.Min(e.Attempts - 1, RetryMinutes.Length - 1)]);
                    log.LogWarning("Email {Id} to {To} failed (attempt {Attempts}): {Error}", e.Id, e.ToAddress, e.Attempts, ex.Message);
                }
            }
            // Save after each email so a crash never sends the same one twice.
            await db.SaveChangesAsync(ct);
        }
    }
}
