namespace Nexus.Application.Rules;

/// <summary>Programmes a person can apply to. Each asks for documents from the shared library.</summary>
public static class Modules
{
    public const string Internship = "internship";

    /// <summary>UGC Soft Skill Training, for students.</summary>
    public const string SoftSkill = "softskill";

    /// <summary>UGC Soft Skill Training, the trainer registration of university teachers.</summary>
    public const string SoftSkillTrainer = "softskill-trainer";

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Internship] = "UGC Internship",
        [SoftSkill] = "UGC Soft Skill Training",
        [SoftSkillTrainer] = "Soft Skill Trainer",
    };

    public static string NameOf(string module) => Names.TryGetValue(module, out var name) ? name : module;
}
