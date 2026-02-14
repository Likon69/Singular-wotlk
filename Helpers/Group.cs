using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Helpers
{
    internal static class Group
    {
        // WotLK 3.3.5a tank auras — Frost Presence is tank presence in WotLK (not Blood)
        private static readonly string[] TankAuras = 
            { "Bear Form", "Dire Bear Form", "Defensive Stance", "Frost Presence", "Righteous Fury" };

        public static bool MeIsTank
        {
            get
            {
                // LFD / raid role assignment (works for Dungeon Finder groups)
                if ((StyxWoW.Me.Role & WoWPartyMember.GroupRole.Tank) != 0)
                    return true;

                // Spec-based fallback for manually formed groups
                var spec = TalentManager.CurrentSpec;
                // WotLK: Blood is the tank spec (Frost Presence for tanking). Frost DK is DPS in WotLK.
                if (spec == TalentSpec.ProtectionWarrior || spec == TalentSpec.ProtectionPaladin ||
                    spec == TalentSpec.BloodDeathKnight || spec == TalentSpec.FeralDruid)
                {
                    // For Feral, confirm tank stance via aura
                    if (spec == TalentSpec.FeralDruid)
                        return StyxWoW.Me.HasAura("Bear Form") || StyxWoW.Me.HasAura("Dire Bear Form");
                    return true;
                }

                // Aura-based last resort (catches stance-dancers etc.)
                return TankAuras.Any(a => StyxWoW.Me.HasAura(a));
            }
        }

        public static bool MeIsHealer
        {
            get
            {
                // LFD / raid role assignment
                if ((StyxWoW.Me.Role & WoWPartyMember.GroupRole.Healer) != 0)
                    return true;

                // Spec-based fallback
                var spec = TalentManager.CurrentSpec;
                return spec == TalentSpec.HolyPriest || spec == TalentSpec.DisciplinePriest ||
                       spec == TalentSpec.RestorationDruid || spec == TalentSpec.HolyPaladin ||
                       spec == TalentSpec.RestorationShaman;
            }
        }

        public static List<WoWPlayer> Tanks
        {
            get
            {
                var result = new List<WoWPlayer>();

                if (!StyxWoW.Me.IsInParty)
                    return result;

                // Add self if tank
                if (MeIsTank)
                    result.Add(StyxWoW.Me);

                var members = StyxWoW.Me.IsInRaid ? StyxWoW.Me.RaidMemberInfos : StyxWoW.Me.PartyMemberInfos;

                foreach (var m in members)
                {
                    var player = m.ToPlayer();
                    if (player == null || result.Contains(player))
                        continue;

                    // LFD / raid role check
                    if (m.IsTank)
                    {
                        result.Add(player);
                        continue;
                    }

                    // Aura-based fallback for manually formed groups
                    if (TankAuras.Any(a => player.HasAura(a)))
                        result.Add(player);
                }

                return result;
            }
        }

        public static List<WoWPlayer> Healers
        {
            get
            {
                var result = new List<WoWPlayer>();

                if (!StyxWoW.Me.IsInParty)
                    return result;

                // Add self if healer
                if (MeIsHealer)
                    result.Add(StyxWoW.Me);

                var members = StyxWoW.Me.IsInRaid ? StyxWoW.Me.RaidMemberInfos : StyxWoW.Me.PartyMemberInfos;

                foreach (var m in members)
                {
                    var player = m.ToPlayer();
                    if (player == null || result.Contains(player))
                        continue;

                    // LFD / raid role check
                    if (m.IsHealer)
                    {
                        result.Add(player);
                        continue;
                    }

                    // No reliable way to detect other players' spec in WotLK
                    // Healer detection for non-LFD groups relies on role assignments
                }

                return result;
            }
        }

        /// <summary>Gets a player by class priority. The order of which classes are passed in, is the priority to find them.</summary>
        /// <remarks>Created 9/9/2011.</remarks>
        /// <param name="range"></param>
        /// <param name="includeDead"></param>
        /// <param name="classes">A variable-length parameters list containing classes.</param>
        /// <returns>The player by class prio.</returns>
        public static WoWUnit GetPlayerByClassPrio(float range, bool includeDead, params WoWClass[] classes)
        {
            foreach (var woWClass in classes)
            {

                var unit =
                    StyxWoW.Me.PartyMemberInfos.FirstOrDefault(
                        p => p.ToPlayer() != null && p.ToPlayer().Distance < range && p.ToPlayer().Class == woWClass);

                if (unit != null)
                {
                    // Skip dead/ghost players unless includeDead is true
                    if (!includeDead && (unit.Dead || unit.Ghost))
                        continue;
                    return unit.ToPlayer();
                }
            }
            return null;
        }
    }
}
