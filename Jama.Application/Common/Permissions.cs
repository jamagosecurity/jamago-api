namespace Jama.Application.Common;

/// <summary>
/// Fine-grained access an admin can grant to an individual staff account.
///
/// Roles stay coarse (Admin / Staff / Technician) and answer "which portal do you
/// land in". Permissions answer "what may you do once you are there", so two staff
/// in different departments can share the Staff portal with different capabilities.
///
/// Admins implicitly hold every permission — see <see cref="ForRole"/> — so an
/// administrator can never lock themselves out by editing permission lists.
/// </summary>
public static class Permissions
{
    public const string DiaView = "dia.view";
    public const string DiaUpload = "dia.upload";
    public const string DiaInspect = "dia.inspect";
    public const string InvoiceView = "invoice.view";
    public const string ContactView = "contact.view";
    public const string PanelsManage = "panels.manage";
    public const string VipManage = "vip.manage";
    public const string CameraManage = "camera.manage";
    public const string BoqManage = "boq.manage";
    public const string BoqPrice = "boq.price";

    /// <summary>Every permission that may be granted, with display copy for the admin UI.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        // Plain language, and each description says what the person can and
        // cannot do. The three DIA permissions previously read as near-synonyms
        // ("View DIA inspections" / "Create DIA records" / "Perform
        // inspections"), which made them impossible to tell apart at a glance.
        // Keys are unchanged, so existing grants are untouched.
        new(DiaView, "Look at DIA records", "Can open the DIA list and see each site's status. Cannot change anything."),
        new(DiaUpload, "Add and edit DIA records", "Can create new DIA records and change existing ones. Looking at them is included."),
        new(DiaInspect, "Do the quarterly site inspections", "For technicians: fill in and submit the inspection form for a site each quarter."),
        new(InvoiceView, "Open invoices", "Can view and download the invoices produced after an inspection."),
        new(ContactView, "Read website enquiries", "Can read messages people send through the contact form on jamago.qa."),
        new(PanelsManage, "Manage control panels", "Can add and edit control panel records."),
        new(VipManage, "Manage VIP clients", "Can create VIP client projects and upload documents to their folders."),
        new(CameraManage, "Manage the stock inventory", "Can add, edit and remove stock items and set their prices. Everyone can read the public catalogue."),
        // One grant, two screens: the storage calculator sizes the array for a
        // quotation, so anyone who can build one can size it. Splitting them
        // would let an account make a quotation it cannot check the storage for.
        new(BoqManage, "Build quotations & size storage", "Can pick stock items into a quotation, set quantities, and size the NVR storage for it. Rates come from the catalogue unless the account also holds the rate override below."),
        // Deliberately separate from BoqManage. Quantity is a fact about the
        // site and belongs to whoever surveys it; a rate is a commercial
        // decision, and one person discounting on their own authority is how a
        // job goes out below cost. The catalogue rate is still recorded on every
        // line, so an override is always visible as a variance rather than
        // replacing the list price.
        new(BoqPrice, "Change rates on a quotation", "Can type a different unit price on a quotation line instead of taking the catalogue rate. The catalogue price is still recorded beside it, so any discount is visible."),
    ];

    private static readonly HashSet<string> Known = All.Select(p => p.Key).ToHashSet();

    public static bool IsValid(string permission) => Known.Contains(permission);

    /// <summary>
    /// Expands implied grants into the effective set.
    ///
    /// Editing or inspecting a DIA record is impossible without reading it, so
    /// both imply <see cref="DiaView"/>. Without this an admin can tick only
    /// "Upload DIA records" and produce an account that reaches the DIA screens
    /// and then 403s on every list and detail request.
    /// </summary>
    public static IReadOnlyList<string> Expand(IEnumerable<string> granted)
    {
        var effective = new HashSet<string>(granted);

        if (effective.Contains(DiaUpload) || effective.Contains(DiaInspect))
        {
            effective.Add(DiaView);
        }

        // Overriding a rate is something you do WHILE building a quotation, so
        // the grant is meaningless without the screen it applies to. Ticking it
        // alone would otherwise produce an account that cannot open a quotation
        // and holds a permission about quotations — the same trap DiaUpload
        // without DiaView used to set.
        if (effective.Contains(BoqPrice))
        {
            effective.Add(BoqManage);
        }

        return effective.ToList();
    }

    /// <summary>
    /// Baseline permissions implied by a role. Admins hold everything; other roles
    /// hold only what has been granted explicitly, so this returns empty for them.
    /// </summary>
    public static IReadOnlyList<string> ForRole(string role) =>
        role == Roles.Admin ? All.Select(p => p.Key).ToList() : [];

    /// <summary>
    /// Capabilities a role cannot function without. Used only when an account has
    /// no explicit grants at all — a Technician with an empty permission list is
    /// an account nobody has configured yet, not one deliberately stripped of the
    /// ability to inspect, and locking them out of their own portal would be a
    /// worse failure than granting the baseline.
    /// </summary>
    private static IReadOnlyList<string> BaselineForRole(string role) =>
        role == Roles.Technician ? [DiaView, DiaInspect, InvoiceView] : [];

    /// <summary>
    /// The effective permission set for an account: the single place that answers
    /// "what may this user do". Resolution order is admin-implied, then explicit
    /// grants, then the role baseline.
    ///
    /// Explicit grants win over the baseline, so unticking "Perform inspections"
    /// on a technician genuinely removes it rather than being quietly restored.
    /// </summary>
    public static IReadOnlyList<string> EffectiveFor(string role, IEnumerable<string> explicitGrants)
    {
        if (role == Roles.Admin)
            return All.Select(p => p.Key).ToList();

        var granted = explicitGrants.Where(IsValid).Distinct().ToList();
        return Expand(granted.Count > 0 ? granted : BaselineForRole(role));
    }

    /// <summary>
    /// Department defaults keyed by the enum name the client sends, so the staff
    /// editor can apply them the moment an admin picks a department instead of
    /// them only ever being seeded server-side at creation time.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultsByDepartment { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["Technician"] = DefaultsForDepartment("Technician"),
            ["MoiDiaUpload"] = DefaultsForDepartment("MOI DIA Upload"),
            ["MoiDiaInspection"] = DefaultsForDepartment("MOI DIA Inspection"),
            ["Panels"] = DefaultsForDepartment("Panels"),
        };

    /// <summary>
    /// Sensible starting permissions when an admin creates an account, based on the
    /// department they picked. The admin can tick or untick anything afterwards.
    /// </summary>
    public static IReadOnlyList<string> DefaultsForDepartment(string? department) =>
        department switch
        {
            "Technician" => [DiaView, DiaInspect, InvoiceView],
            "MOI DIA Upload" => [DiaView, DiaUpload],
            "MOI DIA Inspection" => [DiaView, DiaInspect],
            "Panels" => [PanelsManage],
            _ => [],
        };
}

public sealed record PermissionDefinition(string Key, string Name, string Description);

/// <summary>
/// Everything the staff editor needs to render access settings: the grantable
/// permissions and what each department starts with.
/// </summary>
public sealed record PermissionCatalogueDto(
    IReadOnlyList<PermissionDefinition> Permissions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> DepartmentDefaults);
