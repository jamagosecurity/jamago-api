using Jama.Domain.Enums;

namespace Jama.Domain.Entities;

public class VipClientFolder : BaseEntity
{
    public Guid VipClientId { get; set; }
    public VipClient VipClient { get; set; } = null!;

    public VipFolderKind Kind { get; set; }

    /// <summary>Shown to admins and to the client. Renaming is display-only.</summary>
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public ICollection<VipClientDocument> Documents { get; set; } = [];
}
