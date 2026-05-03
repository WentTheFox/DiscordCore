using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;
using DiscordCore.UI;
using IPA;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Discord;

namespace DiscordCore
{
    public class DiscordManager : MonoBehaviour
    {
        public static bool active = true;

        internal List<DiscordInstance> _activeInstances = new List<DiscordInstance>();
        private float lastUpdateTime;
        public static DiscordManager instance = null;

        public static string deactivationReason;

        public void Awake()
        {
            Plugin.log.Debug($"{nameof(DiscordManager)} Awake");
            DiscordClient.OnActivityInvite += DiscordClient_OnActivityInvite;
            DiscordClient.OnActivityJoin += DiscordClient_OnActivityJoin;
            DiscordClient.OnActivityJoinRequest += DiscordClient_OnActivityJoinRequest;
            DiscordClient.OnActivitySpectate += DiscordClient_OnActivitySpectate;

            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void SetDeactivationReasonFromException(Exception e)
        {
            deactivationReason = e.Message;
        }

        public DiscordInstance CreateInstance(DiscordSettings settings)
        {
            DiscordInstance instance = new DiscordInstance(settings);

            if (Config.Instance.ModStates.TryGetValue(settings.modId, out var state))
            {
                while (_activeInstances.Any(x => x.Priority == state.Priority))
                    state.Priority++;

                instance.Priority = state.Priority;
                instance.activityEnabled = state.Active;
                Config.Instance.ModStates[settings.modId] = state;
            }
            else
            {
                instance.Priority = _activeInstances.Count == 0 ? 0 : _activeInstances.Max(x => x.Priority) + 1;
                instance.activityEnabled = true;

                Config.Instance.ModStates.Add(instance.settings.modId, new ModState() { Active = true, Priority = instance.Priority });
            }

            Config.Instance.Save();

            _activeInstances.Add(instance);
            Plugin.log.Debug($"Added new instance with AppId {instance.settings.appId}");

            Settings.instance.UpdateModsList();

            return instance;
        }

        public void DestroyInstance(DiscordInstance instance)
        {
            if (_activeInstances.Contains(instance))
            {
                _activeInstances.Remove(instance);
                Settings.instance.UpdateModsList();
            }
        }

        public void Update()
        {
            if (!DiscordClient.Enabled) return;

            // Always pump callbacks so status changes arrive on the main thread.
            DiscordClient.RunCallbacks();

            if (!DiscordClient.IsConnected)
            {
                // App ID not yet set — runs on the first Update() tick after all BSIPA mods have
                // completed their Init()/OnEnable() and registered their instances.
                // Pick the highest-priority instance app ID for RPC rich presence.
                ConnectWithBestAppId();
                return;
            }

            // Only update activity once an app ID has been configured.
            if (!active || !DiscordClient.IsReady) return;

            if (Time.time - lastUpdateTime >= 5f)
            {
                lastUpdateTime = Time.time;
                UpdateCurrentActivity();
            }
        }

        private void ConnectWithBestAppId()
        {
            long bestAppId = -1;
            int bestPriority = int.MaxValue;
            foreach (var inst in _activeInstances)
            {
                if (inst.activityEnabled && inst.Priority < bestPriority)
                {
                    bestPriority = inst.Priority;
                    bestAppId = inst.settings.appId;
                }
            }
            DiscordClient.ChangeAppID(bestAppId);
        }

        public void OnEnable()
        {
            active = true;
            // Enable() no longer throws — connection errors arrive via the status callback
            // (OnNativeStatusChanged in DiscordClient) which sets active and deactivationReason.
            DiscordClient.Enable();
        }

        public void OnDisable()
        {
            active = false;
            DiscordClient.Disable();
        }

        private void UpdateCurrentActivity()
        {
            bool activityFound = false;
            int activityPriority = int.MaxValue;
            Activity topPriorityActivity = default;
            long appId = -1;

            foreach (var instance in _activeInstances)
            {
                if (instance.activityValid && instance.activityEnabled && instance.Priority < activityPriority)
                {
                    activityFound = true;
                    activityPriority = instance.Priority;
                    topPriorityActivity = instance.activity;
                    appId = instance.settings.appId;
                }
            }

            if (activityFound)
            {
                DiscordClient.ChangeAppID(appId);
                DiscordClient.UpdateRichPresence(topPriorityActivity, (ok, err) => {
                    if (!ok)
                        Plugin.log.Debug($"Activity update failed: {err}");
                });
            }
            else
            {
                DiscordClient.ChangeAppID(-1);
                DiscordClient.ClearRichPresence();
            }
        }

        #region Event Handlers

        private DiscordInstance FindActivityEventHandler()
        {
            DiscordInstance handlerInstance = null;

            foreach (var instance in _activeInstances)
            {
                if (instance.activityValid && instance.activityEnabled && instance.settings.handleInvites
                    && instance.settings.appId == DiscordClient.CurrentAppID
                    && (handlerInstance == null || instance.Priority < handlerInstance.Priority))
                {
                    handlerInstance = instance;
                }
            }

            return handlerInstance;
        }

        private void DiscordClient_OnActivitySpectate(string secret)
        {
            FindActivityEventHandler()?.CallActivitySpectate(secret);
        }

        private void DiscordClient_OnActivityJoinRequest(ref User user)
        {
            FindActivityEventHandler()?.CallActivityJoinRequest(ref user);
        }

        private void DiscordClient_OnActivityJoin(string secret)
        {
            FindActivityEventHandler()?.CallActivityJoin(secret);
        }

        private void DiscordClient_OnActivityInvite(ActivityActionType type, ref User user, ref Activity activity)
        {
            FindActivityEventHandler()?.CallActivityInvite(type, ref user, ref activity);
        }

        #endregion
    }
}
