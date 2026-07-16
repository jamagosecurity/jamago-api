namespace Jama.Domain.Entities;

public class Staff : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Responsibility { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
