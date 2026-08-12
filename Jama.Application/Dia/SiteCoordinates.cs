using FluentValidation;

namespace Jama.Application.Dia;

/// <summary>A DIA write request that carries an optional map pin for the site.</summary>
public interface ISiteCoordinateRequest
{
    double? Latitude { get; }
    double? Longitude { get; }
}

/// <summary>
/// WGS 84 site-pin rules shared by the create and update commands, so the two cannot
/// drift apart on what a valid pin is. The database carries the same rules as a check
/// constraint — this layer exists to return a readable message rather than a 500.
/// </summary>
public static class SiteCoordinates
{
    /// <summary>
    /// Six decimal places is ~0.11 m at the equator — far finer than any site gate
    /// needs. Rounding on write also keeps float noise (25.286106000000001) out of
    /// the audit trail, where it would otherwise read as a real edit.
    /// </summary>
    public const int Precision = 6;

    public static double? Round(double? value) =>
        value is null ? null : Math.Round(value.Value, Precision, MidpointRounding.AwayFromZero);

    public static void AddRulesTo<T>(AbstractValidator<T> validator)
        where T : ISiteCoordinateRequest
    {
        validator.RuleFor(x => x.Latitude)
            .InclusiveBetween(-90d, 90d)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90.");

        validator.RuleFor(x => x.Longitude)
            .InclusiveBetween(-180d, 180d)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180.");

        validator.RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithName("Coordinates")
            .WithMessage("Latitude and longitude must be provided together.")
            // (0, 0) is in the Atlantic; in practice it is what a failed paste or an
            // empty-string-to-zero conversion looks like, and routing a technician
            // there is worse than having no pin at all.
            .Must(x => x is not { Latitude: 0, Longitude: 0 })
            .WithName("Coordinates")
            .WithMessage("0, 0 is not a valid site location — check the coordinates.");
    }
}
