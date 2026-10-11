namespace SupportDesk.Presentation.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NorthwindSupportDesk";

    public string Audience { get; set; } = "NorthwindSupportDesk.Web";

    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 30;
}