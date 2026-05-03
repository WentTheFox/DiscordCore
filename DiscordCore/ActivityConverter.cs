using System;
using System.Runtime.InteropServices;
using System.Text;
using Discord;
using DiscordCore.Native;

namespace DiscordCore
{
    // Translates a managed Discord.Activity struct into native Discord_Activity P/Invoke calls,
    // then fires Discord_Client_UpdateRichPresence. Kept separate to avoid bloating DiscordClient.
    internal static unsafe class ActivityConverter
    {
        // Stored as a field so the GC doesn't collect the delegate while native code holds the pointer.
        private static Discord_Client_UpdateRichPresenceCallback _richPresenceCallbackDelegate;
        private static Action<bool, string> _richPresenceUserCallback;
        private static Discord_FreeFn _nullFreeFn = (_) => { };

        internal static void UpdateNativeActivity(
            ref Discord_Client client,
            Activity activity,
            Action<bool, string> callback)
        {
            var nativeActivity = new Discord_Activity();
            SDK.Discord_Activity_Init(ref nativeActivity);

            try
            {
                // Name (required, by value)
                SetRequiredString(ref nativeActivity, activity.Name ?? string.Empty,
                    (ref Discord_Activity a, Discord_String s) => SDK.Discord_Activity_SetName(ref a, s));

                // Type
                SDK.Discord_Activity_SetType(ref nativeActivity, (Discord_ActivityTypes)(int)activity.Type);

                // ApplicationId (optional uint64*)
                if (activity.ApplicationId != 0)
                {
                    ulong appId = (ulong)activity.ApplicationId;
                    SDK.Discord_Activity_SetApplicationId(ref nativeActivity, &appId);
                }

                // State (optional Discord_String*)
                SetOptionalString(ref nativeActivity, activity.State,
                    (ref Discord_Activity a, Discord_String* s) => SDK.Discord_Activity_SetState(ref a, s));

                // Details (optional Discord_String*)
                SetOptionalString(ref nativeActivity, activity.Details,
                    (ref Discord_Activity a, Discord_String* s) => SDK.Discord_Activity_SetDetails(ref a, s));

                // Timestamps
                if (activity.Timestamps.Start != 0 || activity.Timestamps.End != 0)
                {
                    var ts = new Discord_ActivityTimestamps();
                    SDK.Discord_ActivityTimestamps_Init(ref ts);
                    try
                    {
                        if (activity.Timestamps.Start != 0)
                            SDK.Discord_ActivityTimestamps_SetStart(ref ts, (ulong)activity.Timestamps.Start);
                        if (activity.Timestamps.End != 0)
                            SDK.Discord_ActivityTimestamps_SetEnd(ref ts, (ulong)activity.Timestamps.End);
                        SDK.Discord_Activity_SetTimestamps(ref nativeActivity, ref ts);
                    }
                    finally
                    {
                        SDK.Discord_ActivityTimestamps_Drop(ref ts);
                    }
                }

                // Assets
                bool hasAssets = !string.IsNullOrEmpty(activity.Assets.LargeImage)
                              || !string.IsNullOrEmpty(activity.Assets.LargeText)
                              || !string.IsNullOrEmpty(activity.Assets.SmallImage)
                              || !string.IsNullOrEmpty(activity.Assets.SmallText);
                if (hasAssets)
                {
                    var assets = new Discord_ActivityAssets();
                    SDK.Discord_ActivityAssets_Init(ref assets);
                    try
                    {
                        SetOptionalString(ref assets, activity.Assets.LargeImage,
                            (ref Discord_ActivityAssets a, Discord_String* s) => SDK.Discord_ActivityAssets_SetLargeImage(ref a, s));
                        SetOptionalString(ref assets, activity.Assets.LargeText,
                            (ref Discord_ActivityAssets a, Discord_String* s) => SDK.Discord_ActivityAssets_SetLargeText(ref a, s));
                        SetOptionalString(ref assets, activity.Assets.SmallImage,
                            (ref Discord_ActivityAssets a, Discord_String* s) => SDK.Discord_ActivityAssets_SetSmallImage(ref a, s));
                        SetOptionalString(ref assets, activity.Assets.SmallText,
                            (ref Discord_ActivityAssets a, Discord_String* s) => SDK.Discord_ActivityAssets_SetSmallText(ref a, s));
                        SDK.Discord_Activity_SetAssets(ref nativeActivity, ref assets);
                    }
                    finally
                    {
                        SDK.Discord_ActivityAssets_Drop(ref assets);
                    }
                }

                // Party
                if (!string.IsNullOrEmpty(activity.Party.Id))
                {
                    var party = new Discord_ActivityParty();
                    SDK.Discord_ActivityParty_Init(ref party);
                    try
                    {
                        SetRequiredString(ref party, activity.Party.Id,
                            (ref Discord_ActivityParty p, Discord_String s) => SDK.Discord_ActivityParty_SetId(ref p, s));
                        SDK.Discord_ActivityParty_SetCurrentSize(ref party, activity.Party.Size.CurrentSize);
                        SDK.Discord_ActivityParty_SetMaxSize(ref party, activity.Party.Size.MaxSize);
                        SDK.Discord_ActivityParty_SetPrivacy(ref party, (Discord_ActivityPartyPrivacy)(int)activity.Party.Privacy);
                        SDK.Discord_Activity_SetParty(ref nativeActivity, ref party);
                    }
                    finally
                    {
                        SDK.Discord_ActivityParty_Drop(ref party);
                    }
                }

                // Secrets — only Join is supported; Spectate and Match are not in Social SDK
                if (!string.IsNullOrEmpty(activity.Secrets.Join))
                {
                    var secrets = new Discord_ActivitySecrets();
                    SDK.Discord_ActivitySecrets_Init(ref secrets);
                    try
                    {
                        SetRequiredString(ref secrets, activity.Secrets.Join,
                            (ref Discord_ActivitySecrets s, Discord_String ds) => SDK.Discord_ActivitySecrets_SetJoin(ref s, ds));
                        SDK.Discord_Activity_SetSecrets(ref nativeActivity, ref secrets);
                    }
                    finally
                    {
                        SDK.Discord_ActivitySecrets_Drop(ref secrets);
                    }
                }

                // Store callback in field before passing to native — prevents GC collection
                _richPresenceUserCallback = callback;
                _richPresenceCallbackDelegate = OnRichPresenceCallback;

                SDK.Discord_Client_UpdateRichPresence(
                    ref client,
                    ref nativeActivity,
                    _richPresenceCallbackDelegate,
                    _nullFreeFn,
                    IntPtr.Zero);
            }
            finally
            {
                SDK.Discord_Activity_Drop(ref nativeActivity);
            }
        }

        private static void OnRichPresenceCallback(IntPtr resultPtr, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            bool ok = SDK.Discord_ClientResult_Successful(ref result);
            string errMsg = null;
            if (!ok)
            {
                Discord_String errStr = default;
                SDK.Discord_ClientResult_Error(ref result, &errStr);
                if (errStr.ptr != null && (int)errStr.size > 0)
                    errMsg = Encoding.UTF8.GetString(errStr.ptr, (int)errStr.size);
                else
                    errMsg = SDK.Discord_ClientResult_Type(ref result).ToString();
            }
            _richPresenceUserCallback?.Invoke(ok, errMsg);
        }

        // Helper: SetRequiredString — converts string to UTF-8, pins it, calls setter by value.
        private static void SetRequiredString<T>(
            ref T target,
            string value,
            SetByValueDelegate<T> setter) where T : struct
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            fixed (byte* p = bytes)
            {
                var ds = Discord_String.From(p, bytes.Length);
                setter(ref target, ds);
            }
        }

        // Helper: SetOptionalString — if non-empty, pins and passes pointer; otherwise passes null.
        private static void SetOptionalString<T>(
            ref T target,
            string value,
            SetNullableDelegate<T> setter) where T : struct
        {
            if (!string.IsNullOrEmpty(value))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                fixed (byte* p = bytes)
                {
                    var ds = Discord_String.From(p, bytes.Length);
                    setter(ref target, &ds);
                }
            }
            else
            {
                setter(ref target, null);
            }
        }

        private delegate void SetByValueDelegate<T>(ref T target, Discord_String value) where T : struct;
        private delegate void SetNullableDelegate<T>(ref T target, Discord_String* value) where T : struct;
    }
}
