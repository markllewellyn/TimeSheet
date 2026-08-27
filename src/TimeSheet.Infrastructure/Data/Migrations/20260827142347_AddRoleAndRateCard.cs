using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Hand-written, not auto-diffed. StaffProjects/Staff/StaffCosts are rebuilt via explicit raw SQL
    /// (CREATE new table, INSERT ... SELECT, DROP old, RENAME) rather than MigrationBuilder's Add/DropColumn/
    /// DropForeignKey - EF's Sqlite provider batches those into a deferred "rebuild" per table, and interleaving
    /// a raw data Sql() call on a table mid-rebuild breaks that batching (confirmed by testing against a copy
    /// of the dev DB: "SQLite does not support this migration operation" once a second drop hit an
    /// already-pending rebuild). Raw SQL sidesteps the batching entirely and gives full control over exactly
    /// when each old table is read from before it's replaced.
    ///
    /// Sequenced so every data-preserving step reads from a still-intact source before that source is dropped:
    ///   1. Create Roles/RateCards (additive - safe via MigrationBuilder, no drops involved).
    ///   2. Seed RateCards (person+client tier) from the OLD StaffCosts shape, untouched at this point.
    ///   3. Rebuild StaffProjects to link directly to Staff (was StaffCosts-mediated) - needs OLD StaffCosts to
    ///      map PK_StaffCosts -> PK_Staff, so happens before StaffCosts is touched.
    ///   4. Build the NEW-meaning StaffCost table (one effective-dated row per staff member, not per client) -
    ///      needs OLD Staff.HourlyCost and OLD StaffCosts.OutOfHoursCost simultaneously, so happens before
    ///      either source is dropped.
    ///   5. Only now drop the OLD StaffCosts table and StaffCostsHistory (pure audit trail, no downstream
    ///      reader once StaffCostsFunctions is replaced) - both fully drained by steps 2-4 above.
    ///   6. Rebuild Staff: drop HourlyCost (captured in step 4) and JobTitle, add JobRoleId (no legacy source -
    ///      starts null for everyone).
    ///   7. Add TimesheetEntry's rate/cost snapshot columns (simple nullable adds - no rebuild needed) and
    ///      backfill them from the RateCards/StaffCosts built in steps 2/4, using the same (StaffId, ClientId)
    ///      join RateCards was seeded from - so every pre-existing entry gets its historical rate/cost
    ///      immediately, not just newly-created ones.
    /// EffectiveFrom for every backfilled row uses the earliest TimesheetEntry date in the system (or
    /// 2000-01-01 on an empty DB) so no existing entry/report/invoice ever fails to resolve a rate.
    /// </remarks>
    public partial class AddRoleAndRateCard : Migration
    {
        private const string EffectiveFromExpr = "COALESCE((SELECT MIN(TaskDate) FROM RecordedTimes), '2000-01-01')";
        private const string NowExpr = "strftime('%Y-%m-%d %H:%M:%f000+00:00', 'now')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Several tables below (StaffProjects, Staff, StaffCosts) are DROPped and replaced while OTHER
            // tables still hold FK references pointing at them (e.g. RateCards/StaffCosts/StaffProjects all
            // reference Staff) - Microsoft.Data.Sqlite enforces FK constraints by default, which would block
            // those drops. PRAGMA foreign_keys can't be toggled inside a transaction, hence suppressTransaction.
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            // 1. Roles / RateCards - additive, safe via MigrationBuilder.
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RateCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: true),
                    StaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateCards", x => x.Id);
                    table.CheckConstraint("CK_RateCard_NotBothClientProject", "\"ClientId\" IS NULL OR \"ProjectId\" IS NULL");
                    table.CheckConstraint("CK_RateCard_ScopeXor", "(\"RoleId\" IS NOT NULL AND \"StaffId\" IS NULL) OR (\"RoleId\" IS NULL AND \"StaffId\" IS NOT NULL)");
                    table.CheckConstraint("CK_RateCard_StaffRequiresScope", "\"StaffId\" IS NULL OR \"ClientId\" IS NOT NULL OR \"ProjectId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_RateCards_Customers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateCards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateCards_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateCards_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_Roles_Name", table: "Roles", column: "Name", unique: true);
            migrationBuilder.CreateIndex(name: "IX_RateCards_ClientId", table: "RateCards", column: "ClientId");
            migrationBuilder.CreateIndex(name: "IX_RateCards_ProjectId", table: "RateCards", column: "ProjectId");
            migrationBuilder.CreateIndex(name: "IX_RateCards_StaffId", table: "RateCards", column: "StaffId");
            migrationBuilder.CreateIndex(
                name: "IX_RateCards_RoleId_StaffId_ClientId_ProjectId_EffectiveFrom",
                table: "RateCards",
                columns: new[] { "RoleId", "StaffId", "ClientId", "ProjectId", "EffectiveFrom" },
                unique: true);

            // 2. Seed RateCards (person+client tier) from every existing old-shape StaffCosts row.
            migrationBuilder.Sql($@"
                INSERT INTO RateCards (StaffId, ClientId, Rate, EffectiveFrom, CreatedUtc)
                SELECT PK_Staff, PK_Customers, CustomerRate, {EffectiveFromExpr}, {NowExpr}
                FROM StaffCosts;
            ");

            // 3. Rebuild StaffProjects to link directly to Staff instead of via StaffCosts.
            migrationBuilder.Sql(@"
                CREATE TABLE StaffProjects_New (
                    PK_StaffProjects INTEGER NOT NULL CONSTRAINT PK_StaffProjects_New PRIMARY KEY AUTOINCREMENT,
                    PK_Staff INTEGER NOT NULL,
                    PK_Projects INTEGER NOT NULL,
                    Active INTEGER NOT NULL,
                    AllocatedHoursPerWeek TEXT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NULL,
                    Notes TEXT NULL,
                    CreatedUtc TEXT NOT NULL,
                    CreatedByUserId INTEGER NULL,
                    CONSTRAINT FK_StaffProjects_Projects_PK_Projects FOREIGN KEY (PK_Projects) REFERENCES Projects (PK_Projects) ON DELETE CASCADE,
                    CONSTRAINT FK_StaffProjects_Staff_PK_Staff FOREIGN KEY (PK_Staff) REFERENCES Staff (PK_Staff) ON DELETE RESTRICT
                );

                INSERT INTO StaffProjects_New (PK_StaffProjects, PK_Staff, PK_Projects, Active, AllocatedHoursPerWeek, StartDate, EndDate, Notes, CreatedUtc, CreatedByUserId)
                SELECT sp.PK_StaffProjects, sc.PK_Staff, sp.PK_Projects, sp.Active, sp.AllocatedHoursPerWeek, sp.StartDate, sp.EndDate, sp.Notes, sp.CreatedUtc, sp.CreatedByUserId
                FROM StaffProjects sp
                JOIN StaffCosts sc ON sc.PK_StaffCosts = sp.PK_StaffCosts;

                DROP TABLE StaffProjects;
                ALTER TABLE StaffProjects_New RENAME TO StaffProjects;

                CREATE UNIQUE INDEX IX_StaffProjects_PK_Staff_PK_Projects ON StaffProjects (PK_Staff, PK_Projects) WHERE Active = 1;
            ");

            // 4. Build the NEW-meaning StaffCost table (one effective-dated row per staff member) while the OLD
            // Staff.HourlyCost and OLD StaffCosts.OutOfHoursCost are both still readable. OutOfHoursCost has no
            // single legacy source (it was per-client before) - MAX() per staff is used since the field was
            // never actually read by any calculation before this feature, so any single value is safe.
            migrationBuilder.Sql($@"
                CREATE TABLE StaffCosts_New (
                    Id INTEGER NOT NULL CONSTRAINT PK_StaffCosts_New PRIMARY KEY AUTOINCREMENT,
                    StaffId INTEGER NOT NULL,
                    HourlyCost TEXT NOT NULL,
                    OutOfHoursCost TEXT NOT NULL,
                    EffectiveFrom TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    CreatedByUserId INTEGER NULL,
                    CONSTRAINT FK_StaffCosts_Staff_StaffId FOREIGN KEY (StaffId) REFERENCES Staff (PK_Staff) ON DELETE RESTRICT
                );

                INSERT INTO StaffCosts_New (StaffId, HourlyCost, OutOfHoursCost, EffectiveFrom, CreatedUtc)
                SELECT s.PK_Staff,
                       COALESCE(s.HourlyCost, 0),
                       COALESCE((SELECT MAX(sc.OutOfHoursCost) FROM StaffCosts sc WHERE sc.PK_Staff = s.PK_Staff), 0),
                       {EffectiveFromExpr},
                       {NowExpr}
                FROM Staff s;
            ");

            // 5. Now safe to drop the OLD per-client StaffCosts table and the pure-audit StaffCostsHistory table
            // (no downstream reader once StaffCostsFunctions is replaced) - both fully drained by steps 2-4.
            migrationBuilder.Sql(@"
                DROP TABLE StaffCostsHistory;
                DROP TABLE StaffCosts;
                ALTER TABLE StaffCosts_New RENAME TO StaffCosts;
                CREATE UNIQUE INDEX IX_StaffCosts_StaffId_EffectiveFrom ON StaffCosts (StaffId, EffectiveFrom);
            ");

            // 6. Rebuild Staff: drop HourlyCost (captured in step 4) and JobTitle, add JobRoleId.
            migrationBuilder.Sql(@"
                CREATE TABLE Staff_New (
                    PK_Staff INTEGER NOT NULL CONSTRAINT PK_Staff_New PRIMARY KEY AUTOINCREMENT,
                    EntraObjectId TEXT NULL,
                    PasswordHash TEXT NULL,
                    Email TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    PayrollNumber TEXT NOT NULL,
                    Admin INTEGER NOT NULL,
                    JobRoleId INTEGER NULL,
                    Active INTEGER NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    ModifiedUtc TEXT NULL,
                    CONSTRAINT FK_Staff_Roles_JobRoleId FOREIGN KEY (JobRoleId) REFERENCES Roles (Id) ON DELETE RESTRICT
                );

                INSERT INTO Staff_New (PK_Staff, EntraObjectId, PasswordHash, Email, FullName, PayrollNumber, Admin, JobRoleId, Active, CreatedUtc, ModifiedUtc)
                SELECT PK_Staff, EntraObjectId, PasswordHash, Email, FullName, PayrollNumber, Admin, NULL, Active, CreatedUtc, ModifiedUtc
                FROM Staff;

                DROP TABLE Staff;
                ALTER TABLE Staff_New RENAME TO Staff;

                CREATE UNIQUE INDEX ""IX_Staff_EntraObjectId"" ON Staff (EntraObjectId);
                CREATE UNIQUE INDEX ""IX_Staff_Email"" ON Staff (Email);
                CREATE INDEX ""IX_Staff_JobRoleId"" ON Staff (JobRoleId);
            ");

            // 7. TimesheetEntry (RecordedTimes) snapshot columns - simple nullable adds, no rebuild needed -
            // backfilled from the RateCards/StaffCosts built in steps 2/4, via the same (StaffId, ClientId)
            // join RateCards was seeded from.
            migrationBuilder.AddColumn<decimal>(name: "ResolvedCustomerRate", table: "RecordedTimes", type: "TEXT", precision: 18, scale: 2, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ResolvedHourlyCost", table: "RecordedTimes", type: "TEXT", precision: 18, scale: 2, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ResolvedOutOfHoursCost", table: "RecordedTimes", type: "TEXT", precision: 18, scale: 2, nullable: true);
            migrationBuilder.AddColumn<int>(name: "RateCardId", table: "RecordedTimes", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "StaffCostId", table: "RecordedTimes", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Tier", table: "RecordedTimes", type: "TEXT", maxLength: 30, nullable: true);

            migrationBuilder.Sql(@"
                UPDATE RecordedTimes
                SET RateCardId = (SELECT rc.Id FROM RateCards rc WHERE rc.StaffId = RecordedTimes.PK_Staff AND rc.ClientId = RecordedTimes.PK_Customers),
                    ResolvedCustomerRate = (SELECT rc.Rate FROM RateCards rc WHERE rc.StaffId = RecordedTimes.PK_Staff AND rc.ClientId = RecordedTimes.PK_Customers),
                    StaffCostId = (SELECT sc.Id FROM StaffCosts sc WHERE sc.StaffId = RecordedTimes.PK_Staff),
                    ResolvedHourlyCost = (SELECT sc.HourlyCost FROM StaffCosts sc WHERE sc.StaffId = RecordedTimes.PK_Staff),
                    ResolvedOutOfHoursCost = (SELECT sc.OutOfHoursCost FROM StaffCosts sc WHERE sc.StaffId = RecordedTimes.PK_Staff),
                    Tier = 'PersonClient'
                WHERE EXISTS (SELECT 1 FROM RateCards rc WHERE rc.StaffId = RecordedTimes.PK_Staff AND rc.ClientId = RecordedTimes.PK_Customers);
            ");

            migrationBuilder.CreateIndex(name: "IX_RecordedTimes_RateCardId", table: "RecordedTimes", column: "RateCardId");
            migrationBuilder.CreateIndex(name: "IX_RecordedTimes_StaffCostId", table: "RecordedTimes", column: "StaffCostId");
            migrationBuilder.AddForeignKey(
                name: "FK_RecordedTimes_RateCards_RateCardId",
                table: "RecordedTimes",
                column: "RateCardId",
                principalTable: "RateCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_RecordedTimes_StaffCosts_StaffCostId",
                table: "RecordedTimes",
                column: "StaffCostId",
                principalTable: "StaffCosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }

        /// <inheritdoc />
        /// <remarks>Best-effort schema reversal only - the original per-client StaffCosts rates and
        /// StaffCostsHistory audit trail are not reconstructible once dropped, so rolling back restores the old
        /// table SHAPES with empty data, not the original rows. Acceptable for a migration that's expected to
        /// move forward only; documented here so it's never mistaken for a full data-preserving rollback.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.DropForeignKey(name: "FK_RecordedTimes_RateCards_RateCardId", table: "RecordedTimes");
            migrationBuilder.DropForeignKey(name: "FK_RecordedTimes_StaffCosts_StaffCostId", table: "RecordedTimes");
            migrationBuilder.DropIndex(name: "IX_RecordedTimes_RateCardId", table: "RecordedTimes");
            migrationBuilder.DropIndex(name: "IX_RecordedTimes_StaffCostId", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "ResolvedCustomerRate", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "ResolvedHourlyCost", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "ResolvedOutOfHoursCost", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "RateCardId", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "StaffCostId", table: "RecordedTimes");
            migrationBuilder.DropColumn(name: "Tier", table: "RecordedTimes");

            migrationBuilder.Sql(@"
                CREATE TABLE Staff_Old (
                    PK_Staff INTEGER NOT NULL CONSTRAINT PK_Staff PRIMARY KEY AUTOINCREMENT,
                    EntraObjectId TEXT NULL,
                    PasswordHash TEXT NULL,
                    Email TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    PayrollNumber TEXT NOT NULL,
                    Admin INTEGER NOT NULL,
                    HourlyCost TEXT NULL,
                    JobTitle TEXT NULL,
                    Active INTEGER NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    ModifiedUtc TEXT NULL
                );

                INSERT INTO Staff_Old (PK_Staff, EntraObjectId, PasswordHash, Email, FullName, PayrollNumber, Admin, HourlyCost, JobTitle, Active, CreatedUtc, ModifiedUtc)
                SELECT PK_Staff, EntraObjectId, PasswordHash, Email, FullName, PayrollNumber, Admin, NULL, NULL, Active, CreatedUtc, ModifiedUtc
                FROM Staff;

                DROP TABLE Staff;
                ALTER TABLE Staff_Old RENAME TO Staff;

                CREATE UNIQUE INDEX ""IX_Staff_EntraObjectId"" ON Staff (EntraObjectId);
                CREATE UNIQUE INDEX ""IX_Staff_Email"" ON Staff (Email);
            ");

            migrationBuilder.Sql(@"
                DROP TABLE StaffCosts;

                CREATE TABLE StaffCosts (
                    PK_StaffCosts INTEGER NOT NULL CONSTRAINT PK_StaffCosts PRIMARY KEY AUTOINCREMENT,
                    PK_Staff INTEGER NOT NULL,
                    PK_Customers INTEGER NOT NULL,
                    CustomerRate TEXT NOT NULL,
                    OutOfHoursCost TEXT NOT NULL,
                    CONSTRAINT FK_StaffCosts_Customers_PK_Customers FOREIGN KEY (PK_Customers) REFERENCES Customers (PK_Customers) ON DELETE CASCADE,
                    CONSTRAINT FK_StaffCosts_Staff_PK_Staff FOREIGN KEY (PK_Staff) REFERENCES Staff (PK_Staff) ON DELETE RESTRICT
                );
                CREATE INDEX IX_StaffCosts_PK_Customers ON StaffCosts (PK_Customers);
                CREATE UNIQUE INDEX IX_StaffCosts_PK_Staff_PK_Customers ON StaffCosts (PK_Staff, PK_Customers);

                CREATE TABLE StaffCostsHistory (
                    PK_History INTEGER NOT NULL CONSTRAINT PK_StaffCostsHistory PRIMARY KEY AUTOINCREMENT,
                    PK_StaffCosts INTEGER NOT NULL,
                    ChangeDate TEXT NOT NULL,
                    NewCustomerRate TEXT NOT NULL,
                    NewOutOfHoursCost TEXT NOT NULL,
                    OldCustomerRate TEXT NOT NULL,
                    OldOutOfHoursCost TEXT NOT NULL,
                    CONSTRAINT FK_StaffCostsHistory_StaffCosts_PK_StaffCosts FOREIGN KEY (PK_StaffCosts) REFERENCES StaffCosts (PK_StaffCosts) ON DELETE CASCADE
                );
                CREATE INDEX IX_StaffCostsHistory_PK_StaffCosts ON StaffCostsHistory (PK_StaffCosts);
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE StaffProjects_Old (
                    PK_StaffProjects INTEGER NOT NULL CONSTRAINT PK_StaffProjects PRIMARY KEY AUTOINCREMENT,
                    PK_StaffCosts INTEGER NOT NULL,
                    PK_Projects INTEGER NOT NULL,
                    Active INTEGER NOT NULL,
                    AllocatedHoursPerWeek TEXT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NULL,
                    Notes TEXT NULL,
                    CreatedUtc TEXT NOT NULL,
                    CreatedByUserId INTEGER NULL,
                    CONSTRAINT FK_StaffProjects_Projects_PK_Projects FOREIGN KEY (PK_Projects) REFERENCES Projects (PK_Projects) ON DELETE CASCADE,
                    CONSTRAINT FK_StaffProjects_StaffCosts_PK_StaffCosts FOREIGN KEY (PK_StaffCosts) REFERENCES StaffCosts (PK_StaffCosts) ON DELETE RESTRICT
                );

                DROP TABLE StaffProjects;
                ALTER TABLE StaffProjects_Old RENAME TO StaffProjects;

                CREATE UNIQUE INDEX IX_StaffProjects_PK_StaffCosts_PK_Projects ON StaffProjects (PK_StaffCosts, PK_Projects) WHERE Active = 1;
            ");

            migrationBuilder.DropTable(name: "RateCards");
            migrationBuilder.DropTable(name: "Roles");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
