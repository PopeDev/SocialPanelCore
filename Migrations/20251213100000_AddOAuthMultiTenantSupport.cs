using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialPanelCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthMultiTenantSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Añadir nuevos campos a SocialChannelConfigs para OAuth multi-tenant
            migrationBuilder.AddColumn<int>(
                name: "ConnectionStatus",
                table: "SocialChannelConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0); // Connected

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "SocialChannelConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                table: "SocialChannelConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefreshAttemptAt",
                table: "SocialChannelConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefreshSuccessAt",
                table: "SocialChannelConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastOAuthErrorCode",
                table: "SocialChannelConfigs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Crear tabla OAuthStates para almacenar estado OAuth + PKCE
            migrationBuilder.CreateTable(
                name: "OAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetworkType = table.Column<int>(type: "integer", nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodeVerifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedScopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsConsumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthStates", x => x.Id);
                });

            // Índices para OAuthStates
            migrationBuilder.CreateIndex(
                name: "IX_OAuthStates_State",
                table: "OAuthStates",
                column: "State",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthStates_ExpiresAt",
                table: "OAuthStates",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar tabla OAuthStates
            migrationBuilder.DropTable(
                name: "OAuthStates");

            // Eliminar columnas añadidas a SocialChannelConfigs
            migrationBuilder.DropColumn(
                name: "ConnectionStatus",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "LastRefreshAttemptAt",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "LastRefreshSuccessAt",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "LastOAuthErrorCode",
                table: "SocialChannelConfigs");
        }
    }
}
