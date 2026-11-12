namespace CareLoop.Domain.Stages;

/// <summary>Which component's event moves a case into a stage, per the MVP architecture diagram.</summary>
public enum StageDriver
{
    /// <summary>HIS simulator / mock HIS adapter.</summary>
    His,

    /// <summary>The orchestration API itself, without an external trigger.</summary>
    Api,

    /// <summary>Patient-channel simulator / mock channel adapter.</summary>
    PatientChannel,

    /// <summary>An authorized clinician acting through the API.</summary>
    Clinician,
}
