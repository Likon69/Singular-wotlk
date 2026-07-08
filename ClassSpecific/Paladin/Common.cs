using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;


namespace Singular.ClassSpecific.Paladin
{
    public enum PaladinSeal
    {
        None,    // sentinel returned by GetBestSeal() when no seal is usable (added for d3c4edd)
        Auto,
        Command,
        Corruption,
        Justice,
        Light,
        Righteousness,
        Vengeance,
        Wisdom
    }
    public enum PaladinAura
    {
        Auto,
        Devotion,
        Retribution,
        // WotLK QC: In WotLK, resistance auras are separate: Shadow/Fire/Frost Resistance Aura
        // "Resistance Aura" (merged) was added in Cata 4.0.1. Keeping enum value for settings compat,
        // but Spell.BuffSelf("Resistance Aura") will silently fail — use specific auras in rotation if needed
        Resistance,
        Concentration,
        Crusader
    }

    enum PaladinBlessings
    {
        Auto, Kings, Might, Wisdom // WotLK: Blessing of Wisdom is separate (merged into Might in Cata 4.0.1)
    }

    public class Common
    {
        [Class(WoWClass.Paladin)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Spec(TalentSpec.HolyPaladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                // This won't run, but it's here for changes in the future. We NEVER run this method if we're mounted.
                    Spell.BuffSelf("Crusader Aura", ret => StyxWoW.Me.Mounted),
                    CreatePaladinBlessBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec == TalentSpec.HolyPaladin,
                        new PrioritySelector(
                            Spell.BuffSelf("Concentration Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto),
                            // WotLK uses Seal of Wisdom for mana regen (renamed to Seal of Insight in Cata 4.0.1)
                            Spell.BuffSelf("Seal of Wisdom"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Wisdom"))
                            )),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != TalentSpec.HolyPaladin,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin && StyxWoW.Me.IsInParty),
                            Spell.BuffSelf(
                                "Devotion Aura",
                                ret =>
                                SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto &&
                                (StyxWoW.Me.IsInParty && TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin ||
                                 TalentManager.CurrentSpec == TalentSpec.Lowbie && !SpellManager.HasSpell("Retribution Aura"))),
                            Spell.BuffSelf(
                                "Retribution Aura",
                                ret =>
                                SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto &&
                                ((!StyxWoW.Me.IsInParty && TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin) ||
                                 TalentManager.CurrentSpec == TalentSpec.Lowbie)),
                            Spell.BuffSelf(
                                "Retribution Aura",
                                ret =>
                                SingularSettings.Instance.Paladin.Aura == PaladinAura.Auto &&
                                TalentManager.CurrentSpec == TalentSpec.RetributionPaladin),
                            // Select seal added by xyFaded
                            new Decorator(
                                ret => SingularSettings.Instance.Paladin.Seal != PaladinSeal.Auto,
                                new PrioritySelector(
                                    Spell.BuffSelf("Seal of Command", ret => SpellManager.HasSpell("Seal of Command") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Command),
                                    Spell.BuffSelf("Seal of Corruption", ret => SpellManager.HasSpell("Seal of Corruption") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Corruption),
                                    Spell.BuffSelf("Seal of Justice", ret => SpellManager.HasSpell("Seal of Justice") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Justice),
                                    Spell.BuffSelf("Seal of Light", ret => SpellManager.HasSpell("Seal of Light") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Light),
                                    Spell.BuffSelf("Seal of Righteousness", ret => SpellManager.HasSpell("Seal of Righteousness") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Righteousness),
                                    Spell.BuffSelf("Seal of Vengeance", ret => SpellManager.HasSpell("Seal of Vengeance") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Vengeance),
                                    Spell.BuffSelf("Seal of Wisdom", ret => SpellManager.HasSpell("Seal of Wisdom") && SingularSettings.Instance.Paladin.Seal == PaladinSeal.Wisdom)
                                )
                            ),
                            new Decorator(
                                ret => SingularSettings.Instance.Paladin.Seal == PaladinSeal.Auto,
                                new PrioritySelector(
                                    Spell.BuffSelf("Seal of Vengeance", ret => !SpellManager.HasSpell("Seal of Corruption")),
                                    Spell.BuffSelf("Seal of Corruption"),
                                    Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Vengeance") && !SpellManager.HasSpell("Seal of Corruption"))
                                )
                            ),
                    new Decorator(
                        ret => SingularSettings.Instance.Paladin.Aura != PaladinAura.Auto,
                        new PrioritySelector(
                            Spell.BuffSelf("Devotion Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Devotion),
                            Spell.BuffSelf("Concentration Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Concentration),
                            // WotLK QC: "Resistance Aura" does not exist in WotLK (Cata merged Shadow/Fire/Frost Resistance Aura)
                            // Using Shadow Resistance Aura as default since it's the most commonly useful
                            Spell.BuffSelf("Shadow Resistance Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Resistance),
                            Spell.BuffSelf("Retribution Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Retribution),
                            Spell.BuffSelf("Crusader Aura", ret => SingularSettings.Instance.Paladin.Aura == PaladinAura.Crusader)
                            ))
                    
                    )));
        }

        private static Composite CreatePaladinBlessBehavior()
        {
            // WotLK QC: wrap each Blessing cast in a Throttle(2s) so we don't re-attempt the cast
            // every pulse while the buff is missing/being-applied. Without this, the priority
            // selector re-evaluates and re-issues CastSpell multiple times per second (e.g. when
            // moving, drinking, or the buff hasn't propagated yet), draining mana and spamming
            // "Casting Blessing of Might on Myself" in the log. Matches Singular 5.4.8's
            // spanBuffFrequency (20s) throttling behavior, scaled down to 2s since the WotLK
            // routine has no group-wide IsItTimeToBuff() guard.
            return
                new PrioritySelector(
                    // WotLK: Blessing of Wisdom — separate from Might in WotLK (merged in Cata 4.0.1)
                    new Throttle(2, Spell.Cast("Blessing of Wisdom",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings != PaladinBlessings.Wisdom)
                                return false;
                            var players = new List<WoWPlayer>();

                            if (StyxWoW.Me.IsInRaid)
                                players.AddRange(StyxWoW.Me.RaidMembers);
                            else if (StyxWoW.Me.IsInParty)
                                players.AddRange(StyxWoW.Me.PartyMembers);

                            players.Add(StyxWoW.Me);

                            return players.Any(
                                        p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                             !p.HasAura("Blessing of Wisdom"));
                        })),
                    new Throttle(2, Spell.Cast("Blessing of Kings",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might ||
                                SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Wisdom)
                                return false;
                            var players = new List<WoWPlayer>();

                            if (StyxWoW.Me.IsInRaid)
                                players.AddRange(StyxWoW.Me.RaidMembers);
                            else if (StyxWoW.Me.IsInParty)
                                players.AddRange(StyxWoW.Me.PartyMembers);

                            players.Add(StyxWoW.Me);

                            return players.Any(
                                        p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                             !p.HasAura("Blessing of Kings") &&
                                             !p.HasAura("Mark of the Wild")
                                             // WotLK QC: Removed "Embrace of the Shale Spider" (Cata-only Shale Spider exotic pet buff)
                                             );
                        })),
                    new Throttle(2, Spell.Cast("Blessing of Might",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Wisdom)
                                return false;
                            var players = new List<WoWPlayer>();

                            if (StyxWoW.Me.IsInRaid)
                                players.AddRange(StyxWoW.Me.RaidMembers);
                            else if (StyxWoW.Me.IsInParty)
                                players.AddRange(StyxWoW.Me.PartyMembers);

                            players.Add(StyxWoW.Me);

                            return players.Any(
                                        p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                             !p.HasAura("Blessing of Might") &&
                                             (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might ||
                                             ((p.HasAura("Blessing of Kings") && !p.HasMyAura("Blessing of Kings")) ||
                                               p.HasAura("Mark of the Wild"))));
                                               // WotLK QC: Removed "Embrace of the Shale Spider" (Cata-only)
                        }))
                    );
        }

        // Cached seal selection. Set once per Combat/CombatBuffs evaluation so we don't
        // call GetBestSeal() twice in the same tick (which would re-evaluate Unit.NearbyUnfriendlyUnits
        // and friends list filtering - wasteful, and can flicker between two seals when the
        // hostile count sits exactly on a threshold like 4).
        // HB 4.3.4 / Singular 4.3.4 hardcode the seal list per behavior; Singular 5.4.8 introduced
        // this cached helper so all combat behaviors share the same seal selection logic and
        // respect the user setting. We follow the 5.4.8 pattern (it is the only one that
        // honors PaladinSettings.Seal) and adapt the seal names for WotLK 3.3.5a.
        private static PaladinSeal _currentSeal;

        // Returns the spell name for a PaladinSeal enum value.
        // "Seal of " + PaladinSeal.ToString() gives the in-game spell name.
        private static string SealSpell(PaladinSeal s)
        {
            return "Seal of " + s.ToString();
        }

        // Picks the seal to use this tick. Respects the user setting first; if Auto,
        // performs spec-aware seal twisting. Adapts Singular 5.4.8 GetBestSeal() to WotLK 3.3.5a:
        //   - No "Seal of Truth" (Cata 4.0.1+) -> use Seal of Vengeance (Alliance) /
        //     Seal of Corruption (Horde) for single target.
        //   - No "Seal of Insight" (Cata 4.0.1+) -> use Seal of Wisdom (WotLK mana regen seal,
        //     learned at level 30 as a talent).
        //   - AoE 4+ mobs -> Seal of Righteousness.
        //   - Manual setting takes priority over Auto when the seal is known.
        public static PaladinSeal GetBestSeal()
        {
            PaladinSeal configuredSeal = SingularSettings.Instance.Paladin.Seal;

            // Manual setting: use it if the paladin knows the spell.
            if (configuredSeal != PaladinSeal.Auto)
            {
                if (SpellManager.HasSpell(SealSpell(configuredSeal)))
                    return configuredSeal;

                // Configured seal not known (e.g. user set Corruption on Alliance) -> fall through
                // to Auto behavior. This matches Singular 5.4.8 GetBestSeal() semantics.
            }

            TalentSpec spec = TalentManager.CurrentSpec;

            // Holy in group -> Seal of Wisdom (mana regen).
            if (spec == TalentSpec.HolyPaladin && StyxWoW.Me.IsInParty)
            {
                if (SpellManager.HasSpell("Seal of Wisdom"))
                    return PaladinSeal.Wisdom;
            }

            // Retribution / Protection: seal twisting.
            if (spec == TalentSpec.RetributionPaladin || spec == TalentSpec.ProtectionPaladin)
            {
                // Low mana: pop Seal of Wisdom to refill, then put main seal back.
                // Thresholds from Singular 5.4.8: switch to Wisdom at < 5% or at < 30% if we
                // already have Wisdom (avoid flip-flop on the way back up).
                if (SpellManager.HasSpell("Seal of Wisdom") &&
                    (StyxWoW.Me.ManaPercent < 5 ||
                     (StyxWoW.Me.ManaPercent < 30 && StyxWoW.Me.HasMyAura("Seal of Wisdom"))))
                {
                    return PaladinSeal.Wisdom;
                }

                // AoE: 4+ enemies in 8yd -> Seal of Righteousness.
                if (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4)
                {
                    if (SpellManager.HasSpell("Seal of Righteousness"))
                        return PaladinSeal.Righteousness;
                }
            }

            // Single target: Seal of Vengeance (Alliance) / Seal of Corruption (Horde).
            // WotLK equivalents of HB 4.3.4 / Singular 4.3.4's "Seal of Truth".
            if (SpellManager.HasSpell("Seal of Corruption"))
                return PaladinSeal.Corruption;
            if (SpellManager.HasSpell("Seal of Vengeance"))
                return PaladinSeal.Vengeance;

            // Fallbacks: Command if known, else Righteousness.
            if (SpellManager.HasSpell("Seal of Command"))
                return PaladinSeal.Command;
            if (SpellManager.HasSpell("Seal of Righteousness"))
                return PaladinSeal.Righteousness;

            return PaladinSeal.None;
        }

        // Composite that selects the best seal this tick and casts it if not already active.
        // Singular 5.4.8 pattern (CreatePaladinSealBehavior), ported to WotLK.
        public static Composite CreatePaladinSealBehavior()
        {
            return new Sequence(
                new Action(ret => _currentSeal = GetBestSeal()),
                new Decorator(
                    ret => _currentSeal != PaladinSeal.None
                        && !StyxWoW.Me.HasMyAura(SealSpell(_currentSeal))
                        && SpellManager.CanCast(SealSpell(_currentSeal), StyxWoW.Me),
                    Spell.BuffSelf(SealSpell(_currentSeal), ret => !StyxWoW.Me.HasAura(SealSpell(_currentSeal)))
                )
            );
        }
    }
}
