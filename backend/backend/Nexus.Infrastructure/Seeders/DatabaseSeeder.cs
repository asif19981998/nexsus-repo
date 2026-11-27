using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Application.Common;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Internships;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Seeders;

/// <summary>
/// Runs at start-up. Applies migrations, then makes sure the reference data exists:
/// roles, the permission catalogue, default role grants (only for a role that has none, so database edits are kept),
/// the policy row, departments, evaluation criteria, grade bands and missing document types (matched by code, so edits are kept). With Seed:DemoData on, an empty database also
/// gets the demo users, batches and applications that the frontend prototype shipped with.
/// </summary>
public sealed partial class DatabaseSeeder(
    NexusDbContext db, UserManager<User> users, RoleManager<Role> roles, IConfiguration config, IClock clock, ILogger<DatabaseSeeder> log)
{
    public async Task SeedAsync()
    {
        await db.Database.MigrateAsync();
        await SeedRolesAsync();
        await SeedPermissionsAsync();
        await SeedReferenceDataAsync();
        await SeedUniversitiesAsync();
        await SeedSoftSkillReferenceAsync();
        await SeedDocumentTypesAsync();
        if (config.GetValue("Seed:DemoData", true) && !await db.InternshipBatches.AnyAsync())
            await SeedDemoAsync();
        else
            await EnsureAdminAsync();
        await SeedSoftSkillDemoAsync();
        if (config.GetValue("Seed:DemoData", true))
            await EnsureUserAsync("monitor@nexus.demo", "Prof. Dr. Rafiqul Islam", Roles.Monitor, "Professor, Institute of Business Administration (expert)");
    }

    // ---------- roles and permissions ----------

    private async Task SeedRolesAsync()
    {
        var descriptions = new Dictionary<string, string>
        {
            [Roles.Admin] = "UGC administration: batches, selection, placement, allowance, certificates and users.",
            [Roles.Student] = "Applicant and intern.",
            [Roles.Mentor] = "Department supervisor: attendance, leave, final report and evaluation of assigned interns.",
            [Roles.Trainer] = "University teacher: Soft Skill sessions, attendance and marks; coordinates a university group when nominated.",
            [Roles.Monitor] = "Academia or industry expert: Soft Skill monitoring visits and reports (cl.14 ঘ).",
        };
        foreach (var name in Roles.All)
            if (!await roles.RoleExistsAsync(name))
                await roles.CreateAsync(new Role(name, descriptions[name]));
    }

    private async Task SeedPermissionsAsync()
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code);
        var added = new HashSet<string>();
        foreach (var def in Permissions.All)
        {
            if (existing.TryGetValue(def.Code, out var p))
            {
                p.Name = def.Name;
                p.Module = def.Module;
            }
            else
            {
                db.Permissions.Add(new Permission { Code = def.Code, Name = def.Name, Module = def.Module });
                added.Add(def.Code);
            }
        }
        await db.SaveChangesAsync();

        // A role with no grants gets its whole default list. A role that has grants gets only the codes added to the
        // catalogue just now (a new module), so grants someone removed in the database are not put back.
        var byCode = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);
        foreach (var (roleName, codes) in Permissions.Defaults)
        {
            var role = await db.Roles.FirstAsync(r => r.Name == roleName);
            var fresh = !await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id);
            var grant = fresh ? codes : codes.Where(added.Contains).ToArray();
            foreach (var code in grant)
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = byCode[code] });
            if (grant.Length > 0) log.LogInformation("Granted {Count} default permissions to {Role}", grant.Length, roleName);
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedReferenceDataAsync()
    {
        if (!await db.InternshipPolicies.AnyAsync()) db.InternshipPolicies.Add(new InternshipPolicy());

        if (!await db.Departments.AnyAsync())
        {
            string[] names = ["Administration", "Procurement", "Research", "Science & Technology", "Accounts & Budget", "ICT"];
            for (var i = 0; i < names.Length; i++) db.Departments.Add(new Department { Name = names[i], SortOrder = i + 1, Latitude = 23.7520625, Longitude = 90.3819375 }); // Plus Code Q92J+RQ, Dhaka
        }

        if (!await db.EvaluationCriteria.AnyAsync())
        {
            (string Key, string Label)[] criteria =
            [
                ("punctuality", "Punctuality and discipline"),
                ("quality", "Quality of work"),
                ("skills", "Domain skills"),
                ("teamwork", "Initiative and teamwork"),
                ("conduct", "Conduct and confidentiality"),
            ];
            for (var i = 0; i < criteria.Length; i++)
                db.EvaluationCriteria.Add(new EvaluationCriterion { Key = criteria[i].Key, Label = criteria[i].Label, MaxScore = 20, SortOrder = i + 1 });
        }

        if (!await db.GradeBands.AnyAsync())
        {
            db.GradeBands.AddRange(
                new GradeBand { Grade = "Outstanding", NameBn = "অসাধারণ", MinScore = 90 },
                new GradeBand { Grade = "Excellent", NameBn = "অতি উত্তম", MinScore = 80 },
                new GradeBand { Grade = "Good", NameBn = "উত্তম", MinScore = 70 },
                new GradeBand { Grade = "Satisfactory", NameBn = "সন্তোষজনক", MinScore = 60 });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The document library's starting list. A type is added only when its code is missing; existing rows are left as edited.</summary>
    private async Task SeedDocumentTypesAsync()
    {
        const string Any = ".pdf,.jpg,.jpeg,.png";
        const string Images = ".jpg,.jpeg,.png";
        const string Pdf = ".pdf";
        const long Mb = 1024 * 1024;
        (DocumentCategory Category, string Code, string Name, string? Description, bool Identity, string Extensions, long Max)[] types =
        [
            (DocumentCategory.Identity, "PHOTO", "Passport-size photo", "Recent, plain light background.", false, Images, 2 * Mb),
            (DocumentCategory.Identity, "NID", "National ID card", "Front and back on one page.", true, Any, 5 * Mb),
            (DocumentCategory.Identity, "BIRTH", "Birth registration certificate", "If you do not have a National ID yet.", true, Any, 5 * Mb),
            (DocumentCategory.Identity, "PASSPORT", "Passport", "The page with your photo.", true, Any, 5 * Mb),
            (DocumentCategory.Academic, "SSC", "SSC or equivalent certificate", null, false, Any, 5 * Mb),
            (DocumentCategory.Academic, "HSC", "HSC or equivalent certificate", null, false, Any, 5 * Mb),
            (DocumentCategory.Academic, "TRANSCRIPT", "Transcript or mark sheet", "The latest one: every semester so far, or the final transcript.", false, Any, 5 * Mb),
            (DocumentCategory.Academic, "STUDENTSHIP", "Studentship certificate", "From your university, issued in the last 3 months.", false, Any, 5 * Mb),
            (DocumentCategory.Academic, "DEGREE", "Degree certificate", "The provisional certificate is accepted.", false, Any, 5 * Mb),
            (DocumentCategory.Academic, "EQUIVALENCE", "UGC equivalence certificate", "For a degree from outside Bangladesh.", false, Any, 5 * Mb),
            (DocumentCategory.Career, "CV", "CV / résumé", null, false, Pdf, 5 * Mb),
            (DocumentCategory.Career, "RECOMMENDATION", "Recommendation letter", "From a teacher or the head of your department.", false, Pdf, 5 * Mb),
            (DocumentCategory.Career, "EXPERIENCE", "Experience certificate", null, false, Any, 5 * Mb),
            (DocumentCategory.Career, "TRAINING", "Training certificate", null, false, Any, 5 * Mb),
            (DocumentCategory.Academic, "STUDENT_ID", "University student ID card", "Front and back on one page.", false, Any, 5 * Mb),
            (DocumentCategory.Career, "STAFF_ID", "University staff ID or appointment letter", "Shows that you teach at the university.", false, Any, 5 * Mb),
            (DocumentCategory.Career, "TOT", "Training of Trainers (ToT) certificate", "From the UGC Soft Skill ToT.", false, Any, 5 * Mb),
            (DocumentCategory.Other, "OTHER", "Other document", null, false, Any, 5 * Mb),
        ];

        var existing = (await db.DocumentTypes.Select(t => t.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        for (var i = 0; i < types.Length; i++)
        {
            var t = types[i];
            if (existing.Contains(t.Code)) continue;
            db.DocumentTypes.Add(new DocumentType
            {
                Code = t.Code, Name = t.Name, Description = t.Description, Category = t.Category, IsIdentity = t.Identity,
                Extensions = t.Extensions, MaxBytes = t.Max, SortOrder = i + 1,
            });
            added++;
        }
        if (added > 0)
        {
            await db.SaveChangesAsync();
            log.LogInformation("Added {Count} document types", added);
        }
        await SeedInternshipRequirementsAsync();
        await SeedSoftSkillRequirementsAsync();
    }

    /// <summary>The UGC Internship's document checklist, set once; later edits in the database are kept.</summary>
    private async Task SeedInternshipRequirementsAsync()
    {
        const string Module = Modules.Internship;
        if (await db.ModuleDocumentRequirements.AnyAsync(r => r.Module == Module)) return;
        var ids = await db.DocumentTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        (string Group, string Code, bool Required, RequirementCondition When, string? Note)[] lines =
        [
            ("photo", "PHOTO", true, RequirementCondition.Always, null),
            ("identity", "NID", true, RequirementCondition.Always, "Any one of these"),
            ("identity", "BIRTH", true, RequirementCondition.Always, null),
            ("identity", "PASSPORT", true, RequirementCondition.Always, null),
            ("cv", "CV", true, RequirementCondition.Always, null),
            ("transcript", "TRANSCRIPT", true, RequirementCondition.Always, null),
            ("studentship", "STUDENTSHIP", true, RequirementCondition.Studying, "Issued in the last 3 months"),
            ("degree", "DEGREE", true, RequirementCondition.Graduated, "The provisional certificate is accepted"),
            ("recommendation", "RECOMMENDATION", false, RequirementCondition.Always, "From a teacher or the head of your department"),
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (!ids.TryGetValue(l.Code, out var typeId)) continue;
            db.ModuleDocumentRequirements.Add(new ModuleDocumentRequirement
            {
                Module = Module, GroupKey = l.Group, DocumentTypeId = typeId, IsRequired = l.Required, Condition = l.When, Note = l.Note, SortOrder = i + 1,
            });
        }
        await db.SaveChangesAsync();
        log.LogInformation("Set the {Module} document checklist", Module);
    }

    // ---------- users ----------

    private string Password => config["Seed:DemoPassword"] ?? "demo123";

    private async Task<User> EnsureUserAsync(string email, string name, string role, string? designation = null, string? department = null)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;

        long? deptId = department is null ? null : (await db.Departments.FirstAsync(d => d.Name == department)).Id;
        user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            Designation = designation,
            DepartmentId = deptId,
            CreatedAt = clock.UtcNow,
        };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded) throw new InvalidOperationException($"Seeding {email} failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        await users.AddToRoleAsync(user, role);
        return user;
    }

    private async Task<User> EnsureAdminAsync()
    {
        var email = config["Seed:AdminEmail"] ?? "admin@nexus.demo";
        return await EnsureUserAsync(email, "Farhana Akter", Roles.Admin, "Director, UGC Internship Programme");
    }

    private async Task<User> StudentAsync(string email, string name, string university, string programme, int year, string graduation, bool verified = true)
    {
        var user = await EnsureUserAsync(email, name, Roles.Student);
        if (!await db.StudentProfiles.AnyAsync(p => p.UserId == user.Id))
        {
            db.StudentProfiles.Add(new StudentProfile { UserId = user.Id, IdentityVerified = verified });
            db.Educations.Add(new Education
            {
                UserId = user.Id,
                Level = EducationLevel.Bachelor,
                Institution = university,
                Programme = programme,
                Status = EducationStatus.Studying,
                CurrentYear = year,
                ResultMonth = DateOnly.ParseExact(graduation + "-01", "yyyy-MM-dd"),
                IsMain = true,
            });
            await db.SaveChangesAsync();
        }
        return user;
    }

    // ---------- demo data (mirrors src/data/ugcInternship.js) ----------

    private async Task SeedDemoAsync()
    {
        log.LogInformation("Seeding UGC Internship demo data");
        var admin = await EnsureAdminAsync();

        var mResearch = await EnsureUserAsync("mentor@nexus.demo", "Dr. Mahfuza Khatun", Roles.Mentor, "Deputy Director, Research", "Research");
        var mIct = await EnsureUserAsync("shahidul.alam@nexus.demo", "Shahidul Alam", Roles.Mentor, "Senior System Analyst", "ICT");
        var mProc = await EnsureUserAsync("anisur.rahman@nexus.demo", "Md. Anisur Rahman", Roles.Mentor, "Deputy Director, Procurement", "Procurement");
        var mAdmin = await EnsureUserAsync("rokeya.sultana@nexus.demo", "Rokeya Sultana", Roles.Mentor, "Assistant Director, Administration", "Administration");
        var mAcc = await EnsureUserAsync("habibur.rahman@nexus.demo", "Md. Habibur Rahman", Roles.Mentor, "Accounts Officer", "Accounts & Budget");
        var mSci = await EnsureUserAsync("selina.parvin@nexus.demo", "Dr. Selina Parvin", Roles.Mentor, "Senior Scientific Officer", "Science & Technology");

        await StudentAsync("student@nexus.demo", "Ayesha Rahman", "BUET", "BSc in Computer Science and Engineering", 4, "2026-06");
        var sabbir = await StudentAsync("sabbir.ahmed@nexus.demo", "Sabbir Ahmed", "CU", "BSc in Computer Science and Engineering", 4, "2026-06");
        var nadia = await StudentAsync("nadia.islam@nexus.demo", "Nadia Islam", "RU", "Bachelor of Pharmacy", 4, "2026-12");
        var arif = await StudentAsync("arif.khan@nexus.demo", "Arif Khan", "PSTU", "BSc in Agriculture", 4, "2026-12");
        var priya = await StudentAsync("priya.saha@nexus.demo", "Priya Saha", "IUB", "Bachelor of Business Administration", 4, "2026-06");
        var rakibul = await StudentAsync("rakibul.islam@nexus.demo", "Rakibul Islam", "AUST", "BSc in Electrical and Electronic Engineering", 4, "2025-12");
        var omar = await StudentAsync("omar.faruk@nexus.demo", "Omar Faruk", "BUET", "BSc in Civil Engineering", 4, "2025-12");
        var sharmin = await StudentAsync("sharmin.sultana@nexus.demo", "Sharmin Sultana", "RU", "BSc in Computer Science and Engineering", 4, "2026-06");
        var hasib = await StudentAsync("hasib.mahmud@nexus.demo", "Hasib Mahmud", "IUB", "BSc in Computer Science and Engineering", 4, "2026-06");
        var shuvo = await StudentAsync("shuvo.das@nexus.demo", "Shuvo Das", "PSTU", "BSc in Public Health", 4, "2026-06");
        var zubair = await StudentAsync("zubair.hossain@nexus.demo", "Zubair Hossain", "CUET", "BSc in Electrical and Electronic Engineering", 4, "2025-12");

        var dept = await db.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);
        var crit = await db.EvaluationCriteria.ToDictionaryAsync(c => c.Key, c => c.Id);

        var b1 = Batch("UGC-INT-2026-B1", "2026 Batch 1", 2026, BatchStatus.Completed, 3, "2025-11-01", "2025-12-15", "2026-01-11", "First cohort under the UGC Internship Policy.", "2025-10-20T10:00:00+06:00");
        var b2 = Batch("UGC-INT-2026-B2", "2026 Batch 2", 2026, BatchStatus.Running, 5, "2026-05-01", "2026-06-10", "2026-07-12", "Placed across Administration, Research, ICT and Accounts.", "2026-04-20T10:00:00+06:00");
        var b3 = Batch("UGC-INT-2027-B1", "2027 Batch 1", 2027, BatchStatus.ApplicationsOpen, 4, "2026-11-01", "2026-12-15", "2027-01-10", "Open to final-year, postgraduate and recent graduates.", "2026-10-20T10:00:00+06:00");
        db.InternshipBatches.AddRange(b1, b2, b3);

        InternshipApplication App(InternshipBatch batch, User student, ApplicationStatus status, string appliedAt, string[] prefs, string? statement = null)
        {
            var a = new InternshipApplication
            {
                Batch = batch,
                StudentId = student.Id,
                Status = status,
                AppliedAt = Utc(appliedAt),
                Statement = statement ?? "I want to learn how the national higher-education regulator works and contribute to its digital programmes.",
                Basis = "cl.7 (ক)",
                CreatedAt = Utc(appliedAt),
                CreatedById = student.Id,
            };
            for (var i = 0; i < prefs.Length; i++) a.PreferredDepartments.Add(new ApplicationPreferredDepartment { DepartmentId = dept[prefs[i]], Rank = i + 1 });
            db.InternshipApplications.Add(a);
            return a;
        }

        void History(InternshipApplication a, User student, params (ApplicationStatus Status, string At, string? Note)[] steps)
        {
            foreach (var (s, at, note) in steps)
            {
                var byStudent = s == ApplicationStatus.Submitted;
                a.History.Add(new ApplicationStatusHistory
                {
                    Status = s,
                    At = Utc(at),
                    ById = byStudent ? student.Id : admin.Id,
                    ByName = byStudent ? student.FullName : admin.FullName,
                    Note = note ?? (byStudent ? "Application submitted" : null),
                });
            }
        }

        void Exam(InternshipApplication a, ExamMode mode, string date, int score)
        {
            a.ExamMode = mode;
            a.ExamDate = DateOnly.Parse(date);
            a.ExamScore = score;
        }

        Internship Place(InternshipApplication a, string department, User mentor, string start, int months, int[] absent, int[] leave, int[] approvedAbsent)
        {
            var s = DateOnly.Parse(start);
            var it = new Internship
            {
                DepartmentId = dept[department],
                MentorId = mentor.Id,
                StartDate = s,
                EndDate = InternshipRules.EndDateFor(s, months),
                Tasks = "Assist the department with data entry, document preparation, analysis and weekly reporting as assigned by the supervisor.",
            };
            it.Placements.Add(new InternshipPlacement { DepartmentId = dept[department], MentorId = mentor.Id, From = s, Note = "Initial placement" });
            foreach (var rec in Attendance(it.StartDate, it.EndDate, absent, leave, approvedAbsent)) it.Attendance.Add(rec);
            a.Internship = it;
            return it;
        }

        void PaidBills(Internship it, User mentor)
        {
            foreach (var key in InternshipRules.MonthsInRange(it.StartDate, it.EndDate))
            {
                var first = DateOnly.ParseExact(key + "-01", "yyyy-MM-dd");
                var dim = DateTime.DaysInMonth(first.Year, first.Month);
                it.Bills.Add(new AllowanceBill
                {
                    Month = key,
                    Report = $"Work done in {key}: tasks assigned by the supervisor, weekly progress notes and the month's deliverables.",
                    SubmittedAt = Utc($"{key}-{Math.Min(28, dim):D2}T10:00:00+06:00"),
                    Score = 80,
                    AttendanceScore = 20,
                    ConductScore = 21,
                    DisciplineScore = 20,
                    WorkSkillScore = 19,
                    EvaluationNote = "Satisfactory progress this month.",
                    EvaluatedAt = Utc($"{key}-{Math.Min(29, dim):D2}T10:00:00+06:00"),
                    EvaluatedById = mentor.Id,
                    EvaluatedByName = mentor.FullName,
                    PaidAt = Utc($"{key}-{Math.Min(30, dim):D2}T12:00:00+06:00"),
                    PaidById = admin.Id,
                    PaidByName = admin.FullName,
                });
            }
        }

        void Report(Internship it, string text, string at, string note) =>
            it.Report = new InternshipReport { Text = text, SubmittedAt = Utc(at), Status = ReportStatus.Accepted, ReviewNote = note, ReviewedById = it.MentorId, ReviewedAt = Utc(at).AddDays(2) };

        void Evaluate(Internship it, int punctuality, int quality, int skills, int teamwork, int conduct, string at, string recommendation)
        {
            var e = new InternshipEvaluation
            {
                Total = punctuality + quality + skills + teamwork + conduct,
                Recommendation = recommendation,
                EvaluatorId = it.MentorId,
                EvaluatedAt = Utc(at),
            };
            foreach (var (k, v) in new[] { ("punctuality", punctuality), ("quality", quality), ("skills", skills), ("teamwork", teamwork), ("conduct", conduct) })
                e.Scores.Add(new EvaluationScore { CriterionId = crit[k], Score = v });
            it.Evaluation = e;
        }

        // ---- Batch 1: completed ----
        var a1 = App(b1, omar, ApplicationStatus.Certified, "2025-11-20T10:00:00+06:00", ["Research", "ICT"]);
        Exam(a1, ExamMode.Written, "2025-12-22", 84);
        History(a1, omar, (ApplicationStatus.Submitted, "2025-11-20T10:00:00+06:00", null), (ApplicationStatus.Shortlisted, "2025-12-18T10:00:00+06:00", null),
            (ApplicationStatus.Selected, "2025-12-28T10:00:00+06:00", null), (ApplicationStatus.Active, "2026-01-11T09:00:00+06:00", "Placed in Procurement"),
            (ApplicationStatus.Completed, "2026-04-10T17:00:00+06:00", null), (ApplicationStatus.Certified, "2026-04-28T11:00:00+06:00", "Grade: Excellent"));
        var i1 = Place(a1, "Procurement", mProc, "2026-01-11", 3, [12], [30, 31], []);
        PaidBills(i1, mProc);
        Report(i1, "Prepared tender comparison sheets, assisted evaluation committees and drafted the procurement plan summary for FY 2025-26.", "2026-04-20T10:00:00+06:00", "Clear and complete.");
        Evaluate(i1, 17, 17, 16, 17, 18, "2026-04-24T10:00:00+06:00", "Recommended for certificate; strong analytical work.");
        i1.CertificateNumber = "UGC-INT-2026-B1-001";
        i1.CertificateGrade = "Excellent";
        i1.CertificateIssuedAt = Utc("2026-04-28T11:00:00+06:00");

        var a2 = App(b1, rakibul, ApplicationStatus.Certified, "2025-11-22T10:00:00+06:00", ["ICT", "Research"]);
        Exam(a2, ExamMode.Oral, "2025-12-23", 74);
        History(a2, rakibul, (ApplicationStatus.Submitted, "2025-11-22T10:00:00+06:00", null), (ApplicationStatus.Shortlisted, "2025-12-18T10:00:00+06:00", null),
            (ApplicationStatus.Selected, "2025-12-28T10:00:00+06:00", null), (ApplicationStatus.Active, "2026-01-11T09:00:00+06:00", "Placed in ICT"),
            (ApplicationStatus.Completed, "2026-04-10T17:00:00+06:00", null), (ApplicationStatus.Certified, "2026-04-28T11:00:00+06:00", "Grade: Good"));
        var i2 = Place(a2, "ICT", mIct, "2026-01-11", 3, [5, 22], [40], []);
        PaidBills(i2, mIct);
        Report(i2, "Supported office network documentation and a small inventory tracker for ICT equipment.", "2026-04-21T10:00:00+06:00", "Accepted.");
        Evaluate(i2, 14, 15, 14, 15, 16, "2026-04-24T10:00:00+06:00", "Good performance.");
        i2.CertificateNumber = "UGC-INT-2026-B1-002";
        i2.CertificateGrade = "Good";
        i2.CertificateIssuedAt = Utc("2026-04-28T11:00:00+06:00");

        var a3 = App(b1, zubair, ApplicationStatus.NotCertified, "2025-11-25T10:00:00+06:00", ["Administration"]);
        Exam(a3, ExamMode.Written, "2025-12-22", 69);
        History(a3, zubair, (ApplicationStatus.Submitted, "2025-11-25T10:00:00+06:00", null), (ApplicationStatus.Shortlisted, "2025-12-18T10:00:00+06:00", null),
            (ApplicationStatus.Selected, "2025-12-28T10:00:00+06:00", null), (ApplicationStatus.Active, "2026-01-11T09:00:00+06:00", "Placed in Administration"),
            (ApplicationStatus.Completed, "2026-04-10T17:00:00+06:00", null), (ApplicationStatus.NotCertified, "2026-04-28T11:00:00+06:00", "Attendance below 80%"));
        var i3 = Place(a3, "Administration", mAdmin, "2026-01-11", 3, [3, 4, 9, 10, 15, 16, 21, 22, 27, 28, 33, 34, 39, 40, 45, 46], [], []);
        PaidBills(i3, mAdmin);
        Report(i3, "Short report on file digitisation.", "2026-04-22T10:00:00+06:00", "Accepted.");
        Evaluate(i3, 9, 13, 13, 13, 14, "2026-04-24T10:00:00+06:00", "Work acceptable, attendance poor.");
        i3.OutcomeResult = "Not certified";
        i3.OutcomeReasons = "Attendance is below 80% (cl.11 ঞ).";

        // ---- Batch 2: running ----
        (User Student, string Applied, ExamMode Mode, string ExamDate, int Score, string Dept, User Mentor, int[] Absent, int[] Leave, int[] ApprovedAbsent)[] running =
        [
            (sabbir, "2026-05-12T10:00:00+06:00", ExamMode.Online, "2026-06-20", 88, "Research", mResearch, [14], [40], []),
            (sharmin, "2026-05-14T10:00:00+06:00", ExamMode.Online, "2026-06-20", 81, "ICT", mIct, [8, 9, 25, 26, 27, 44, 45, 52], [30], []),
            (hasib, "2026-05-15T10:00:00+06:00", ExamMode.Written, "2026-06-19", 79, "Accounts & Budget", mAcc, [20], [], [33]),
            (shuvo, "2026-05-18T10:00:00+06:00", ExamMode.Written, "2026-06-19", 77, "Science & Technology", mSci, [], [], []),
        ];
        foreach (var r in running)
        {
            var a = App(b2, r.Student, ApplicationStatus.Active, r.Applied, r.Dept == "Research" ? ["Research", "ICT"] : [r.Dept, "Research"]);
            Exam(a, r.Mode, r.ExamDate, r.Score);
            History(a, r.Student, (ApplicationStatus.Submitted, r.Applied, null), (ApplicationStatus.Shortlisted, "2026-06-15T10:00:00+06:00", null),
                (ApplicationStatus.Selected, "2026-06-28T10:00:00+06:00", null), (ApplicationStatus.Active, "2026-07-12T09:00:00+06:00", $"Placed in {r.Dept}"));
            Place(a, r.Dept, r.Mentor, "2026-07-12", 5, r.Absent, r.Leave, r.ApprovedAbsent);
        }

        var a8 = App(b2, priya, ApplicationStatus.Selected, "2026-05-20T10:00:00+06:00", ["Accounts & Budget", "Administration"]);
        Exam(a8, ExamMode.Written, "2026-06-19", 73);
        History(a8, priya, (ApplicationStatus.Submitted, "2026-05-20T10:00:00+06:00", null), (ApplicationStatus.Shortlisted, "2026-06-15T10:00:00+06:00", null),
            (ApplicationStatus.Selected, "2026-06-28T10:00:00+06:00", null));

        // ---- Batch 3: applications open ----
        var a9 = App(b3, nadia, ApplicationStatus.Submitted, "2026-11-04T10:00:00+06:00", ["Research", "Science & Technology"]);
        History(a9, nadia, (ApplicationStatus.Submitted, "2026-11-04T10:00:00+06:00", null));
        var a10 = App(b3, arif, ApplicationStatus.Shortlisted, "2026-11-03T10:00:00+06:00", ["Research"]);
        History(a10, arif, (ApplicationStatus.Submitted, "2026-11-03T10:00:00+06:00", null), (ApplicationStatus.Shortlisted, "2026-11-08T10:00:00+06:00", "Invited to the written test"));

        await db.SaveChangesAsync();

        // Slip numbers need the internship id, known only after the first save.
        foreach (var it in new[] { i1, i2, i3 })
            foreach (var bill in it.Bills)
                bill.SlipNumber = AllowanceBill.SlipNumberFor(it.Id, bill.Month);
        await db.SaveChangesAsync();
        log.LogInformation("Demo data seeded");
    }

    private InternshipBatch Batch(string code, string name, int year, BatchStatus status, int months, string from, string to, string start, string note, string createdAt) =>
        new()
        {
            Code = code,
            Name = name,
            Year = year,
            Status = status,
            Capacity = 20,
            DurationMonths = months,
            ApplyFrom = DateOnly.Parse(from),
            ApplyTo = DateOnly.Parse(to),
            StartDate = DateOnly.Parse(start),
            Note = note,
            CreatedAt = Utc(createdAt),
        };

    // Demo data uses the default week and declares no holidays.
    private static readonly WorkCalendar NoHolidays = new(WorkCalendar.DefaultWeekend, new HashSet<DateOnly>());

    /// <summary>
    /// Attendance up to yesterday: index i is the i-th working day. Indexes in <paramref name="absent"/> get no record
    /// (an unapproved absence), <paramref name="approvedAbsent"/> an approved absence, <paramref name="leave"/> approved leave.
    /// </summary>
    private IEnumerable<AttendanceRecord> Attendance(DateOnly start, DateOnly end, int[] absent, int[] leave, int[] approvedAbsent)
    {
        var yesterday = clock.Today.AddDays(-1);
        var upto = end < yesterday ? end : yesterday;
        var days = upto < start ? [] : InternshipRules.WorkingDaysBetween(start, upto, NoHolidays);
        for (var i = 0; i < days.Count; i++)
        {
            if (absent.Contains(i)) continue;
            if (approvedAbsent.Contains(i))
                yield return new AttendanceRecord { Date = days[i], Status = AttendanceStatus.Absent, Approved = true, Reason = "Medical certificate" };
            else if (leave.Contains(i))
                yield return new AttendanceRecord { Date = days[i], Status = AttendanceStatus.Leave, Approved = true, Reason = "Family event" };
            else
                yield return new AttendanceRecord
                {
                    Date = days[i],
                    Status = AttendanceStatus.Present,
                    CheckIn = new TimeOnly(9, i % 6),
                    CheckOut = new TimeOnly(17, 10 + i % 40),
                };
        }
    }

    private static DateTime Utc(string iso) => DateTimeOffset.Parse(iso).UtcDateTime;
}
