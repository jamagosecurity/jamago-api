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

    /// <summary>
    /// Seeing what stock costs us, and the margin on it.
    ///
    /// Separate from <see cref="CameraManage"/> and implied by nothing: managing
    /// the catalogue and knowing the buying price are different jobs, and this is
    /// the figure a business least wants on a screen it does not control. Held by
    /// nobody until it is granted, which is the point.
    /// </summary>
    public const string CostView = "camera.cost";

    /// <summary>Building and submitting CAD drawings. Deliberately separate from
    /// <see cref="DrawingApprove"/>, for the same reason boq.manage and
    /// boq.approve are separate: drafting a drawing and deciding on it are
    /// different jobs, and one person doing both is how a drawing goes out
    /// without anyone else having looked at it.</summary>
    public const string DrawingManage = "drawing.manage";

    /// <summary>Deciding on a drawing someone else drafted. Not implied by
    /// <see cref="DrawingManage"/>, and grants nothing about editing one.</summary>
    public const string DrawingApprove = "drawing.approve";
    public const string BoqManage = "boq.manage";

    /// <summary>Deciding on a quotation someone else built. Deliberately not
    /// implied by <see cref="BoqManage"/>: building one and approving it are
    /// different jobs, and one person doing both is the thing this separates.</summary>
    public const string BoqApprove = "boq.approve";

    /// <summary>Changing a quotation that has already been approved — price,
    /// discount, or which items are on it. Previously only the super
    /// administrator could do this at all; this grant lets an admin hand the
    /// same ability to a specific account (e.g. a manager who has to correct
    /// a signed-off quotation) without making them the root account. Implies
    /// <see cref="BoqManage"/> — see <see cref="Expand"/> — since there is no
    /// way to reach the editor without it, and still needs a reason on every
    /// amendment — see UpdateBoqCommandHandler.</summary>
    public const string BoqAmend = "boq.amend";

    /// <summary>
    /// Seeing every quotation, not just the holder's own plus whatever is
    /// Approved — see BoqVisibility.CanSee. Without this, a plain
    /// boq.manage account can only browse their own drafts and submissions;
    /// a coordinator or team lead who needs oversight of the whole team's
    /// quotations, but is not the one approving them, previously had no
    /// grant that gave them that without also handing them boq.approve's
    /// power to decide on one. Read-only: implies nothing about building,
    /// approving, or amending.
    /// </summary>
    public const string BoqViewAll = "boq.viewAll";

    /// <summary>Managing maintenance tickets — power, cabling, camera, NVR,
    /// and software service callouts. One grant covers the whole lifecycle
    /// (assign, resolve, cancel): unlike a quotation, there is no separate
    /// decision to approve, so building and deciding are not different jobs
    /// here the way boq.manage/boq.approve are split.</summary>
    public const string MaintenanceManage = "maintenance.manage";

    /// <summary>Issuing Non-MOI jobs to technicians, seeing their status and amount, and managing the Non-MOI inventory (the services-and-prices list the technician ticks from). Split out of
    /// <see cref="MaintenanceManage"/> so a coordinator can hand out jobs without also getting the
    /// whole ticket queue. Anyone who already holds maintenance.manage keeps this — see <see cref="Expand"/>.</summary>
    public const string NonMoiJobManage = "nonmoijob.manage";

    /// <summary>Reading the company mailboxes in the Company mail screen. Read-only: sending, replying and
    /// forwarding stay with administrators, because mail goes out in a mailbox owner's name.</summary>
    public const string MailRead = "mail.read";

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
        new(CameraManage, "Manage the stock inventory", "Can add, edit and remove stock items and set their selling prices. Give it deliberately: everything the business quotes is priced from these rows. What an item COST is a separate grant."),
        new(CostView, "See supplier cost & margin", "Can see what stock costs the business and the margin on it, on the stock screens and — behind a button that closes itself — while building a quotation. Off for everyone until granted, including staff who manage the catalogue."),
        // One grant, two screens: the storage calculator sizes the array for a
        // quotation, so anyone who can build one can size it. Splitting them
        // would let an account make a quotation it cannot check the storage for.
        new(BoqManage, "Build quotations & size storage", "Can pick stock items into a quotation, set quantities and rates, submit it for approval, and size the NVR storage for it. Cannot approve one — not even their own."),
        // The one grant that is about somebody else's work. Held on its own it
        // is enough to open a quotation and decide on it, and nothing else.
        new(BoqApprove, "Approve or reject quotations", "Can open a quotation submitted for approval and approve it, or reject it with a reason. Does not include building or editing quotations."),
        new(BoqAmend, "Change an approved quotation", "Can edit a quotation after it has been approved — price, discount, or the items on it — same as the super administrator can. Every change still needs a reason, recorded on the quotation's history. Includes \"Build quotations & size storage\" automatically, since that's what opens the editor."),
        new(BoqViewAll, "View all quotations", "Oversight: can browse every quotation in the system, whoever built it and whatever its status — not just their own plus the ones already approved. Read-only: does not include building, approving, or amending."),
        new(DrawingManage, "Build & submit drawings", "Can upload CAD drawings, attach the plotted PDF, and submit a drawing for approval. Cannot approve one — not even their own."),
        new(DrawingApprove, "Approve or reject drawings", "Can open a drawing submitted for approval and approve it, or reject it with a reason. Does not include uploading or editing drawings."),
        new(MaintenanceManage, "Manage maintenance tickets", "Can see every maintenance ticket, assign one to themself or another staff member, resolve it with notes, or cancel it. Also needed to log a ticket on a client's behalf — a client can always log their own."),
        new(NonMoiJobManage, "Non-MOI jobs & inventory", "Can hand a Non-MOI job to one technician, edit or delete it, see which are done and for how much, and manage the Non-MOI inventory (services, prices and pictures). Does not include the maintenance ticket queue."),
        new(MailRead, "Read company mail", "Can open the Company mail screen and read every company mailbox, incoming and outgoing. Read-only: cannot send, reply or forward — that stays with administrators."),
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

        // Same shape as the DIA case above: the BOQ update endpoint is gated on
        // BoqManage at the route, before UpdateBoqCommandHandler ever gets to
        // ask about BoqAmend. An admin who ticks only "Change an approved
        // quotation" — the natural thing to tick for "let them fix the price
        // after sign-off" — produced an account that 403s the moment it tries,
        // never reaching the amend check at all.
        // Handling maintenance used to be what let someone issue Non-MOI jobs. Keep that true, so splitting
        // the grant out does not take access away from anyone who already had it.
        if (effective.Contains(MaintenanceManage))
        {
            effective.Add(NonMoiJobManage);
        }

        if (effective.Contains(BoqAmend))
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
