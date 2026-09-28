namespace OMM.Public.Data.Entities;

public enum OnboardingStatus
{
    NotStarted,
    InProgress,
    Skipped,
    Completed
}

public enum OnboardingStep
{
    Welcome,
    Currency,
    Country,
    DisplayName,
    FirstMineDecision,
    Complete
}
