namespace Jama.Application.Staffs;

using Jama.Application.Common;

public enum StaffDepartment
{
    Technician,
    MoiDiaUpload,
    MoiDiaInspection,
    Panels,
}

public static class StaffDepartmentExtensions
{
    public static string ToDisplayName(this StaffDepartment department) =>
        department switch
        {
            StaffDepartment.Technician => "Technician",
            StaffDepartment.MoiDiaUpload => "MOI DIA Upload",
            StaffDepartment.MoiDiaInspection => "MOI DIA Inspection",
            StaffDepartment.Panels => "Panels",
            _ => throw new ArgumentOutOfRangeException(nameof(department), department, null),
        };

    public static string ToAuthRole(this StaffDepartment? department) =>
        department == StaffDepartment.Technician
            ? Roles.Technician
            : Roles.Staff;
}
