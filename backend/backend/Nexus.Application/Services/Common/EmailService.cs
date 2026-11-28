using System.Net;
using Microsoft.Extensions.Options;
using Nexus.Application.Common;
using Nexus.Application.Data;
using Nexus.Domain.Entities.Common;

namespace Nexus.Application.Services.Common;

/// <summary>The "Email" configuration section. Secrets (the SMTP password) belong in user-secrets or environment variables.</summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>When false, emails are still queued but the worker does not send them.</summary>
    public bool Enabled { get; set; }

    /// <summary>The front end's address; links in emails are built from it.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:3000";

    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "UGC Nexus";

    public SmtpSettings Smtp { get; set; } = new();

    public sealed class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
    }
}

public interface IEmailService
{
    /// <summary>Queues an email to a user. Saved with the caller's next SaveChanges. Skipped when the user is inactive or has no email.</summary>
    Task QueueToUserAsync(long userId, string subject, string? message, string? link, string? linkText = null);

    /// <summary>Queues an email to an address. <paramref name="sensitive"/>: the body is removed after sending.</summary>
    Task QueueAsync(string toAddress, string? toName, long? userId, string subject, string htmlBody, bool sensitive = false);

    /// <summary>A full link into the front end from a path such as <c>/login</c>.</summary>
    string AppUrl(string path);

    /// <summary>The standard email layout around a greeting, a message and an optional button.</summary>
    string Render(string? toName, string heading, string? message, string? link, string? linkText, string? extraHtml = null);
}

public sealed class EmailService(IUnitOfWork uow, IClock clock, IOptions<EmailOptions> options) : IEmailService
{
    public async Task QueueToUserAsync(long userId, string subject, string? message, string? link, string? linkText = null)
    {
        if (await uow.Users.GetContactAsync(userId) is not { } to) return;
        await QueueAsync(to.Email, to.FullName, userId, subject,
            Render(to.FullName, subject, message, link is null ? null : AppUrl(link), linkText ?? "Open Nexus"));
    }

    public Task QueueAsync(string toAddress, string? toName, long? userId, string subject, string htmlBody, bool sensitive = false)
    {
        var now = clock.UtcNow;
        return uow.Emails.AddAsync(new EmailMessage
        {
            UserId = userId,
            ToAddress = toAddress,
            ToName = toName,
            Subject = subject.Length > 200 ? subject[..200] : subject,
            HtmlBody = htmlBody,
            IsSensitive = sensitive,
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }

    public string AppUrl(string path) => options.Value.AppBaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

    public string Render(string? toName, string heading, string? message, string? link, string? linkText, string? extraHtml = null)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var paragraphs = string.Join("", (message ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"<p style=\"margin:0 0 12px\">{E(p)}</p>"));
        var button = link is null ? "" :
            $"<p style=\"margin:20px 0\"><a href=\"{E(link)}\" style=\"background:#0f172a;color:#fff;text-decoration:none;padding:10px 18px;border-radius:8px;display:inline-block\">{E(linkText ?? "Open Nexus")}</a></p>" +
            $"<p style=\"margin:0 0 12px;font-size:12px;color:#64748b\">If the button does not work, open this link: {E(link)}</p>";

        return $"""
            <!doctype html>
            <html><body style="margin:0;padding:24px;background:#f1f5f9;font-family:Segoe UI,Arial,sans-serif;color:#0f172a">
              <div style="max-width:560px;margin:0 auto;background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:24px">
                <h2 style="margin:0 0 16px;font-size:20px">{E(heading)}</h2>
                <p style="margin:0 0 12px">Dear {E(string.IsNullOrWhiteSpace(toName) ? "user" : toName)},</p>
                {paragraphs}
                {extraHtml ?? ""}
                {button}
                <p style="margin:24px 0 0;font-size:12px;color:#64748b">University Grants Commission of Bangladesh · Nexus. This is an automatic email; please do not reply.</p>
              </div>
            </body></html>
            """;
    }
}
