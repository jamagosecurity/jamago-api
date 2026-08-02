using FluentValidation;

namespace Jama.Application.Common;

/// <summary>
/// One password policy for every account the system issues — staff, technician
/// and VIP client alike. Kept here rather than repeated per validator so the
/// rule cannot drift and leave one kind of login weaker than another.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 8;

    public static IRuleBuilderOptions<T, string?> Strong<T>(IRuleBuilder<T, string?> rule) =>
        rule.MinimumLength(MinimumLength)
                .WithMessage($"Password must be at least {MinimumLength} characters.")
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.");
}
