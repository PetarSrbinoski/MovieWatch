namespace MovieWatch.Domain.Config;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "MovieWatch";
    public string Audience { get; set; } = "MovieWatch";
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 15;
}
