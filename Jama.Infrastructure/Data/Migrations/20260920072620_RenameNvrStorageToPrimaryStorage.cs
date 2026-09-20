using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jama.Infrastructure.Data.Migrations
{
    /// <summary>
    /// "NVR & Storage" is now "Primary Storage" — a second section, "Failover
    /// storage", sits beside it for a separate recording path priced as its
    /// own hardware.
    ///
    /// BoqSections.Title is free text, not an enum stored by name, so unlike
    /// the Location-to-Lot rename this is not something that fails to parse
    /// on read if left alone. It is still worth rewriting: BoqSectionTitles
    /// no longer recognises the old spelling, so a section still holding it
    /// would fall outside the canonical list — the picker would no longer
    /// find its stock group, and typing the exact old title back on an edit
    /// would be refused as not one of the allowed headings.
    ///
    /// Data only: no column changes, which is why the scaffolded migration
    /// was empty.
    /// </summary>
    public partial class RenameNvrStorageToPrimaryStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""BoqSections"" SET ""Title"" = 'Primary Storage' WHERE ""Title"" = 'NVR & Storage';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""BoqSections"" SET ""Title"" = 'NVR & Storage' WHERE ""Title"" = 'Primary Storage';");
        }
    }
}
