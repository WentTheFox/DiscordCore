using System;

namespace Discord
{
    // === Enums ===

    public enum ActivityType
    {
        Playing = 0,
        Streaming = 1,
        Listening = 2,
        Watching = 3,
    }

    public enum ActivityActionType
    {
        Join = 1,
        Spectate = 2, // kept for downstream API compat; no longer supported by Social SDK
    }

    public enum ActivityPartyPrivacy
    {
        Private = 0,
        Public = 1,
    }

    // Kept so downstream code that catches ResultException or compares Result.Ok still compiles.
    // No longer thrown by DiscordCore — the Social SDK uses status callbacks instead.
    public enum Result
    {
        Ok = 0,
        NotRunning = 27,
    }

    public class ResultException : Exception
    {
        public Result Result { get; }
        public ResultException(Result result) : base($"Discord error: {result}") { Result = result; }
    }

    // === Activity sub-structs ===

    public struct ActivityTimestamps
    {
        public long Start;
        public long End;
    }

    public struct ActivityAssets
    {
        public string LargeImage;
        public string LargeText;
        public string SmallImage;
        public string SmallText;
    }

    public struct ActivityPartySize
    {
        public int CurrentSize;
        public int MaxSize;
    }

    public struct ActivityParty
    {
        public string Id;
        public ActivityPartySize Size;
        public ActivityPartyPrivacy Privacy;
    }

    public struct ActivitySecrets
    {
        public string Match;    // deprecated — not supported by Discord Social SDK
        public string Join;
        public string Spectate; // deprecated — not supported by Discord Social SDK
    }

    // === Activity ===

    public struct Activity
    {
        public ActivityType Type;
        public long ApplicationId;
        public string Name;
        public string State;
        public string Details;
        public ActivityTimestamps Timestamps;
        public ActivityAssets Assets;
        public ActivityParty Party;
        public ActivitySecrets Secrets;
        public bool Instance;
    }

    // === User ===
    // Note: The Social SDK only provides SenderId (user ID) in activity invite callbacks.
    // Username, Discriminator, Avatar, and Bot will always be default values in callbacks.

    public struct User
    {
        public long Id;
        public string Username;
        public string Discriminator;
        public string Avatar;
        public bool Bot;
    }

    // === Delegate types ===
    // Previously nested under Discord.ActivityManager; now defined directly in the Discord namespace.

    public delegate void ActivityJoinHandler(string secret);
    public delegate void ActivitySpectateHandler(string secret); // no-op — Social SDK has no spectate
    public delegate void ActivityJoinRequestHandler(ref User user);
    public delegate void ActivityInviteHandler(ActivityActionType type, ref User user, ref Activity activity);
}
