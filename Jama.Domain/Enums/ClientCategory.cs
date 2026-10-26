namespace Jama.Domain.Enums;

/// <summary>Set by the admin when accepting a registration. An MOI client's
/// site is under Ministry of Interior inspection, so accepting one also opens
/// its DIA inspection record; a Non-MOI client has no such record.</summary>
public enum ClientCategory
{
    Moi,
    NonMoi,
}
