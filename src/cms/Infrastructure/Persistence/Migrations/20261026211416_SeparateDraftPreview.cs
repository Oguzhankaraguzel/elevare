using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateDraftPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftGjsCss",
                table: "PageContents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftGjsHtml",
                table: "PageContents",
                type: "text",
                nullable: true);

            // Until now the editor's preview wrote the same columns an approval
            // workflow stages into, so pages that were only previewed (and, since
            // 1.3.0, every page the SEO analyzer read) look as if an update were
            // waiting. Content is staged for approval only under a request, so where
            // a page has no pending or rejected one (a rejected request keeps its
            // content on view) the columns hold nothing but an old preview: that
            // moves to the editor's own draft columns.
            migrationBuilder.Sql("""
                UPDATE "PageContents" c
                SET "DraftGjsHtml" = c."PreviewGjsHtml",
                    "DraftGjsCss" = c."PreviewGjsCss",
                    "PreviewGjsHtml" = NULL,
                    "PreviewGjsCss" = NULL
                WHERE (c."PreviewGjsHtml" IS NOT NULL OR c."PreviewGjsCss" IS NOT NULL)
                  AND NOT EXISTS (
                      SELECT 1 FROM "ApprovalRequests" a
                      WHERE a."ContentType" = 0
                        AND a."ContentId" = c."PageInfoId"
                        AND a."Status" IN (0, 2)
                        AND NOT a."IsDeleted");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftGjsCss",
                table: "PageContents");

            migrationBuilder.DropColumn(
                name: "DraftGjsHtml",
                table: "PageContents");
        }
    }
}
