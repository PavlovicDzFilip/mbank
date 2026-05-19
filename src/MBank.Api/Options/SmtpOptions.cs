namespace MBank.Api.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 25;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool EnableSsl { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "MBank";
}
