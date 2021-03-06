﻿namespace UIURescueSquad
{
#pragma warning disable SA1202

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Configs;
    using Exiled.API.Extensions;
    using Exiled.API.Features;
    using Exiled.CustomItems.API.Features;
    using Exiled.Events.EventArgs;
    using MEC;
    using Respawning;
    using UnityEngine;

    /// <summary>
    /// EventHandlers and Methods which UIURescueSquad uses.
    /// </summary>
    public partial class EventHandlers
    {
        private static readonly Config Config = UIURescueSquad.Instance.Config;

        /// <summary>
        /// Is UIU spawnable in <see cref="Exiled.Events.Handlers.Server.OnRespawningTeam(RespawningTeamEventArgs)"/>.
        /// </summary>
        public static bool IsSpawnable;

        /// <summary>
        /// The maximum number of UIU players in next the respawn.
        /// </summary>
        public static uint MaxPlayers;

        /// <summary>
        /// Players that are currently UIU.
        /// </summary>
        public static List<Player> UiuPlayers = new List<Player>();

        private static System.Random rng = new System.Random();
        private static int respawns = 0;

        /// <summary>
        /// Handles UIU spawn chance with all other conditions.
        /// </summary>
        internal static void CalculateChance()
        {
            IsSpawnable = rng.Next(1, 101) <= Config.SpawnManager.Probability &&
                respawns >= Config.SpawnManager.Respawns;

            Log.Debug($"Is UIU spawnable: {IsSpawnable}", Config.Debug);
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Server.OnWaitingForPlayers"/>
        internal static void OnWaitingForPlayers()
        {
            UiuPlayers.Clear();
            respawns = 0;
            MaxPlayers = Config.SpawnManager.MaxSquad;
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Server.OnRoundStarted"/>
        internal static void OnRoundStart()
        {
            if (!string.IsNullOrEmpty(Config.TeamColors.GuardUnitColor))
                Map.ChangeUnitColor(0, Config.TeamColors.GuardUnitColor);
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Server.OnRespawningTeam(RespawningTeamEventArgs)"/>
        internal static void OnTeamRespawn(RespawningTeamEventArgs ev)
        {
            if (ev.NextKnownTeam == SpawnableTeamType.NineTailedFox)
            {
                respawns++;

                if (IsSpawnable)
                {
                    bool prioritySpawn = RespawnManager.Singleton._prioritySpawn;

                    if (prioritySpawn)
                        ev.Players.OrderBy((x) => x.ReferenceHub.characterClassManager.DeathTime);

                    for (int i = ev.Players.Count; i > MaxPlayers; i--)
                    {
                        Player player = prioritySpawn ? ev.Players.Last() : ev.Players[rng.Next(ev.Players.Count)];
                        ev.Players.Remove(player);
                    }

                    List<Player> uiuPlayers = new List<Player>(ev.Players);

                    Timing.CallDelayed(0f, () =>
                    {
                        foreach (Player player in uiuPlayers)
                        {
                            SpawnPlayer(player);
                        }

                        if (Config.SpawnManager.AnnouncementText != null)
                        {
                            Map.ClearBroadcasts();
                            Map.Broadcast(Config.SpawnManager.AnnouncementTime, Config.SpawnManager.AnnouncementText);
                        }

                        if (Config.SupplyDrop.DropEnabled)
                        {
                            foreach (var item in Config.SupplyDrop.DropItems)
                            {
                                Vector3 spawnPos = Role.GetRandomSpawnPoint(RoleType.NtfCadet);

                                if (Enum.TryParse(item.Key, out ItemType parsedItem))
                                {
                                    Item.Spawn(parsedItem, Item.GetDefaultDurability(parsedItem), spawnPos);
                                }
                                else
                                {
                                    CustomItem.TrySpawn(item.Key, spawnPos, out Pickup pickup);
                                }
                            }
                        }
                    });

                    if (!string.IsNullOrEmpty(Config.TeamColors.UiuUnitColor))
                    {
                        Timing.CallDelayed(Timing.WaitUntilTrue(() => RespawnManager.Singleton.NamingManager.AllUnitNames.Count >= respawns), () =>
                        {
                            Map.ChangeUnitColor(respawns, Config.TeamColors.UiuUnitColor);
                        });
                    }

                    MaxPlayers = Config.SpawnManager.MaxSquad;
                }
                else if (!string.IsNullOrEmpty(Config.TeamColors.NtfUnitColor))
                {
                    Timing.CallDelayed(Timing.WaitUntilTrue(() => RespawnManager.Singleton.NamingManager.AllUnitNames.Count >= respawns), () =>
                    {
                        Map.ChangeUnitColor(respawns, Config.TeamColors.NtfUnitColor);
                    });
                }
            }
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Map.OnAnnouncingNtfEntrance(AnnouncingNtfEntranceEventArgs) />
        internal static void OnAnnouncingNTF(AnnouncingNtfEntranceEventArgs ev)
        {
            string cassieMessage = string.Empty;

            if (!IsSpawnable)
            {
                if (ev.ScpsLeft == 0 && !string.IsNullOrEmpty(Config.SpawnManager.NtfAnnouncmentCassieNoScp))
                {
                    ev.IsAllowed = false;

                    cassieMessage = Config.SpawnManager.NtfAnnouncmentCassieNoScp;
                }
                else if (!string.IsNullOrEmpty(Config.SpawnManager.NtfAnnouncementCassie))
                {
                    ev.IsAllowed = false;

                    cassieMessage = Config.SpawnManager.NtfAnnouncementCassie;
                }
            }
            else
            {
                ev.IsAllowed = false;

                if (ev.ScpsLeft == 0 && !string.IsNullOrEmpty(Config.SpawnManager.UiuAnnouncmentCassieNoScp))
                {
                    cassieMessage = Config.SpawnManager.UiuAnnouncmentCassieNoScp;
                }
                else if (ev.ScpsLeft > 1 && !string.IsNullOrEmpty(Config.SpawnManager.UiuAnnouncementCassie))
                {
                    cassieMessage = Config.SpawnManager.UiuAnnouncementCassie;
                }
            }

            cassieMessage = cassieMessage.Replace("{scpnum}", $"{ev.ScpsLeft} scpsubject");

            if (ev.ScpsLeft > 1)
                cassieMessage = cassieMessage.Replace("scpsubject", "scpsubjects");

            cassieMessage = cassieMessage.Replace("{designation}", $"nato_{ev.UnitName[0]} {ev.UnitNumber}");

            if (!string.IsNullOrEmpty(cassieMessage))
                Cassie.GlitchyMessage(cassieMessage, Config.SpawnManager.GlitchChance, Config.SpawnManager.JamChance);
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Player.OnDestroying(DestroyingEventArgs)"/>
        internal static void OnDestroy(DestroyingEventArgs ev)
        {
            if (UiuPlayers.Contains(ev.Player))
            {
                DestroyUIU(ev.Player);
            }
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Player.OnDied(DiedEventArgs)"/>
        internal static void OnDied(DiedEventArgs ev)
        {
            if (UiuPlayers.Contains(ev.Target))
            {
                DestroyUIU(ev.Target);
            }
        }

        /// <inheritdoc cref="Exiled.Events.Handlers.Player.OnChangingRole(ChangingRoleEventArgs)"/>
        internal static void OnChanging(ChangingRoleEventArgs ev)
        {
            if (UiuPlayers.Contains(ev.Player) && ev.NewRole.GetTeam() != Team.MTF)
            {
                DestroyUIU(ev.Player);
            }
        }
    }
}
