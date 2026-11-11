namespace Jama.Application.Options;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Technicians work on site with the phone in a pocket for hours between opening a job and filling it in; a 60-minute
    /// session expired under them and threw them out mid-job. Their session is a working day instead.</summary>
    public int TechnicianTokenMinutes { get; set; } = 720;
}
