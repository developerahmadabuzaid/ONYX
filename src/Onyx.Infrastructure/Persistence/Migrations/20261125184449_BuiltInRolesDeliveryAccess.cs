using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data only: the built-in roles (ContentManager, Marketing, Sales) now include Delivery.Access, so employees can use the
    /// project workspace. New databases get it from the seeded bundles; this brings roles that already exist up to date.
    /// Idempotent; custom roles and SuperAdmin (which holds every permission implicitly) are not touched.
    /// </summary>
    public partial class BuiltInRolesDeliveryAccess : Migration
    {
        private const string RoleNames = "('CONTENTMANAGER', 'MARKETING', 'SALES')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO role_permissions (role_id, permission)
                SELECT r.id, 'Delivery.Access'
                FROM roles r
                WHERE r.normalized_name IN {RoleNames}
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.id AND rp.permission = 'Delivery.Access');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM role_permissions rp
                USING roles r
                WHERE rp.role_id = r.id AND r.normalized_name IN {RoleNames} AND rp.permission = 'Delivery.Access';
                """);
        }
    }
}
