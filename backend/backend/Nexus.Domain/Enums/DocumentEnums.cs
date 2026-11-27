namespace Nexus.Domain.Enums;

/// <summary>How the document library groups document types.</summary>
public enum DocumentCategory
{
    Identity = 1,
    Academic = 2,
    Career = 3,
    Other = 4,
}

/// <summary>Review state of an uploaded document. A document counts for an application once uploaded; verification comes later.</summary>
public enum DocumentStatus
{
    Submitted = 1,
    Verified = 2,
    Rejected = 3,
}

/// <summary>When a module's document requirement applies, judged from the person's academic profile.</summary>
public enum RequirementCondition
{
    Always = 0,

    /// <summary>Still studying (year of study set, or result month not reached).</summary>
    Studying = 1,

    /// <summary>Finished: the result has been published.</summary>
    Graduated = 2,
}
