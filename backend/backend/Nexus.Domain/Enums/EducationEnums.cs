namespace Nexus.Domain.Enums;

/// <summary>Level of one education record, lowest first.</summary>
public enum EducationLevel
{
    Ssc = 1,
    Hsc = 2,
    Diploma = 3,
    Bachelor = 4,
    Master = 5,
    Doctorate = 6,
}

public enum EducationStatus
{
    Studying = 1,

    /// <summary>The result has been published.</summary>
    Completed = 2,
}
