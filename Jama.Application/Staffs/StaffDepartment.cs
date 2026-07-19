namespace Jama.Application.Staffs;

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
}
