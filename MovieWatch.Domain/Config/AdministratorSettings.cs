namespace MovieWatch.Domain.Config;

public sealed class AdministratorSettings
{
    public bool Enabled { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
