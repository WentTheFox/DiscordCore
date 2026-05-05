using Discord;
using DiscordCore.Native;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordCore
{
    public static unsafe class DiscordClient
    {
        public const string DisabledReason = "Disabled";

        // Public events — same names and delegate signatures as the old Game SDK API.
        internal static event ActivityJoinHandler OnActivityJoin;
        internal static event ActivityJoinRequestHandler OnActivityJoinRequest;
        internal static event ActivityInviteHandler OnActivityInvite;
#pragma warning disable CS0067 // Event never used: spectate is not supported by the Social SDK
        internal static event ActivitySpectateHandler OnActivitySpectate;
#pragma warning restore CS0067

        public static long CurrentAppID { get; private set; } = -1;
        public static bool Enabled { get; private set; }

        // True once SetApplicationId has been called with a valid app ID (RPC mode — no OAuth2 needed).
        internal static bool IsReady => _connected;

        // True once SetApplicationId has been called with a valid app ID.
        internal static bool IsConnected => _connected;

        internal static Discord_Client _client;         // opaque struct — must be a field, not a local
        internal static bool _clientInitialized;
        private static bool _connected;               // true after first Connect(); false after Disable()
        private static bool _authenticated;           // set by DiscordAuth; used so IsAuthenticated is accurate immediately after revoke
        private static Discord_Client_Status _currentStatus = Discord_Client_Status.Disconnected;

        // Log level mapping (C# 7.3 — no switch expressions)
        private static readonly Dictionary<Discord_LoggingSeverity, Logger.Level> _logLevels =
            new Dictionary<Discord_LoggingSeverity, Logger.Level>
            {
                { Discord_LoggingSeverity.Verbose, Logger.Level.Debug },
                { Discord_LoggingSeverity.Info,    Logger.Level.Info },
                { Discord_LoggingSeverity.Warning, Logger.Level.Warning },
                { Discord_LoggingSeverity.Error,   Logger.Level.Error },
            };

        // Pinned delegate fields — MUST be fields to prevent GC collection while native code holds them.
        private static Discord_Client_OnStatusChanged _statusChangedDelegate;
        private static Discord_Client_LogCallback _logCallbackDelegate;
        private static Discord_Client_ActivityJoinCallback _activityJoinDelegate;
        private static Discord_Client_ActivityInviteCallback _activityInviteDelegate;
        internal static Discord_FreeFn _nullFreeFn = (_) => { };

        public static bool IsAuthenticated =>
            _clientInitialized && _authenticated && SDK.Discord_Client_IsAuthenticated(ref _client);

        internal static void SetAuthenticated(bool value) => _authenticated = value;

        public static void Authorize(Action<bool, string> callback) =>
            DiscordAuth.Authorize(callback);

        public static void RevokeAuth(Action callback) =>
            DiscordAuth.Revoke(callback);

        internal static void Disable(bool hasError = false)
        {
            CurrentAppID = -1;
            if (_clientInitialized)
            {
                if (SDK.Discord_Client_IsAuthenticated(ref _client))
                    SDK.Discord_Client_Disconnect(ref _client);
                SDK.Discord_Client_Drop(ref _client);
                _client = default;
                _clientInitialized = false;
            }
            _connected = false;
            _authenticated = false;
            _currentStatus = Discord_Client_Status.Disconnected;
            if (Enabled)
            {
                Enabled = false;
                if (!hasError)
                {
                    DiscordManager.deactivationReason = DisabledReason;
                    Plugin.log.Info("DiscordClient disabled.");
                }
                else
                {
                    Plugin.log.Info("DiscordClient disabled by error.");
                }
            }
        }

        internal static void Enable()
        {
            if (Enabled) return;
            if (_clientInitialized)
            {
                SDK.Discord_Client_Drop(ref _client);
                _client = default;
                _clientInitialized = false;
                _connected = false;
            }

            InitClient();

            Enabled = true;
            DiscordManager.active = true;
            DiscordManager.deactivationReason = string.Empty;
            Plugin.log.Info("DiscordClient enabled (waiting for mod instances to register app ID).");
            // Connection is deferred: DiscordManager.Update() calls ChangeAppID() on the first tick,
            // after all BSIPA mods have registered their instances.
        }

        private static void InitClient()
        {
            _client = default;
            SDK.Discord_Client_Init(ref _client);
            _clientInitialized = true;

            // Status changes drive DiscordManager.active instead of exception catching.
            _statusChangedDelegate = OnNativeStatusChanged;
            SDK.Discord_Client_SetStatusChangedCallback(ref _client, _statusChangedDelegate, _nullFreeFn, IntPtr.Zero);

            // SDK log messages forwarded to BSIPA logger.
            _logCallbackDelegate = OnNativeLog;
            SDK.Discord_Client_AddLogCallback(ref _client, _logCallbackDelegate, _nullFreeFn, IntPtr.Zero, Discord_LoggingSeverity.Verbose);

            // Activity join — user accepted a join invite in Discord.
            _activityJoinDelegate = OnNativeActivityJoin;
            SDK.Discord_Client_SetActivityJoinCallback(ref _client, _activityJoinDelegate, _nullFreeFn, IntPtr.Zero);

            // Activity invite — covers both incoming join invites (Type=Join) and join requests (Type=JoinRequest).
            _activityInviteDelegate = OnNativeActivityInvite;
            SDK.Discord_Client_SetActivityInviteCreatedCallback(ref _client, _activityInviteDelegate, _nullFreeFn, IntPtr.Zero);

            // Token expiry → auto-refresh via DiscordAuth.
            DiscordAuth.RegisterExpiryCallback();

            // SetApplicationId, RegisterLaunchSteamApplication, and Connect are NOT called here.
            // They happen in ChangeAppID() once DiscordManager determines the correct app ID.
        }

        public static void ChangeAppID(long newAppId)
        {
            if (newAppId <= 0)
            {
                // No valid app ID — clear state and stay idle (RPC will silently stop).
                if (_connected)
                {
                    _connected = false;
                    CurrentAppID = -1;
                }
                return;
            }

            ulong resolvedId = (ulong)newAppId;

            // Same ID already active — no-op.
            if ((long)resolvedId == CurrentAppID && _connected) return;

            if (!_clientInitialized)
                InitClient();

            // Rich Presence via RPC does not require Connect() / OAuth2.
            // SetApplicationId is the only call needed before UpdateRichPresence.
            SDK.Discord_Client_SetApplicationId(ref _client, resolvedId);
            // Register the game so Discord can launch it via Steam for join invites.
            SDK.Discord_Client_RegisterLaunchSteamApplication(ref _client, resolvedId, 620980u);
            CurrentAppID = (long)resolvedId;
            _connected = true;
            DiscordAuth.TryConnect();
        }

        public static void UpdateRichPresence(Activity activity, Action<bool, string> callback)
        {
            if (!_clientInitialized) return;
            ActivityConverter.UpdateNativeActivity(ref _client, activity, callback);
        }

        public static void ClearRichPresence()
        {
            if (!_clientInitialized) return;
            SDK.Discord_Client_ClearRichPresence(ref _client);
        }

        public static void RunCallbacks()
        {
            // Global — no per-client call needed in the Social SDK.
            SDK.Discord_RunCallbacks();
        }

        // === Native callback implementations ===

        private static void OnNativeStatusChanged(
            Discord_Client_Status status,
            Discord_Client_Error error,
            int errorDetail,
            IntPtr userData)
        {
            _currentStatus = status;
            // Log at Debug level; in RPC mode (no OAuth2) these status events are informational only
            // and do not affect the ability to set rich presence.
            Plugin.log.Debug($"[DISCORD] Status: {status} (error={error}, detail={errorDetail})");
        }

        private static void OnNativeLog(Discord_String message, Discord_LoggingSeverity severity, IntPtr userData)
        {
            if (message.ptr == null || (int)message.size == 0) return;
            string msg = Encoding.UTF8.GetString(message.ptr, (int)message.size);

            Logger.Level level;
            if (!_logLevels.TryGetValue(severity, out level))
                level = Logger.Level.Debug;

            Plugin.log.Log(level, $"[DISCORD] {msg}");
        }

        private static void OnNativeActivityJoin(Discord_String joinSecret, IntPtr userData)
        {
            if (joinSecret.ptr == null || (int)joinSecret.size == 0) return;
            string secret = Encoding.UTF8.GetString(joinSecret.ptr, (int)joinSecret.size);
            OnActivityJoin?.Invoke(secret);
        }

        private static void OnNativeActivityInvite(IntPtr invitePtr, IntPtr userData)
        {
            // invitePtr is a Discord_ActivityInvite* passed from native code.
            Discord_ActivityInvite invite = Marshal.PtrToStructure<Discord_ActivityInvite>(invitePtr);
            Discord_ActivityActionTypes nativeType = SDK.Discord_ActivityInvite_Type(ref invite);
            ulong senderId = SDK.Discord_ActivityInvite_SenderId(ref invite);

            // Populate a User with only Id — the Social SDK doesn't provide full user details here.
            User user = new User { Id = (long)senderId };
            Activity activity = default; // Social SDK doesn't pass full activity details in invite callback

            if (nativeType == Discord_ActivityActionTypes.JoinRequest)
            {
                if (Config.Instance.AllowJoin)
                    OnActivityJoinRequest?.Invoke(ref user);
            }
            else if (nativeType == Discord_ActivityActionTypes.Join)
            {
                if (Config.Instance.AllowInvites)
                    OnActivityInvite?.Invoke(ActivityActionType.Join, ref user, ref activity);
            }
        }
    }
}
