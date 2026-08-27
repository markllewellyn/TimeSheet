using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegacySchemaAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseReportingCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    DefaultInvoiceMonthEndDay = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    PK_Currency = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrencyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.PK_Currency);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecordedTimesarc",
                columns: table => new
                {
                    PK_RecordedTimes = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Staff = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Customers = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Projects = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TaskDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ToPayroll = table.Column<decimal>(type: "TEXT", precision: 7, scale: 2, nullable: false),
                    ToCompany = table.Column<decimal>(type: "TEXT", precision: 7, scale: 2, nullable: false),
                    WorkHours = table.Column<decimal>(type: "TEXT", precision: 4, scale: 1, nullable: false),
                    OutOfHours = table.Column<decimal>(type: "TEXT", precision: 4, scale: 1, nullable: false),
                    ApprovedPayroll = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApprovedByStaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateApprovedPayroll = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SentToPayroll = table.Column<bool>(type: "INTEGER", nullable: false),
                    SentByStaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    SentByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateSentToPayroll = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PostingBatch = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ExpensesValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordedTimesarc", x => x.PK_RecordedTimes);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    PK_Staff = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntraObjectId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PayrollNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Admin = table.Column<bool>(type: "INTEGER", nullable: false),
                    HourlyCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    JobTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.PK_Staff);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyExchangeHistory",
                columns: table => new
                {
                    PK_History = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_Currency = table.Column<int>(type: "INTEGER", nullable: false),
                    OldExchangeRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    NewExchangeRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    ChangeDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchangeHistory", x => x.PK_History);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeHistory_Currency_PK_Currency",
                        column: x => x.PK_Currency,
                        principalTable: "Currency",
                        principalColumn: "PK_Currency",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    PK_Customers = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Account = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BillingAddressLine1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BillingAddressLine2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BillingCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BillingPostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BillingCountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    PrimaryContactPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PK_Currency = table.Column<int>(type: "INTEGER", nullable: true),
                    InvoicingMonthEndDay = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.PK_Customers);
                    table.ForeignKey(
                        name: "FK_Customers_Currency_PK_Currency",
                        column: x => x.PK_Currency,
                        principalTable: "Currency",
                        principalColumn: "PK_Currency",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipientUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RelatedProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    RelatedTimesheetEntryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Staff_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAccountManagers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAccountManagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientAccountManagers_Customers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAccountManagers_Staff_UserId",
                        column: x => x.UserId,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ReportingCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinalizedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    PdfContent = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    PK_Projects = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_Customers = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentModel = table.Column<int>(type: "INTEGER", nullable: false),
                    CanInvoice = table.Column<bool>(type: "INTEGER", nullable: true),
                    CurrencyOverride = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    BudgetHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    FixedFeeAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    BudgetAlertThresholdPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    LatestHealthAssessmentId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.PK_Projects);
                    table.ForeignKey(
                        name: "FK_Projects_Customers_PK_Customers",
                        column: x => x.PK_Customers,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffCosts",
                columns: table => new
                {
                    PK_StaffCosts = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_Staff = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Customers = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    OutOfHoursCost = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCosts", x => x.PK_StaffCosts);
                    table.ForeignKey(
                        name: "FK_StaffCosts_Customers_PK_Customers",
                        column: x => x.PK_Customers,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffCosts_Staff_PK_Staff",
                        column: x => x.PK_Staff,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsBillable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseEntries_Staff_UserId",
                        column: x => x.UserId,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Hours = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHealthAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssessedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ContributingFactorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: true),
                    PercentBudgetConsumed = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    PercentTimeElapsed = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    AiModelUsed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHealthAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHealthAssessments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecordedTimes",
                columns: table => new
                {
                    PK_RecordedTimes = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_Staff = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Customers = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Projects = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    WorkHours = table.Column<decimal>(type: "TEXT", precision: 4, scale: 1, nullable: false),
                    OutOfHours = table.Column<decimal>(type: "TEXT", precision: 4, scale: 1, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ToPayroll = table.Column<decimal>(type: "TEXT", precision: 7, scale: 2, nullable: false),
                    ToCompany = table.Column<decimal>(type: "TEXT", precision: 7, scale: 2, nullable: false),
                    ApprovedPayroll = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApprovedByStaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateApprovedPayroll = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SentToPayroll = table.Column<bool>(type: "INTEGER", nullable: false),
                    SentByStaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    SentByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateSentToPayroll = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PostingBatch = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ExpensesValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordedTimes", x => x.PK_RecordedTimes);
                    table.ForeignKey(
                        name: "FK_RecordedTimes_Customers_PK_Customers",
                        column: x => x.PK_Customers,
                        principalTable: "Customers",
                        principalColumn: "PK_Customers",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecordedTimes_Projects_PK_Projects",
                        column: x => x.PK_Projects,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecordedTimes_Staff_PK_Staff",
                        column: x => x.PK_Staff,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffCostsHistory",
                columns: table => new
                {
                    PK_History = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_StaffCosts = table.Column<int>(type: "INTEGER", nullable: false),
                    OldCustomerRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    NewCustomerRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    OldOutOfHoursCost = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    NewOutOfHoursCost = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    ChangeDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCostsHistory", x => x.PK_History);
                    table.ForeignKey(
                        name: "FK_StaffCostsHistory_StaffCosts_PK_StaffCosts",
                        column: x => x.PK_StaffCosts,
                        principalTable: "StaffCosts",
                        principalColumn: "PK_StaffCosts",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffProjects",
                columns: table => new
                {
                    PK_StaffProjects = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PK_StaffCosts = table.Column<int>(type: "INTEGER", nullable: false),
                    PK_Projects = table.Column<int>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllocatedHoursPerWeek = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProjects", x => x.PK_StaffProjects);
                    table.ForeignKey(
                        name: "FK_StaffProjects_Projects_PK_Projects",
                        column: x => x.PK_Projects,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffProjects_StaffCosts_PK_StaffCosts",
                        column: x => x.PK_StaffCosts,
                        principalTable: "StaffCosts",
                        principalColumn: "PK_StaffCosts",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimesheetEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_RecordedTimes_TimesheetEntryId",
                        column: x => x.TimesheetEntryId,
                        principalTable: "RecordedTimes",
                        principalColumn: "PK_RecordedTimes",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Escalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimesheetEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    BudgetLimitAtTimeOfEntry = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CumulativeValueAtTimeOfEntry = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DecisionNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Escalations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Escalations_RecordedTimes_TimesheetEntryId",
                        column: x => x.TimesheetEntryId,
                        principalTable: "RecordedTimes",
                        principalColumn: "PK_RecordedTimes",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "BaseReportingCurrency", "DefaultInvoiceMonthEndDay" },
                values: new object[] { 1, "GBP", 31 });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TimesheetEntryId",
                table: "Attachments",
                column: "TimesheetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccountManagers_ClientId_UserId",
                table: "ClientAccountManagers",
                columns: new[] { "ClientId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccountManagers_UserId",
                table: "ClientAccountManagers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeHistory_PK_Currency",
                table: "CurrencyExchangeHistory",
                column: "PK_Currency");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_BaseCurrency_QuoteCurrency_RateDate",
                table: "CurrencyRates",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "RateDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Account",
                table: "Customers",
                column: "Account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PK_Currency",
                table: "Customers",
                column: "PK_Currency");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_Decision",
                table: "Escalations",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_ProjectId",
                table: "Escalations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_TimesheetEntryId",
                table: "Escalations",
                column: "TimesheetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_ProjectId_Date",
                table: "ExpenseEntries",
                columns: new[] { "ProjectId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_UserId",
                table: "ExpenseEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_ProjectId",
                table: "InvoiceLineItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "ClientId", "InvoiceNumber" },
                unique: true,
                filter: "\"Status\" = 'Finalized'");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHealthAssessments_ProjectId_AssessedAtUtc",
                table: "ProjectHealthAssessments",
                columns: new[] { "ProjectId", "AssessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PK_Customers_Code",
                table: "Projects",
                columns: new[] { "PK_Customers", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordedTimes_PK_Customers_TaskDate",
                table: "RecordedTimes",
                columns: new[] { "PK_Customers", "TaskDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedTimes_PK_Projects_TaskDate",
                table: "RecordedTimes",
                columns: new[] { "PK_Projects", "TaskDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedTimes_PK_Staff_TaskDate",
                table: "RecordedTimes",
                columns: new[] { "PK_Staff", "TaskDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Email",
                table: "Staff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_EntraObjectId",
                table: "Staff",
                column: "EntraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffCosts_PK_Customers",
                table: "StaffCosts",
                column: "PK_Customers");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCosts_PK_Staff_PK_Customers",
                table: "StaffCosts",
                columns: new[] { "PK_Staff", "PK_Customers" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffCostsHistory_PK_StaffCosts",
                table: "StaffCostsHistory",
                column: "PK_StaffCosts");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProjects_PK_Projects",
                table: "StaffProjects",
                column: "PK_Projects");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProjects_PK_StaffCosts_PK_Projects",
                table: "StaffProjects",
                columns: new[] { "PK_StaffCosts", "PK_Projects" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "ClientAccountManagers");

            migrationBuilder.DropTable(
                name: "CurrencyExchangeHistory");

            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropTable(
                name: "Escalations");

            migrationBuilder.DropTable(
                name: "ExpenseEntries");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProjectHealthAssessments");

            migrationBuilder.DropTable(
                name: "RecordedTimesarc");

            migrationBuilder.DropTable(
                name: "StaffCostsHistory");

            migrationBuilder.DropTable(
                name: "StaffProjects");

            migrationBuilder.DropTable(
                name: "RecordedTimes");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "StaffCosts");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Currency");
        }
    }
}
