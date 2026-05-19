namespace MBank.Api.Services;

public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string HtmlBody);
