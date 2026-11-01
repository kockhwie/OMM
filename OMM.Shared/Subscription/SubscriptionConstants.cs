namespace OMM.Shared.Subscription;

public static class SubscriptionConstants
{
    public static class PromptModes
    {
        public const string Standard = "STANDARD";
        public const string Pro = "PRO";
    }

    public static class PlanCodes
    {
        public const string Standard = "STANDARD";
        public const string Pro = "PRO";
    }

    public static class Statuses
    {
        public const string Active = "Active";
        public const string Scheduled = "Scheduled";
        public const string Suspended = "Suspended";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
        public const string Terminated = "Terminated";
    }

    public static class Slots
    {
        public const string Current = "Current";
        public const string Scheduled = "Scheduled";
    }

    public static class DisplayLabels
    {
        public const string Standard = "Educational information only";
        public const string Pro = "Personalized Pro Advice";
        public const string ProUnavailableFallback = "Standard mode — Pro analysis temporarily unavailable";
    }
}

