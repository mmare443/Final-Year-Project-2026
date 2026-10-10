using System.Net;
using System.Net.Mail;

namespace LCC_CMS_Api.Services;

public sealed class AdmissionEmailOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public string User { get; set; } = "";

    public string Password { get; set; } = "";

    public string From { get; set; } = "";

    public bool EnableSsl { get; set; } = true;

    public string PickupDirectory { get; set; } = "";
}

public interface IAdmissionEmailSender
{
    Task<(bool Sent, string? Error)> SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken);
}

public sealed class AdmissionEmailSender : IAdmissionEmailSender
{
    private readonly AdmissionEmailOptions _options;
    private readonly IWebHostEnvironment _environment;

    public AdmissionEmailSender(Microsoft.Extensions.Options.IOptions<AdmissionEmailOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<(bool Sent, string? Error)> SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return (false, "The application has no email address.");
        }

        var pickup = _options.PickupDirectory?.Trim() ?? "";
        if (pickup.Length > 0)
        {
            var folder = Path.IsPathRooted(pickup)
                ? pickup
                : Path.Combine(_environment.ContentRootPath, pickup);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml");
            var content = $"To: {to}{Environment.NewLine}Subject: {subject}{Environment.NewLine}{Environment.NewLine}{body}";
            await File.WriteAllTextAsync(file, content, cancellationToken);
            return (true, null);
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
        {
            return (false, "Email delivery is not configured.");
        }

        try
        {
            using var message = new MailMessage(_options.From.Trim(), to.Trim(), subject, body);
            using var client = new SmtpClient(_options.Host.Trim(), _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrWhiteSpace(_options.User))
            {
                client.Credentials = new NetworkCredential(_options.User, _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            var safe = ex.Message ?? "Email delivery failed.";
            if (!string.IsNullOrEmpty(_options.Password))
            {
                safe = safe.Replace(_options.Password, "[redacted]", StringComparison.Ordinal);
            }
            if (safe.Length > 500) safe = safe[..500];
            return (false, safe);
        }
    }
}
