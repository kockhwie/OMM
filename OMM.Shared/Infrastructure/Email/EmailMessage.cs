namespace OMM.Shared.Infrastructure.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? FromAddress = null,
    string? FromName = null,
    string? PlainText = null);
