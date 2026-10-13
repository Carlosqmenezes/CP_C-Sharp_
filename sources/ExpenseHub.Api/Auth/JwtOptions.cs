namespace ExpenseHub.Api.Auth;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ExpenseHub";

    public string Audience { get; set; } = "ExpenseHub";

    public int ExpirationMinutes { get; set; } = 60;
}
