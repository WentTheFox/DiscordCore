using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiscordCore.UI
{
    public class Settings : MonoBehaviour
    {
        public static Settings instance = new Settings();

        [UIValue("enable-plugin")]
        public bool enablePlugin { get { return Config.Instance.EnableDiscordCore; } set { Config.Instance.EnableDiscordCore = value; } }
        [UIValue("allow-join")]
        public bool allowJoinRequests { get { return Config.Instance.AllowJoin; } set { Config.Instance.AllowJoin = value; } }
        [UIValue("allow-spectator")]
        public bool allowSpectatorRequests { get { return Config.Instance.AllowSpectate; } set { Config.Instance.AllowSpectate = value; } }
        [UIValue("allow-invites")]
        public bool allowInvites { get { return Config.Instance.AllowInvites; } set { Config.Instance.AllowInvites = value; } }

#pragma warning disable CS0649 // assigned by BSML via reflection
        [UIObject("discord-connect-btn")]
        private GameObject _connectBtn;

        [UIObject("discord-disconnect-btn")]
        private GameObject _disconnectBtn;
#pragma warning restore CS0649

        [UIComponent("mods-list")]
        public CustomCellListTableData modsList;

        [UIValue("mods")]
        public List<object> modObjectsList = new List<object>();

        [UIAction("connect-discord")]
        private void ConnectDiscord()
        {
            DiscordClient.Authorize((ok, err) =>
            {
                if (!ok) Plugin.log.Error($"Discord auth failed: {err}");
                RefreshAuthButtons();
            });
        }

        [UIAction("disconnect-discord")]
        private void DisconnectDiscord()
        {
            DiscordClient.RevokeAuth(RefreshAuthButtons);
        }

        private void RefreshAuthButtons()
        {
            bool auth = DiscordClient.IsAuthenticated;
            if (_connectBtn != null) _connectBtn.SetActive(!auth);
            if (_disconnectBtn != null) _disconnectBtn.SetActive(auth);
        }

        [UIAction("#post-parse")]
        public void UpdateModsList()
        {
            RefreshAuthButtons();
            try
            {
                var instances = DiscordManager.instance._activeInstances.OrderBy(y => y.Priority).ToList();

                if (modObjectsList.Count != instances.Count)
                {
                    modObjectsList.Clear();

                    foreach (var instance in instances)
                    {
                        var listObject = new ModListObject(instance);

                        listObject.activeStateChanged += ListObject_activeStateChanged;
                        listObject.increasePriorityPressed += ListObject_increasePriorityPressed;
                        listObject.decreasePriorityPressed += ListObject_decreasePriorityPressed;

                        modObjectsList.Add(listObject);
                    }
                }
                else
                {
                    for (int i = 0; i < modObjectsList.Count; i++)
                        (modObjectsList[i] as ModListObject).ReplaceModInstance(instances[i]);
                }

                if (modsList != null)
                    modsList.TableView.ReloadData();
            }
            catch (Exception e)
            {
                Plugin.log.Error($"Unable to update mods list in settings! Exception: {e}");
            }
        }

        private void ListObject_increasePriorityPressed(DiscordInstance sender)
        {
            var nextInstance = DiscordManager.instance._activeInstances.OrderByDescending(x => x.Priority).FirstOrDefault(x => x.Priority < sender.Priority);

            if (nextInstance != null)
            {
                int temp = sender.Priority;
                sender.Priority = nextInstance.Priority;
                nextInstance.Priority = temp;
                UpsateModState(sender);
                UpsateModState(nextInstance);
                UpdateModsList();
            }
        }

        private void ListObject_decreasePriorityPressed(DiscordInstance sender)
        {
            var prevInstance = DiscordManager.instance._activeInstances.OrderBy(x => x.Priority).FirstOrDefault(x => x.Priority > sender.Priority);

            if (prevInstance != null)
            {
                int temp = sender.Priority;
                sender.Priority = prevInstance.Priority;
                prevInstance.Priority = temp;
                UpsateModState(sender);
                UpsateModState(prevInstance);
                UpdateModsList();
            }
        }

        private void ListObject_activeStateChanged(DiscordInstance sender, bool newState)
        {
            sender.activityEnabled = newState;
            UpsateModState(sender);
        }

        public void UpsateModState(DiscordInstance sender)
        {
            if (Config.Instance.ModStates.ContainsKey(sender.settings.modId))
                Config.Instance.ModStates[sender.settings.modId] = new ModState() { Active = sender.activityEnabled, Priority = sender.Priority };
            else
                Config.Instance.ModStates.Add(sender.settings.modId, new ModState() { Active = sender.activityEnabled, Priority = sender.Priority });
            Config.Instance.Save();
        }

        public class ModListObject
        {
            public event Action<DiscordInstance> increasePriorityPressed;
            public event Action<DiscordInstance> decreasePriorityPressed;
            public event Action<DiscordInstance, bool> activeStateChanged;

            private DiscordInstance modInstance;

            [UIParams]
            protected BSMLParserParams parserParams;

            [UIComponent("mod-name")]
            protected TextMeshProUGUI modName;

            [UIComponent("mod-icon")]
            protected Image modIcon;

            [UIValue("enable-mod")]
            protected bool enableMod;

            public ModListObject(DiscordInstance instance)
            {
                modInstance = instance;
                enableMod = modInstance.activityEnabled;
            }

            public void ReplaceModInstance(DiscordInstance instance)
            {
                modInstance = instance;
                enableMod = modInstance.activityEnabled;

                if (modIcon != null && modName != null)
                {
                    Refresh(false, false);
                    parserParams.EmitEvent("cancel");
                }
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool selected, bool highlighted)
            {
                modName.text = modInstance.settings.modName;
                modIcon.gameObject.SetActive(modInstance.settings.modIcon != null);
                modIcon.sprite = modInstance.settings.modIcon;
            }

            [UIAction("increase-priority")]
            private void IncreasePriority()
            {
                increasePriorityPressed?.Invoke(modInstance);
            }

            [UIAction("decrease-priority")]
            private void DecreasePriority()
            {
                decreasePriorityPressed?.Invoke(modInstance);
            }

            [UIAction("active-state-changed")]
            private void ActiveStateChanged(bool newState)
            {
                activeStateChanged?.Invoke(modInstance, newState);
            }
        }

    }
}
