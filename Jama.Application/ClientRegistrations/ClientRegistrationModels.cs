using Jama.Application.Staffs;
using Jama.Domain.Entities;
using Jama.Domain.Enums;

namespace Jama.Application.ClientRegistrations;

/// <summary>One allottable team — a staff department, by its stable key and
/// its display name.</summary>
public sealed record ClientTeamDto(string Key, string Name);

public sealed record ClientRegistrationDto(
    Guid Id,
    string FullName,
    string Email,
    string Mobile,
    string SiteLocation,
    ClientRegistrationStatus Status,
    ClientCategory? Category,
    IReadOnlyList<ClientTeamDto> Teams,
    Guid? DiaInspectionId,
    string? DiaNumber,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? RejectionReason);

public sealed record ClientRegistrationCountsDto(int Pending, int Approved, int Rejected);

/// <summary>The admin list, with every status's count (for the tabs) and the
/// teams that can be allotted (for the accept dialog) in the same response —
/// both are needed the moment the page opens.</summary>
public sealed record ClientRegistrationListDto(
    IReadOnlyList<ClientRegistrationDto> Items,
    ClientRegistrationCountsDto Counts,
    IReadOnlyList<ClientTeamDto> AvailableTeams);

/// <summary>
/// What a client sees about their own registration. Deliberately narrower than
/// <see cref="ClientRegistrationDto"/>: no reviewer name (it can carry a staff
/// email) and no rejection reason (written by admins for each other).
/// </summary>
public sealed record MyClientRegistrationDto(
    string FullName,
    string Email,
    string Mobile,
    string SiteLocation,
    ClientRegistrationStatus Status,
    ClientCategory? Category,
    IReadOnlyList<ClientTeamDto> Teams,
    string? DiaNumber,
    bool DiaActive,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

/// <summary>
/// Teams are the existing staff departments — no separate team table to keep
/// in step with them. Stored by enum NAME so a renamed display label cannot
/// orphan an allotment; validated by exact name so "1" (which Enum.TryParse
/// would happily read as the second department) is refused.
/// </summary>
internal static class ClientTeams
{
    private static readonly string[] Keys = Enum.GetNames<StaffDepartment>();

    public static IReadOnlyList<ClientTeamDto> All { get; } = Enum.GetValues<StaffDepartment>()
        .Select(d => new ClientTeamDto(d.ToString(), d.ToDisplayName()))
        .ToList();

    public static bool IsValid(string? key) => key is not null && Keys.Contains(key, StringComparer.Ordinal);

    /// <summary>Distinct, in department order — so the same allotment always
    /// reads the same way regardless of the order the boxes were ticked.</summary>
    public static List<string> Normalize(IEnumerable<string> keys)
    {
        var wanted = keys.ToHashSet(StringComparer.Ordinal);
        return Keys.Where(wanted.Contains).ToList();
    }

    public static IReadOnlyList<ClientTeamDto> Describe(IEnumerable<string> keys) =>
        keys.Select(k => Enum.TryParse<StaffDepartment>(k, out var d) && IsValid(k)
                ? new ClientTeamDto(k, d.ToDisplayName())
                : new ClientTeamDto(k, k))
            .ToList();
}

internal static class ClientRegistrationMappings
{
    /// <summary>Expects <see cref="ClientRegistration.DiaInspection"/> loaded
    /// when one is linked — the DIA number is read through it.</summary>
    public static ClientRegistrationDto ToDto(ClientRegistration x) => new(
        x.Id,
        x.FullName,
        x.Email,
        x.Mobile,
        x.SiteLocation,
        x.Status,
        x.Category,
        ClientTeams.Describe(x.Teams),
        x.DiaInspectionId,
        x.DiaInspection?.DiaNumber,
        x.CreatedAt,
        x.ReviewedByName,
        x.ReviewedAt,
        x.RejectionReason);
}
