using OMM.Public.Data.Entities;

namespace OMM.Public.Models;

public class MinerProfile
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string JoinedDate { get; set; } = string.Empty;
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NotStarted;
    public OnboardingStep OnboardingStep { get; set; } = OnboardingStep.Welcome;
}
