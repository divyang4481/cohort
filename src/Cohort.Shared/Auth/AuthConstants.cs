namespace Cohort.Shared.Auth;

public static class AuthConstants
{
    public static class Policies
    {
        public const string AdminOnly = "AdminOnly";
        public const string HostOnly = "HostOnly";
        public const string ParticipantOnly = "ParticipantOnly";
        public const string ParticipantAnonymousOrOidc = "ParticipantAnonymousOrOidc";
    }

    public static class Claims
    {
        public const string AppRole = "app_role";
        public const string ParticipantMode = "participant_mode"; // "oidc" | "anonymous"
        public const string DisplayName = "display_name";
    }

    public static class AppRoles
    {
        public const string Admin = "admin";
        public const string Host = "host";
        public const string Participant = "participant";
    }
}
