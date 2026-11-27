using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Application.Rules;
using Nexus.Application.Security;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Setup;
using Nexus.Domain.Entities.SoftSkills;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.Seeders;

/// <summary>Reference data shared by modules (universities) and the UGC Soft Skill Training's own.</summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// A starting list of public universities, added by name when missing (edits in the database are kept).
    /// Then links education lines that name one of them, so the cl.7 check reads a fact instead of text.
    /// </summary>
    private async Task SeedUniversitiesAsync()
    {
        (string Name, string Short, string? Bn)[] list =
        [
            ("University of Dhaka", "DU", "ঢাকা বিশ্ববিদ্যালয়"),
            ("University of Rajshahi", "RU", "রাজশাহী বিশ্ববিদ্যালয়"),
            ("University of Chittagong", "CU", "চট্টগ্রাম বিশ্ববিদ্যালয়"),
            ("Jahangirnagar University", "JU", "জাহাঙ্গীরনগর বিশ্ববিদ্যালয়"),
            ("Bangladesh University of Engineering and Technology", "BUET", "বাংলাদেশ প্রকৌশল বিশ্ববিদ্যালয়"),
            ("Bangladesh Agricultural University", "BAU", "বাংলাদেশ কৃষি বিশ্ববিদ্যালয়"),
            ("Islamic University, Bangladesh", "IU", "ইসলামী বিশ্ববিদ্যালয়"),
            ("Shahjalal University of Science and Technology", "SUST", "শাহজালাল বিজ্ঞান ও প্রযুক্তি বিশ্ববিদ্যালয়"),
            ("Khulna University", "KU", "খুলনা বিশ্ববিদ্যালয়"),
            ("Jagannath University", "JnU", "জগন্নাথ বিশ্ববিদ্যালয়"),
            ("Comilla University", "CoU", "কুমিল্লা বিশ্ববিদ্যালয়"),
            ("Jatiya Kabi Kazi Nazrul Islam University", "JKKNIU", "জাতীয় কবি কাজী নজরুল ইসলাম বিশ্ববিদ্যালয়"),
            ("Begum Rokeya University, Rangpur", "BRUR", "বেগম রোকেয়া বিশ্ববিদ্যালয়, রংপুর"),
            ("University of Barishal", "BU", "বরিশাল বিশ্ববিদ্যালয়"),
            ("Sher-e-Bangla Agricultural University", "SAU", "শেরেবাংলা কৃষি বিশ্ববিদ্যালয়"),
            ("Mawlana Bhashani Science and Technology University", "MBSTU", null),
            ("Patuakhali Science and Technology University", "PSTU", null),
            ("Noakhali Science and Technology University", "NSTU", null),
            ("Hajee Mohammad Danesh Science and Technology University", "HSTU", null),
            ("Jashore University of Science and Technology", "JUST", null),
            ("Pabna University of Science and Technology", "PUST", null),
            ("Rajshahi University of Engineering & Technology", "RUET", null),
            ("Khulna University of Engineering & Technology", "KUET", null),
            ("Chittagong University of Engineering & Technology", "CUET", null),
            ("Dhaka University of Engineering & Technology, Gazipur", "DUET", null),
            ("Chittagong Veterinary and Animal Sciences University", "CVASU", null),
            ("Sylhet Agricultural University", "SyAU", null),
            ("Bangladesh University of Textiles", "BUTEX", null),
            ("Bangladesh University of Professionals", "BUP", null),
            ("Rangamati Science and Technology University", "RMSTU", null),
            ("Khulna Agricultural University", "KAU", null),
            ("Rabindra University, Bangladesh", "RUB", null),
            ("Islamic Arabic University", "IAU", null),
            ("Bangladesh Open University", "BOU", null),
            ("National University", "NU", null),
        ];
        var existing = (await db.Universities.Select(u => u.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (existing.Contains(list[i].Name)) continue;
            db.Universities.Add(new University { Name = list[i].Name, ShortName = list[i].Short, NameBn = list[i].Bn, IsPublic = true, SortOrder = i + 1 });
            added++;
        }
        if (added > 0)
        {
            await db.SaveChangesAsync();
            log.LogInformation("Added {Count} universities", added);
        }

        // Link education lines typed before the list existed, matched on the full or short name.
        var universities = await db.Universities.AsNoTracking().ToListAsync();
        var unlinked = await db.Educations.Where(e => e.UniversityId == null && e.Level >= EducationLevel.Bachelor).ToListAsync();
        var linked = 0;
        foreach (var e in unlinked)
        {
            var name = e.Institution.Trim();
            var match = universities.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? universities.FirstOrDefault(u => u.ShortName is not null && string.Equals(u.ShortName, name, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            e.UniversityId = match.Id;
            e.Institution = match.Name;
            linked++;
        }
        if (linked > 0)
        {
            await db.SaveChangesAsync();
            log.LogInformation("Linked {Count} education lines to universities", linked);
        }
    }

    /// <summary>The Soft Skill policy row, the six cl.13 topics and the cl.15 grade bands, each set once.</summary>
    private async Task SeedSoftSkillReferenceAsync()
    {
        if (!await db.SoftSkillPolicies.AnyAsync()) db.SoftSkillPolicies.Add(new SoftSkillPolicy());

        if (!await db.SoftSkillTopics.AnyAsync())
        {
            (string Code, string Name, string Bn, int No)[] topics =
            [
                ("communication", "Communication Skills", "কমিউনিকেশন স্কিল", 1),
                ("problem-solving", "Problem Solving Techniques", "প্রবলেম সলভিং টেকনিক", 1),
                ("innovation", "Innovation and Creative Thinking", "ইনোভেশন ও ক্রিয়েটিভ থিংকিং", 2),
                ("entrepreneurship", "Entrepreneurship Mindset", "এন্টারপ্রেনিউরশিপ মাইন্ডসেট", 2),
                ("ai-tools", "Use of AI Tools", "এআই টুলসের ব্যবহার", 3),
                ("freelancing", "Freelancing and Self-employment", "ফ্রিল্যান্সিং পরিচিতি ও স্বকর্মসংস্থান", 3),
            ];
            for (var i = 0; i < topics.Length; i++)
                db.SoftSkillTopics.Add(new SoftSkillTopic
                {
                    Code = topics[i].Code, Name = topics[i].Name, NameBn = topics[i].Bn, Hours = 8, AssessmentNo = topics[i].No, SortOrder = i + 1,
                });
        }

        if (!await db.SoftSkillGradeBands.AnyAsync())
        {
            // cl.14 (খ) leaves the marks to the Commission; these are starting values to edit in the database.
            db.SoftSkillGradeBands.AddRange(
                new SoftSkillGradeBand { Grade = "Outstanding", NameBn = "অসাধারণ", MinAverage = 90 },
                new SoftSkillGradeBand { Grade = "Excellent", NameBn = "অতি উত্তম", MinAverage = 80 },
                new SoftSkillGradeBand { Grade = "Good", NameBn = "উত্তম", MinAverage = 65 },
                new SoftSkillGradeBand { Grade = "Satisfactory", NameBn = "সন্তোষজনক", MinAverage = 50 });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The Soft Skill checklists for students and for trainers, each set once; later edits are kept.</summary>
    private async Task SeedSoftSkillRequirementsAsync()
    {
        await SeedChecklistAsync(Modules.SoftSkill,
        [
            ("photo", "PHOTO", true, RequirementCondition.Always, null),
            ("identity", "NID", true, RequirementCondition.Always, "Any one of these"),
            ("identity", "BIRTH", true, RequirementCondition.Always, null),
            ("identity", "PASSPORT", true, RequirementCondition.Always, null),
            ("student-id", "STUDENT_ID", true, RequirementCondition.Always, "Shows you are a regular student (cl.7 ক)"),
            ("studentship", "STUDENTSHIP", false, RequirementCondition.Always, "Issued in the last 3 months"),
        ]);
        await SeedChecklistAsync(Modules.SoftSkillTrainer,
        [
            ("photo", "PHOTO", true, RequirementCondition.Always, null),
            ("identity", "NID", true, RequirementCondition.Always, "Any one of these"),
            ("identity", "PASSPORT", true, RequirementCondition.Always, null),
            ("staff-id", "STAFF_ID", true, RequirementCondition.Always, "cl.12 (ক): trainers are the university's own teachers"),
            ("cv", "CV", true, RequirementCondition.Always, null),
            ("tot", "TOT", false, RequirementCondition.Always, "Upload it once you finish the UGC ToT (cl.12 খ)"),
        ]);
    }

    private async Task SeedChecklistAsync(string module, (string Group, string Code, bool Required, RequirementCondition When, string? Note)[] lines)
    {
        if (await db.ModuleDocumentRequirements.AnyAsync(r => r.Module == module)) return;
        var ids = await db.DocumentTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (!ids.TryGetValue(l.Code, out var typeId)) continue;
            db.ModuleDocumentRequirements.Add(new ModuleDocumentRequirement
            {
                Module = module, GroupKey = l.Group, DocumentTypeId = typeId, IsRequired = l.Required, Condition = l.When, Note = l.Note, SortOrder = i + 1,
            });
        }
        await db.SaveChangesAsync();
        log.LogInformation("Set the {Module} document checklist", module);
    }

    /// <summary>
    /// With Seed:DemoData on and no Soft Skill batch yet: one batch open for applications at two universities,
    /// an approved trainer who coordinates the Dhaka university group, and a first-year student who can apply.
    /// </summary>
    private async Task SeedSoftSkillDemoAsync()
    {
        if (!config.GetValue("Seed:DemoData", true) || await db.SoftSkillBatches.AnyAsync()) return;
        log.LogInformation("Seeding UGC Soft Skill demo data");

        var du = await db.Universities.FirstAsync(u => u.ShortName == "DU");
        var ru = await db.Universities.FirstAsync(u => u.ShortName == "RU");
        var policy = await db.SoftSkillPolicies.FirstAsync();

        var trainer = await EnsureUserAsync("trainer@nexus.demo", "Dr. Nasrin Sultana", Roles.Trainer, "Associate Professor");
        if (!await db.TrainerProfiles.AnyAsync(p => p.UserId == trainer.Id))
            db.TrainerProfiles.Add(new TrainerProfile { UserId = trainer.Id, UniversityId = du.Id, AcademicDepartment = "Department of English" });
        if (!await db.SoftSkillTrainers.AnyAsync(t => t.UserId == trainer.Id))
            db.SoftSkillTrainers.Add(new SoftSkillTrainer
            {
                UserId = trainer.Id, UniversityId = du.Id, Status = TrainerStatus.Approved,
                Statement = "Ten years of teaching communication and presentation skills.",
                AppliedAt = clock.UtcNow, DecidedAt = clock.UtcNow, TotCompleted = true, TotCompletedOn = clock.Today.AddDays(-30), TotReference = "UGC-TOT-DEMO-01",
            });

        var student = await EnsureUserAsync("softskill.student@nexus.demo", "Tanvir Hasan", Roles.Student);
        if (!await db.StudentProfiles.AnyAsync(p => p.UserId == student.Id))
        {
            db.StudentProfiles.Add(new StudentProfile { UserId = student.Id, IdentityVerified = true });
            db.Educations.Add(new Education
            {
                UserId = student.Id, Level = EducationLevel.Bachelor, Institution = du.Name, UniversityId = du.Id,
                Programme = "BSS in Economics", Status = EducationStatus.Studying, CurrentYear = 2, IsMain = true,
            });
        }

        var today = clock.Today;
        var start = today.AddDays(30);
        var batch = new SoftSkillBatch
        {
            Code = $"UGC-SSK-{start.Year}-B1", Name = $"Soft Skill Training {start.Year} — Batch 1", Year = start.Year,
            Status = BatchStatus.ApplicationsOpen, ApplyFrom = today.AddDays(-5), ApplyTo = today.AddDays(20),
            StartDate = start, EndDate = InternshipRules.EndDateFor(start, policy.DurationMonths),
        };
        batch.UniversityGroups.Add(new SoftSkillUniversityGroup { University = du, Capacity = policy.SeatsPerUniversity, Coordinator = trainer, Venue = "TSC Seminar Room" });
        batch.UniversityGroups.Add(new SoftSkillUniversityGroup { University = ru, Capacity = policy.SeatsPerUniversity });
        db.SoftSkillBatches.Add(batch);
        await db.SaveChangesAsync();
    }
}
