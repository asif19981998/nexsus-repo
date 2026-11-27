using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// The UGC Soft Skill Training module. Shared: a Universities list that education lines may point to, and trainer
    /// profiles for university teachers. Module: policy, topics, grade bands, batches, per-university cohorts, trainers,
    /// applications, trainees, sessions, attendance, assessments, stipends and learning reports.
    /// Existing education lines are linked to universities by the seeder, matched on name.
    /// </summary>
    public partial class UniversitiesAndSoftSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UniversityId",
                table: "Educations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SoftSkillBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApplyFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ApplyTo = table.Column<DateOnly>(type: "date", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverrideById = table.Column<long>(type: "bigint", nullable: true),
                    OverrideAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillGradeBands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameBn = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinAverage = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillGradeBands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillPolicy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeatsPerUniversity = table.Column<int>(type: "int", nullable: false),
                    MaxBatchesPerYear = table.Column<int>(type: "int", nullable: false),
                    MinStudyYear = table.Column<int>(type: "int", nullable: false),
                    MaxStudyYear = table.Column<int>(type: "int", nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    TotalHours = table.Column<int>(type: "int", nullable: false),
                    MonthlyStipend = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinAttendancePct = table.Column<int>(type: "int", nullable: false),
                    StipendMinAttendancePct = table.Column<int>(type: "int", nullable: true),
                    ApprovedLeaveCountsAsPresent = table.Column<bool>(type: "bit", nullable: false),
                    ReportWorkingDays = table.Column<int>(type: "int", nullable: false),
                    AssessmentMaxMarks = table.Column<int>(type: "int", nullable: false),
                    AssessmentPassMarks = table.Column<int>(type: "int", nullable: false),
                    MinStatementLength = table.Column<int>(type: "int", nullable: false),
                    MinReportLength = table.Column<int>(type: "int", nullable: false),
                    MinFinalReportLength = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillTopics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameBn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Hours = table.Column<int>(type: "int", nullable: false),
                    AssessmentNo = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Universities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameBn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillCohorts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    UniversityId = table.Column<long>(type: "bigint", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoordinatorId = table.Column<long>(type: "bigint", nullable: true),
                    Venue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FinalReport = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    FinalRecommendation = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    FinalReportAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalReportById = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillCohorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillCohorts_SoftSkillBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "SoftSkillBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftSkillCohorts_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillCohorts_Users_CoordinatorId",
                        column: x => x.CoordinatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillTrainers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    UniversityId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedById = table.Column<long>(type: "bigint", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotCompleted = table.Column<bool>(type: "bit", nullable: false),
                    TotCompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    TotReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillTrainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillTrainers_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillTrainers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainerProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    UniversityId = table.Column<long>(type: "bigint", nullable: true),
                    AcademicDepartment = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmployeeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerProfiles_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillApplications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Basis = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Documents = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScreeningScore = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillApplications_SoftSkillBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "SoftSkillBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillApplications_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillApplications_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillAssessments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    No = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MaxMarks = table.Column<int>(type: "int", nullable: false),
                    PassMarks = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillAssessments_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    TopicId = table.Column<long>(type: "bigint", nullable: false),
                    TrainerId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillSessions_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftSkillSessions_SoftSkillTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "SoftSkillTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillSessions_Users_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillApplicationHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ById = table.Column<long>(type: "bigint", nullable: true),
                    ByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillApplicationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillApplicationHistory_SoftSkillApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "SoftSkillApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillTrainees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AverageScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CertificateGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CertificateIssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutcomeReasons = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillTrainees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillTrainees_SoftSkillApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "SoftSkillApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftSkillTrainees_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillAssessmentResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssessmentId = table.Column<long>(type: "bigint", nullable: false),
                    TraineeId = table.Column<long>(type: "bigint", nullable: false),
                    Marks = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Absent = table.Column<bool>(type: "bit", nullable: false),
                    Malpractice = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnteredById = table.Column<long>(type: "bigint", nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillAssessmentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillAssessmentResults_SoftSkillAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "SoftSkillAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftSkillAssessmentResults_SoftSkillTrainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "SoftSkillTrainees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillAttendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    TraineeId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    ReasonType = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MarkedById = table.Column<long>(type: "bigint", nullable: true),
                    MarkedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillAttendance_SoftSkillSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "SoftSkillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftSkillAttendance_SoftSkillTrainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "SoftSkillTrainees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillLearningReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraineeId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedById = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillLearningReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillLearningReports_SoftSkillTrainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "SoftSkillTrainees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillStipends",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraineeId = table.Column<long>(type: "bigint", nullable: false),
                    AssessmentNo = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidById = table.Column<long>(type: "bigint", nullable: true),
                    PaidByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PaidAmount = table.Column<int>(type: "int", nullable: true),
                    SlipNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WithheldReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WithheldAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithheldById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillStipends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillStipends_SoftSkillTrainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "SoftSkillTrainees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Educations_UniversityId",
                table: "Educations",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillApplicationHistory_ApplicationId",
                table: "SoftSkillApplicationHistory",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillApplications_BatchId_Status",
                table: "SoftSkillApplications",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillApplications_CohortId_Status",
                table: "SoftSkillApplications",
                columns: new[] { "CohortId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillApplications_StudentId",
                table: "SoftSkillApplications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillAssessmentResults_AssessmentId_TraineeId",
                table: "SoftSkillAssessmentResults",
                columns: new[] { "AssessmentId", "TraineeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillAssessmentResults_TraineeId",
                table: "SoftSkillAssessmentResults",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillAssessments_CohortId_No",
                table: "SoftSkillAssessments",
                columns: new[] { "CohortId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillAttendance_SessionId_TraineeId",
                table: "SoftSkillAttendance",
                columns: new[] { "SessionId", "TraineeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillAttendance_TraineeId",
                table: "SoftSkillAttendance",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillBatches_Code",
                table: "SoftSkillBatches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillBatches_Year_Status",
                table: "SoftSkillBatches",
                columns: new[] { "Year", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillCohorts_BatchId_UniversityId",
                table: "SoftSkillCohorts",
                columns: new[] { "BatchId", "UniversityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillCohorts_CoordinatorId",
                table: "SoftSkillCohorts",
                column: "CoordinatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillCohorts_UniversityId",
                table: "SoftSkillCohorts",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillLearningReports_TraineeId",
                table: "SoftSkillLearningReports",
                column: "TraineeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillSessions_CohortId_Date",
                table: "SoftSkillSessions",
                columns: new[] { "CohortId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillSessions_TopicId",
                table: "SoftSkillSessions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillSessions_TrainerId_Date",
                table: "SoftSkillSessions",
                columns: new[] { "TrainerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillStipends_TraineeId_AssessmentNo",
                table: "SoftSkillStipends",
                columns: new[] { "TraineeId", "AssessmentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTopics_Code",
                table: "SoftSkillTopics",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTrainees_ApplicationId",
                table: "SoftSkillTrainees",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTrainees_CertificateNumber",
                table: "SoftSkillTrainees",
                column: "CertificateNumber",
                unique: true,
                filter: "[CertificateNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTrainees_CohortId",
                table: "SoftSkillTrainees",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTrainers_UniversityId_Status",
                table: "SoftSkillTrainers",
                columns: new[] { "UniversityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillTrainers_UserId",
                table: "SoftSkillTrainers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainerProfiles_UniversityId",
                table: "TrainerProfiles",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerProfiles_UserId",
                table: "TrainerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Universities_Name",
                table: "Universities",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Educations_Universities_UniversityId",
                table: "Educations",
                column: "UniversityId",
                principalTable: "Universities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Educations_Universities_UniversityId",
                table: "Educations");

            migrationBuilder.DropTable(
                name: "SoftSkillApplicationHistory");

            migrationBuilder.DropTable(
                name: "SoftSkillAssessmentResults");

            migrationBuilder.DropTable(
                name: "SoftSkillAttendance");

            migrationBuilder.DropTable(
                name: "SoftSkillGradeBands");

            migrationBuilder.DropTable(
                name: "SoftSkillLearningReports");

            migrationBuilder.DropTable(
                name: "SoftSkillPolicy");

            migrationBuilder.DropTable(
                name: "SoftSkillStipends");

            migrationBuilder.DropTable(
                name: "SoftSkillTrainers");

            migrationBuilder.DropTable(
                name: "TrainerProfiles");

            migrationBuilder.DropTable(
                name: "SoftSkillAssessments");

            migrationBuilder.DropTable(
                name: "SoftSkillSessions");

            migrationBuilder.DropTable(
                name: "SoftSkillTrainees");

            migrationBuilder.DropTable(
                name: "SoftSkillTopics");

            migrationBuilder.DropTable(
                name: "SoftSkillApplications");

            migrationBuilder.DropTable(
                name: "SoftSkillCohorts");

            migrationBuilder.DropTable(
                name: "SoftSkillBatches");

            migrationBuilder.DropTable(
                name: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_Educations_UniversityId",
                table: "Educations");

            migrationBuilder.DropColumn(
                name: "UniversityId",
                table: "Educations");
        }
    }
}
