using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialPanelCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthMethodAndApiKeyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthMethod",
                table: "SocialChannelConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "SocialChannelConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiSecret",
                table: "SocialChannelConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessTokenSecret",
                table: "SocialChannelConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalChannelId",
                table: "SocialChannelConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserId",
                table: "SocialChannelConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthMethod",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "ApiSecret",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "AccessTokenSecret",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "ExternalChannelId",
                table: "SocialChannelConfigs");

            migrationBuilder.DropColumn(
                name: "ExternalUserId",
                table: "SocialChannelConfigs");
        }
    }
}
