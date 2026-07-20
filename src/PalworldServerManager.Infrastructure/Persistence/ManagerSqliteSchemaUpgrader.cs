using Microsoft.EntityFrameworkCore;

namespace PalworldServerManager.Infrastructure.Persistence;

internal static class ManagerSqliteSchemaUpgrader
{
    public static async Task UpgradeAsync(ManagerDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var statement in Statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static readonly string[] Statements =
    [
        """
        CREATE TABLE IF NOT EXISTS "__ManagerSchemaVersions" (
            "Version" INTEGER NOT NULL CONSTRAINT "PK___ManagerSchemaVersions" PRIMARY KEY,
            "AppliedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "AuditRecords" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_AuditRecords" PRIMARY KEY,
            "OccurredAtUtc" TEXT NOT NULL,
            "Actor" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "Outcome" TEXT NOT NULL,
            "Detail" TEXT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_AuditRecords_OccurredAtUtc" ON "AuditRecords" ("OccurredAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetRoles" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
            "Name" TEXT NULL,
            "NormalizedName" TEXT NULL,
            "ConcurrencyStamp" TEXT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetUsers" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
            "CreatedAtUtc" TEXT NOT NULL,
            "LastSuccessfulLoginAtUtc" TEXT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "UserName" TEXT NULL,
            "NormalizedUserName" TEXT NULL,
            "Email" TEXT NULL,
            "NormalizedEmail" TEXT NULL,
            "EmailConfirmed" INTEGER NOT NULL,
            "PasswordHash" TEXT NULL,
            "SecurityStamp" TEXT NULL,
            "ConcurrencyStamp" TEXT NULL,
            "PhoneNumber" TEXT NULL,
            "PhoneNumberConfirmed" INTEGER NOT NULL,
            "TwoFactorEnabled" INTEGER NOT NULL,
            "LockoutEnd" TEXT NULL,
            "LockoutEnabled" INTEGER NOT NULL,
            "AccessFailedCount" INTEGER NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
            "RoleId" TEXT NOT NULL,
            "ClaimType" TEXT NULL,
            "ClaimValue" TEXT NULL,
            CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
            "UserId" TEXT NOT NULL,
            "ClaimType" TEXT NULL,
            "ClaimValue" TEXT NULL,
            CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
            "LoginProvider" TEXT NOT NULL,
            "ProviderKey" TEXT NOT NULL,
            "ProviderDisplayName" TEXT NULL,
            "UserId" TEXT NOT NULL,
            CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
            CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
            "UserId" TEXT NOT NULL,
            "RoleId" TEXT NOT NULL,
            CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
            CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
            "UserId" TEXT NOT NULL,
            "LoginProvider" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Value" TEXT NULL,
            CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
            CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
        );
        """,
        """
        INSERT OR IGNORE INTO "__ManagerSchemaVersions" ("Version", "AppliedAtUtc") VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
        """
    ];
}
