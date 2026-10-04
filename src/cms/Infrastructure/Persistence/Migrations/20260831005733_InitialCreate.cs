using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Level = table.Column<int>(type: "integer", nullable: false),
                Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Exception = table.Column<string>(type: "text", nullable: true),
                Source = table.Column<int>(type: "integer", nullable: false),
                Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastLoginDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuthEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EventType = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                UserNameSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PageClickHits",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ElementLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                ClickedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageClickHits", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PageGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageGroups", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PageViewHits",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                ViewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DurationSeconds = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageViewHits", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PasswordSetupTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordSetupTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SitemapCaches",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CacheKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                SitemapUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                XmlContent = table.Column<string>(type: "text", nullable: false),
                GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EntryCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SitemapCaches", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ContentBulkEdits",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Kind = table.Column<int>(type: "integer", nullable: false),
                SearchText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ReplaceText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AffectedPageCount = table.Column<int>(type: "integer", nullable: false),
                IsReverted = table.Column<bool>(type: "boolean", nullable: false),
                RevertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentBulkEdits", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContentBulkEdits_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ContentBulkEdits_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ContentBulkEdits_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "FormReplyTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Body = table.Column<string>(type: "text", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FormReplyTemplates", x => x.Id);
                table.ForeignKey(
                    name: "FK_FormReplyTemplates_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_FormReplyTemplates_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_FormReplyTemplates_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "IntegrationSecrets",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "text", nullable: true),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Category = table.Column<int>(type: "integer", nullable: false),
                IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntegrationSecrets", x => x.Id);
                table.ForeignKey(
                    name: "FK_IntegrationSecrets_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_IntegrationSecrets_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_IntegrationSecrets_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "MediaFiles",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                FolderPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Width = table.Column<int>(type: "integer", nullable: true),
                Height = table.Column<int>(type: "integer", nullable: true),
                Renditions = table.Column<string>(type: "jsonb", nullable: true),
                MediaType = table.Column<int>(type: "integer", nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_MediaFiles_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_MediaFiles_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_MediaFiles_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "SiteCodeSnippets",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Preset = table.Column<int>(type: "integer", nullable: false),
                Placement = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteCodeSnippets", x => x.Id);
                table.ForeignKey(
                    name: "FK_SiteCodeSnippets_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SiteCodeSnippets_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SiteCodeSnippets_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "SiteSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "text", nullable: true),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Group = table.Column<int>(type: "integer", nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_SiteSettings_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SiteSettings_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SiteSettings_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tags", x => x.Id);
                table.ForeignKey(
                    name: "FK_Tags_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Tags_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Tags_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "UserNotes",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                Visibility = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserNotes", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserNotes_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserNotes_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserNotes_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserNotes_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "UserReminders",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                RemindAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsDismissed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                Channel = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserReminders", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserReminders_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserReminders_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserReminders_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserReminders_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "UserTasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ParentTaskId = table.Column<int>(type: "integer", nullable: true),
                RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserTasks_AspNetUsers_AssignedByUserId",
                    column: x => x.AssignedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTasks_AspNetUsers_AssignedToUserId",
                    column: x => x.AssignedToUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTasks_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTasks_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTasks_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTasks_UserTasks_ParentTaskId",
                    column: x => x.ParentTaskId,
                    principalTable: "UserTasks",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "WorkflowDefinitions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContentType = table.Column<int>(type: "integer", nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowDefinitions_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_WorkflowDefinitions_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_WorkflowDefinitions_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Languages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                NameInNative = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                NameInEnglish = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TwoLetterCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                IsRtl = table.Column<bool>(type: "boolean", nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                FlagIconFileId = table.Column<int>(type: "integer", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Languages", x => x.Id);
                table.ForeignKey(
                    name: "FK_Languages_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Languages_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Languages_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Languages_MediaFiles_FlagIconFileId",
                    column: x => x.FlagIconFileId,
                    principalTable: "MediaFiles",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "AnnouncementReads",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserNoteId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnnouncementReads", x => x.Id);
                table.ForeignKey(
                    name: "FK_AnnouncementReads_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AnnouncementReads_UserNotes_UserNoteId",
                    column: x => x.UserNoteId,
                    principalTable: "UserNotes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserTaskComments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserTaskId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserTaskComments", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserTaskComments_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTaskComments_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTaskComments_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTaskComments_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserTaskComments_UserTasks_UserTaskId",
                    column: x => x.UserTaskId,
                    principalTable: "UserTasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ContentType = table.Column<int>(type: "integer", nullable: false),
                ContentId = table.Column<int>(type: "integer", nullable: false),
                WorkflowDefinitionId = table.Column<int>(type: "integer", nullable: false),
                CurrentStepOrder = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ApprovalRequests_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ApprovalRequests_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ApprovalRequests_WorkflowDefinitions_WorkflowDefinitionId",
                    column: x => x.WorkflowDefinitionId,
                    principalTable: "WorkflowDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowSteps",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                WorkflowDefinitionId = table.Column<int>(type: "integer", nullable: false),
                StepOrder = table.Column<int>(type: "integer", nullable: false),
                RequiredRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                RequiredUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowSteps_AspNetRoles_RequiredRoleId",
                    column: x => x.RequiredRoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkflowSteps_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_WorkflowSteps_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_WorkflowSteps_AspNetUsers_RequiredUserId",
                    column: x => x.RequiredUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkflowSteps_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId",
                    column: x => x.WorkflowDefinitionId,
                    principalTable: "WorkflowDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PageInfos",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                FullSlug = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                PageStatus = table.Column<int>(type: "integer", nullable: false),
                PendingStatus = table.Column<int>(type: "integer", nullable: true),
                PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Kind = table.Column<int>(type: "integer", nullable: false),
                SeoIsCanonical = table.Column<bool>(type: "boolean", nullable: false),
                SeoCanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SeoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SeoMetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                SeoMetaAuthor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SeoMeta_FocusKeyword = table.Column<string>(type: "text", nullable: true),
                SeoNoIndex = table.Column<bool>(type: "boolean", nullable: false),
                SeoNoFollow = table.Column<bool>(type: "boolean", nullable: false),
                SeoStructuredData = table.Column<string>(type: "text", nullable: true),
                OgTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                OgDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                OgType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                OgImage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                OgUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                TwitterCard = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                TwitterSite = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                SeoSocialJson = table.Column<string>(type: "jsonb", nullable: true),
                SeoScore = table.Column<int>(type: "integer", nullable: true),
                SeoScoreUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ParentPageId = table.Column<int>(type: "integer", nullable: true),
                LanguageId = table.Column<int>(type: "integer", nullable: false),
                PageGroupId = table.Column<int>(type: "integer", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageInfos", x => x.Id);
                table.ForeignKey(
                    name: "FK_PageInfos_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageInfos_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageInfos_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageInfos_Languages_LanguageId",
                    column: x => x.LanguageId,
                    principalTable: "Languages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PageInfos_PageGroups_PageGroupId",
                    column: x => x.PageGroupId,
                    principalTable: "PageGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PageInfos_PageInfos_ParentPageId",
                    column: x => x.ParentPageId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PageTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                LanguageId = table.Column<int>(type: "integer", nullable: true),
                IsLinked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                GjsHtml = table.Column<string>(type: "text", nullable: true),
                GjsCss = table.Column<string>(type: "text", nullable: true),
                GjsData = table.Column<string>(type: "text", nullable: true),
                ContentChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PreviewGjsHtml = table.Column<string>(type: "text", nullable: true),
                PreviewGjsCss = table.Column<string>(type: "text", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageTemplates", x => x.Id);
                table.ForeignKey(
                    name: "FK_PageTemplates_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageTemplates_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageTemplates_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageTemplates_Languages_LanguageId",
                    column: x => x.LanguageId,
                    principalTable: "Languages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalStepDecisions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ApprovalRequestId = table.Column<int>(type: "integer", nullable: false),
                StepOrder = table.Column<int>(type: "integer", nullable: false),
                Decision = table.Column<int>(type: "integer", nullable: false),
                Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalStepDecisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalStepDecisions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApprovalStepDecisions_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ApprovalStepDecisions_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ApprovalStepDecisions_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ContentBulkEditItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ContentBulkEditId = table.Column<int>(type: "integer", nullable: false),
                PageInfoId = table.Column<int>(type: "integer", nullable: false),
                PageTitleSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PageFullSlugSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                MatchCount = table.Column<int>(type: "integer", nullable: false),
                OldGjsHtml = table.Column<string>(type: "text", nullable: true),
                OldGjsCss = table.Column<string>(type: "text", nullable: true),
                OldGjsData = table.Column<string>(type: "text", nullable: true),
                OldStructuredData = table.Column<string>(type: "text", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentBulkEditItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContentBulkEditItems_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ContentBulkEditItems_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ContentBulkEditItems_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ContentBulkEditItems_ContentBulkEdits_ContentBulkEditId",
                    column: x => x.ContentBulkEditId,
                    principalTable: "ContentBulkEdits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ContentBulkEditItems_PageInfos_PageInfoId",
                    column: x => x.PageInfoId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FormSubmissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PageInfoId = table.Column<int>(type: "integer", nullable: false),
                FormName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                FieldsJson = table.Column<string>(type: "text", nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RepliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RepliedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ReplySubject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                ReplyBody = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FormSubmissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_FormSubmissions_AspNetUsers_RepliedByUserId",
                    column: x => x.RepliedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FormSubmissions_PageInfos_PageInfoId",
                    column: x => x.PageInfoId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PageContents",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PageInfoId = table.Column<int>(type: "integer", nullable: false),
                GjsHtml = table.Column<string>(type: "text", nullable: true),
                GjsCss = table.Column<string>(type: "text", nullable: true),
                GjsData = table.Column<string>(type: "text", nullable: true),
                PreviewGjsHtml = table.Column<string>(type: "text", nullable: true),
                PreviewGjsCss = table.Column<string>(type: "text", nullable: true),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageContents", x => x.Id);
                table.ForeignKey(
                    name: "FK_PageContents_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageContents_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageContents_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PageContents_PageInfos_PageInfoId",
                    column: x => x.PageInfoId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PageInfoSiteCodeExclusions",
            columns: table => new
            {
                PageInfoId = table.Column<int>(type: "integer", nullable: false),
                SiteCodeSnippetId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageInfoSiteCodeExclusions", x => new { x.PageInfoId, x.SiteCodeSnippetId });
                table.ForeignKey(
                    name: "FK_PageInfoSiteCodeExclusions_PageInfos_PageInfoId",
                    column: x => x.PageInfoId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PageInfoSiteCodeExclusions_SiteCodeSnippets_SiteCodeSnippet~",
                    column: x => x.SiteCodeSnippetId,
                    principalTable: "SiteCodeSnippets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PageInfoTags",
            columns: table => new
            {
                PageInfoId = table.Column<int>(type: "integer", nullable: false),
                TagId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PageInfoTags", x => new { x.PageInfoId, x.TagId });
                table.ForeignKey(
                    name: "FK_PageInfoTags_PageInfos_PageInfoId",
                    column: x => x.PageInfoId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PageInfoTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Redirects",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OldPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                NewPath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                SourcePageId = table.Column<int>(type: "integer", nullable: true),
                Reason = table.Column<int>(type: "integer", nullable: false),
                CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdateUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeleteUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Redirects", x => x.Id);
                table.ForeignKey(
                    name: "FK_Redirects_AspNetUsers_CreateUserId",
                    column: x => x.CreateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Redirects_AspNetUsers_DeleteUserId",
                    column: x => x.DeleteUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Redirects_AspNetUsers_UpdateUserId",
                    column: x => x.UpdateUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Redirects_PageInfos_SourcePageId",
                    column: x => x.SourcePageId,
                    principalTable: "PageInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FormSubmissionAttachments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FormSubmissionId = table.Column<int>(type: "integer", nullable: false),
                FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Size = table.Column<long>(type: "bigint", nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FormSubmissionAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_FormSubmissionAttachments_FormSubmissions_FormSubmissionId",
                    column: x => x.FormSubmissionId,
                    principalTable: "FormSubmissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnnouncementReads_UserId",
            table: "AnnouncementReads",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AnnouncementReads_UserNoteId_UserId",
            table: "AnnouncementReads",
            columns: new[] { "UserNoteId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppLogs_CreatedAtUtc",
            table: "AppLogs",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_AppLogs_Level",
            table: "AppLogs",
            column: "Level");

        migrationBuilder.CreateIndex(
            name: "IX_AppLogs_Source",
            table: "AppLogs",
            column: "Source");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_Content_Status",
            table: "ApprovalRequests",
            columns: new[] { "ContentType", "ContentId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_CreateDate",
            table: "ApprovalRequests",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_CreateUserId",
            table: "ApprovalRequests",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_DeleteUserId",
            table: "ApprovalRequests",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_IsActive",
            table: "ApprovalRequests",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_IsDeleted",
            table: "ApprovalRequests",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_IsDeleted_IsActive",
            table: "ApprovalRequests",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_UpdateUserId",
            table: "ApprovalRequests",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_WorkflowDefinitionId",
            table: "ApprovalRequests",
            column: "WorkflowDefinitionId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_ApprovalRequestId",
            table: "ApprovalStepDecisions",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_CreateDate",
            table: "ApprovalStepDecisions",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_CreateUserId",
            table: "ApprovalStepDecisions",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_DeleteUserId",
            table: "ApprovalStepDecisions",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_IsActive",
            table: "ApprovalStepDecisions",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_IsDeleted",
            table: "ApprovalStepDecisions",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_IsDeleted_IsActive",
            table: "ApprovalStepDecisions",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalStepDecisions_UpdateUserId",
            table: "ApprovalStepDecisions",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuthEvents_CreatedAtUtc",
            table: "AuthEvents",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_AuthEvents_EventType",
            table: "AuthEvents",
            column: "EventType");

        migrationBuilder.CreateIndex(
            name: "IX_AuthEvents_UserId",
            table: "AuthEvents",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_ContentBulkEditId",
            table: "ContentBulkEditItems",
            column: "ContentBulkEditId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_CreateDate",
            table: "ContentBulkEditItems",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_CreateUserId",
            table: "ContentBulkEditItems",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_DeleteUserId",
            table: "ContentBulkEditItems",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_IsActive",
            table: "ContentBulkEditItems",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_IsDeleted",
            table: "ContentBulkEditItems",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_IsDeleted_IsActive",
            table: "ContentBulkEditItems",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_PageInfoId",
            table: "ContentBulkEditItems",
            column: "PageInfoId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEditItems_UpdateUserId",
            table: "ContentBulkEditItems",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_CreateDate",
            table: "ContentBulkEdits",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_CreateUserId",
            table: "ContentBulkEdits",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_DeleteUserId",
            table: "ContentBulkEdits",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_IsActive",
            table: "ContentBulkEdits",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_IsDeleted",
            table: "ContentBulkEdits",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_IsDeleted_IsActive",
            table: "ContentBulkEdits",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_IsReverted",
            table: "ContentBulkEdits",
            column: "IsReverted");

        migrationBuilder.CreateIndex(
            name: "IX_ContentBulkEdits_UpdateUserId",
            table: "ContentBulkEdits",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_CreateDate",
            table: "FormReplyTemplates",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_CreateUserId",
            table: "FormReplyTemplates",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_DeleteUserId",
            table: "FormReplyTemplates",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_IsActive",
            table: "FormReplyTemplates",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_IsDeleted",
            table: "FormReplyTemplates",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_IsDeleted_IsActive",
            table: "FormReplyTemplates",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_SortOrder",
            table: "FormReplyTemplates",
            column: "SortOrder");

        migrationBuilder.CreateIndex(
            name: "IX_FormReplyTemplates_UpdateUserId",
            table: "FormReplyTemplates",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FormSubmissionAttachments_FormSubmissionId",
            table: "FormSubmissionAttachments",
            column: "FormSubmissionId");

        migrationBuilder.CreateIndex(
            name: "IX_FormSubmissions_PageInfoId",
            table: "FormSubmissions",
            column: "PageInfoId");

        migrationBuilder.CreateIndex(
            name: "IX_FormSubmissions_RepliedByUserId",
            table: "FormSubmissions",
            column: "RepliedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FormSubmissions_SubmittedAtUtc",
            table: "FormSubmissions",
            column: "SubmittedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_Category",
            table: "IntegrationSecrets",
            column: "Category");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_CreateDate",
            table: "IntegrationSecrets",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_CreateUserId",
            table: "IntegrationSecrets",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_DeleteUserId",
            table: "IntegrationSecrets",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_IsActive",
            table: "IntegrationSecrets",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_IsDeleted",
            table: "IntegrationSecrets",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_IsDeleted_IsActive",
            table: "IntegrationSecrets",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationSecrets_UpdateUserId",
            table: "IntegrationSecrets",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_IntegrationSecrets_Key",
            table: "IntegrationSecrets",
            column: "Key",
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_CreateDate",
            table: "Languages",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_CreateUserId",
            table: "Languages",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_DeleteUserId",
            table: "Languages",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_FlagIconFileId",
            table: "Languages",
            column: "FlagIconFileId");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_IsActive",
            table: "Languages",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_IsActive_IsDeleted",
            table: "Languages",
            columns: new[] { "IsActive", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_Languages_IsDeleted",
            table: "Languages",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_IsDeleted_IsActive",
            table: "Languages",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_Languages_NameInEnglish",
            table: "Languages",
            column: "NameInEnglish");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_TwoLetterCode",
            table: "Languages",
            column: "TwoLetterCode");

        migrationBuilder.CreateIndex(
            name: "IX_Languages_UpdateUserId",
            table: "Languages",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_Languages_Default_ActiveNotDeleted",
            table: "Languages",
            column: "IsDefault",
            unique: true,
            filter: "\"IsDefault\" = true AND \"IsActive\" = true AND \"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_CreateDate",
            table: "MediaFiles",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_CreateUserId",
            table: "MediaFiles",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_DeleteUserId",
            table: "MediaFiles",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_FilePath",
            table: "MediaFiles",
            column: "FilePath");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_FolderPath",
            table: "MediaFiles",
            column: "FolderPath");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_IsActive",
            table: "MediaFiles",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_IsDeleted",
            table: "MediaFiles",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_IsDeleted_IsActive",
            table: "MediaFiles",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_MediaType",
            table: "MediaFiles",
            column: "MediaType");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFiles_UpdateUserId",
            table: "MediaFiles",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageClickHits_ClickedAtUtc",
            table: "PageClickHits",
            column: "ClickedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_PageClickHits_Path",
            table: "PageClickHits",
            column: "Path");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_CreateDate",
            table: "PageContents",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_CreateUserId",
            table: "PageContents",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_DeleteUserId",
            table: "PageContents",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_IsActive",
            table: "PageContents",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_IsDeleted",
            table: "PageContents",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_IsDeleted_IsActive",
            table: "PageContents",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_PageContents_UpdateUserId",
            table: "PageContents",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_PageContents_PageInfoId",
            table: "PageContents",
            column: "PageInfoId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PageGroups_Name",
            table: "PageGroups",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_CreateDate",
            table: "PageInfos",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_CreateUserId",
            table: "PageInfos",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_DeleteUserId",
            table: "PageInfos",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_FullSlug",
            table: "PageInfos",
            column: "FullSlug");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_IsActive",
            table: "PageInfos",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_IsDeleted",
            table: "PageInfos",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_IsDeleted_IsActive",
            table: "PageInfos",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_LanguageId",
            table: "PageInfos",
            column: "LanguageId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_PageGroupId",
            table: "PageInfos",
            column: "PageGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_ParentPageId",
            table: "PageInfos",
            column: "ParentPageId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_Slug",
            table: "PageInfos",
            column: "Slug");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfos_UpdateUserId",
            table: "PageInfos",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_PageInfos_Slug_Language",
            table: "PageInfos",
            columns: new[] { "Slug", "LanguageId" },
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfoSiteCodeExclusions_SiteCodeSnippetId",
            table: "PageInfoSiteCodeExclusions",
            column: "SiteCodeSnippetId");

        migrationBuilder.CreateIndex(
            name: "IX_PageInfoTags_TagId",
            table: "PageInfoTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_CreateDate",
            table: "PageTemplates",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_CreateUserId",
            table: "PageTemplates",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_DeleteUserId",
            table: "PageTemplates",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_IsActive",
            table: "PageTemplates",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_IsDeleted",
            table: "PageTemplates",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_IsDeleted_IsActive",
            table: "PageTemplates",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_IsLinked",
            table: "PageTemplates",
            column: "IsLinked");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_LanguageId",
            table: "PageTemplates",
            column: "LanguageId");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_Type",
            table: "PageTemplates",
            column: "Type");

        migrationBuilder.CreateIndex(
            name: "IX_PageTemplates_UpdateUserId",
            table: "PageTemplates",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PageViewHits_Path",
            table: "PageViewHits",
            column: "Path");

        migrationBuilder.CreateIndex(
            name: "IX_PageViewHits_ViewedAtUtc",
            table: "PageViewHits",
            column: "ViewedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordSetupTokens_TokenHash",
            table: "PasswordSetupTokens",
            column: "TokenHash");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordSetupTokens_UserId",
            table: "PasswordSetupTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_CreateDate",
            table: "Redirects",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_CreateUserId",
            table: "Redirects",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_DeleteUserId",
            table: "Redirects",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_IsActive",
            table: "Redirects",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_IsDeleted",
            table: "Redirects",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_IsDeleted_IsActive",
            table: "Redirects",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_SourcePageId",
            table: "Redirects",
            column: "SourcePageId");

        migrationBuilder.CreateIndex(
            name: "IX_Redirects_UpdateUserId",
            table: "Redirects",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_Redirects_OldPath",
            table: "Redirects",
            column: "OldPath",
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_CreateDate",
            table: "SiteCodeSnippets",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_CreateUserId",
            table: "SiteCodeSnippets",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_DeleteUserId",
            table: "SiteCodeSnippets",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_IsActive",
            table: "SiteCodeSnippets",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_IsDeleted",
            table: "SiteCodeSnippets",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_IsDeleted_IsActive",
            table: "SiteCodeSnippets",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_Placement_SortOrder",
            table: "SiteCodeSnippets",
            columns: new[] { "Placement", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteCodeSnippets_UpdateUserId",
            table: "SiteCodeSnippets",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SitemapCaches_GeneratedAt",
            table: "SitemapCaches",
            column: "GeneratedAt");

        migrationBuilder.CreateIndex(
            name: "UX_SitemapCaches_CacheKey",
            table: "SitemapCaches",
            column: "CacheKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_CreateDate",
            table: "SiteSettings",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_CreateUserId",
            table: "SiteSettings",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_DeleteUserId",
            table: "SiteSettings",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_Group",
            table: "SiteSettings",
            column: "Group");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_IsActive",
            table: "SiteSettings",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_IsDeleted",
            table: "SiteSettings",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_IsDeleted_IsActive",
            table: "SiteSettings",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_IsSystem",
            table: "SiteSettings",
            column: "IsSystem");

        migrationBuilder.CreateIndex(
            name: "IX_SiteSettings_UpdateUserId",
            table: "SiteSettings",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_SiteSettings_Key_Group",
            table: "SiteSettings",
            columns: new[] { "Key", "Group" },
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_CreateDate",
            table: "Tags",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_CreateUserId",
            table: "Tags",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_DeleteUserId",
            table: "Tags",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_IsActive",
            table: "Tags",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_IsDeleted",
            table: "Tags",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_IsDeleted_IsActive",
            table: "Tags",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_Tags_UpdateUserId",
            table: "Tags",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_Tags_Slug",
            table: "Tags",
            column: "Slug",
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_CreateDate",
            table: "UserNotes",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_CreateUserId",
            table: "UserNotes",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_DeleteUserId",
            table: "UserNotes",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_IsActive",
            table: "UserNotes",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_IsArchived",
            table: "UserNotes",
            column: "IsArchived");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_IsDeleted",
            table: "UserNotes",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_IsDeleted_IsActive",
            table: "UserNotes",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_IsPinned",
            table: "UserNotes",
            column: "IsPinned");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_UpdateUserId",
            table: "UserNotes",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_UserId",
            table: "UserNotes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserNotes_UserId_IsDeleted_IsArchived",
            table: "UserNotes",
            columns: new[] { "UserId", "IsDeleted", "IsArchived" });

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_CreateDate",
            table: "UserReminders",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_CreateUserId",
            table: "UserReminders",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_DeleteUserId",
            table: "UserReminders",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_IsActive",
            table: "UserReminders",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_IsCompleted",
            table: "UserReminders",
            column: "IsCompleted");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_IsDeleted",
            table: "UserReminders",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_IsDeleted_IsActive",
            table: "UserReminders",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_Pending",
            table: "UserReminders",
            columns: new[] { "IsCompleted", "IsDismissed", "RemindAt", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_RemindAt",
            table: "UserReminders",
            column: "RemindAt");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_UpdateUserId",
            table: "UserReminders",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReminders_UserId",
            table: "UserReminders",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_CreateDate",
            table: "UserTaskComments",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_CreateUserId",
            table: "UserTaskComments",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_DeleteUserId",
            table: "UserTaskComments",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_IsActive",
            table: "UserTaskComments",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_IsDeleted",
            table: "UserTaskComments",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_IsDeleted_IsActive",
            table: "UserTaskComments",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_UpdateUserId",
            table: "UserTaskComments",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_UserId",
            table: "UserTaskComments",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTaskComments_UserTaskId",
            table: "UserTaskComments",
            column: "UserTaskId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_AssignedByUserId",
            table: "UserTasks",
            column: "AssignedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_AssignedTo_Status_IsDeleted",
            table: "UserTasks",
            columns: new[] { "AssignedToUserId", "Status", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_AssignedToUserId",
            table: "UserTasks",
            column: "AssignedToUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_CreateDate",
            table: "UserTasks",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_CreateUserId",
            table: "UserTasks",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_DeleteUserId",
            table: "UserTasks",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_DueDate",
            table: "UserTasks",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_IsActive",
            table: "UserTasks",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_IsDeleted",
            table: "UserTasks",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_IsDeleted_IsActive",
            table: "UserTasks",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_ParentTaskId",
            table: "UserTasks",
            column: "ParentTaskId");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_Priority",
            table: "UserTasks",
            column: "Priority");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_Status",
            table: "UserTasks",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_UserTasks_UpdateUserId",
            table: "UserTasks",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_CreateDate",
            table: "WorkflowDefinitions",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_CreateUserId",
            table: "WorkflowDefinitions",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_DeleteUserId",
            table: "WorkflowDefinitions",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_IsActive",
            table: "WorkflowDefinitions",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_IsDeleted",
            table: "WorkflowDefinitions",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_IsDeleted_IsActive",
            table: "WorkflowDefinitions",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowDefinitions_UpdateUserId",
            table: "WorkflowDefinitions",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_CreateDate",
            table: "WorkflowSteps",
            column: "CreateDate");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_CreateUserId",
            table: "WorkflowSteps",
            column: "CreateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_DeleteUserId",
            table: "WorkflowSteps",
            column: "DeleteUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_IsActive",
            table: "WorkflowSteps",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_IsDeleted",
            table: "WorkflowSteps",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_IsDeleted_IsActive",
            table: "WorkflowSteps",
            columns: new[] { "IsDeleted", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_RequiredRoleId",
            table: "WorkflowSteps",
            column: "RequiredRoleId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_RequiredUserId",
            table: "WorkflowSteps",
            column: "RequiredUserId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_UpdateUserId",
            table: "WorkflowSteps",
            column: "UpdateUserId");

        migrationBuilder.CreateIndex(
            name: "UX_WorkflowSteps_Definition_Order",
            table: "WorkflowSteps",
            columns: new[] { "WorkflowDefinitionId", "StepOrder" },
            unique: true,
            filter: "\"IsDeleted\" = false");

        // Not in the model, so EF cannot generate these: trigram indexes behind
        // the CMS's case-insensitive "contains" searches (logs, users, media,
        // pages), and newest-first keyset indexes for the long lists.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        migrationBuilder.Sql("""CREATE INDEX "IX_AppLogs_Message_Lower_Trgm" ON "AppLogs" USING gin (lower("Message") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AppLogs_Path_Lower_Trgm" ON "AppLogs" USING gin (lower("Path") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AppLogs_Exception_Lower_Trgm" ON "AppLogs" USING gin (lower("Exception") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AuthEvents_IpAddress_Lower_Trgm" ON "AuthEvents" USING gin (lower("IpAddress") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AuthEvents_UserNameSnapshot_Lower_Trgm" ON "AuthEvents" USING gin (lower("UserNameSnapshot") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AspNetUsers_Email_Lower_Trgm" ON "AspNetUsers" USING gin (lower("Email") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AspNetUsers_UserName_Lower_Trgm" ON "AspNetUsers" USING gin (lower("UserName") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_MediaFiles_FileName_Lower_Trgm" ON "MediaFiles" USING gin (lower("FileName") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_MediaFiles_Title_Lower_Trgm" ON "MediaFiles" USING gin (lower("Title") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_PageInfos_SeoTitle_Lower_Trgm" ON "PageInfos" USING gin (lower("SeoTitle") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_PageContents_GjsHtml_Lower_Trgm" ON "PageContents" USING gin (lower("GjsHtml") gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_PageContents_GjsCss_Trgm" ON "PageContents" USING gin ("GjsCss" gin_trgm_ops);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_PageContents_GjsData_Trgm" ON "PageContents" USING gin ("GjsData" gin_trgm_ops);""");

        migrationBuilder.Sql("""CREATE INDEX "IX_AppLogs_CreatedAtUtc_Id" ON "AppLogs" ("CreatedAtUtc" DESC, "Id" DESC);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_AuthEvents_CreatedAtUtc_Id" ON "AuthEvents" ("CreatedAtUtc" DESC, "Id" DESC);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_MediaFiles_CreateDate_Id" ON "MediaFiles" ("CreateDate" DESC, "Id" DESC);""");
        migrationBuilder.Sql("""CREATE INDEX "IX_FormSubmissions_SubmittedAtUtc_Id" ON "FormSubmissions" ("SubmittedAtUtc" DESC, "Id" DESC);""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnnouncementReads");

        migrationBuilder.DropTable(
            name: "AppLogs");

        migrationBuilder.DropTable(
            name: "ApprovalStepDecisions");

        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "AuthEvents");

        migrationBuilder.DropTable(
            name: "ContentBulkEditItems");

        migrationBuilder.DropTable(
            name: "FormReplyTemplates");

        migrationBuilder.DropTable(
            name: "FormSubmissionAttachments");

        migrationBuilder.DropTable(
            name: "IntegrationSecrets");

        migrationBuilder.DropTable(
            name: "PageClickHits");

        migrationBuilder.DropTable(
            name: "PageContents");

        migrationBuilder.DropTable(
            name: "PageInfoSiteCodeExclusions");

        migrationBuilder.DropTable(
            name: "PageInfoTags");

        migrationBuilder.DropTable(
            name: "PageTemplates");

        migrationBuilder.DropTable(
            name: "PageViewHits");

        migrationBuilder.DropTable(
            name: "PasswordSetupTokens");

        migrationBuilder.DropTable(
            name: "Redirects");

        migrationBuilder.DropTable(
            name: "SitemapCaches");

        migrationBuilder.DropTable(
            name: "SiteSettings");

        migrationBuilder.DropTable(
            name: "UserReminders");

        migrationBuilder.DropTable(
            name: "UserTaskComments");

        migrationBuilder.DropTable(
            name: "WorkflowSteps");

        migrationBuilder.DropTable(
            name: "UserNotes");

        migrationBuilder.DropTable(
            name: "ApprovalRequests");

        migrationBuilder.DropTable(
            name: "ContentBulkEdits");

        migrationBuilder.DropTable(
            name: "FormSubmissions");

        migrationBuilder.DropTable(
            name: "SiteCodeSnippets");

        migrationBuilder.DropTable(
            name: "Tags");

        migrationBuilder.DropTable(
            name: "UserTasks");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "WorkflowDefinitions");

        migrationBuilder.DropTable(
            name: "PageInfos");

        migrationBuilder.DropTable(
            name: "Languages");

        migrationBuilder.DropTable(
            name: "PageGroups");

        migrationBuilder.DropTable(
            name: "MediaFiles");

        migrationBuilder.DropTable(
            name: "AspNetUsers");
    }
}
