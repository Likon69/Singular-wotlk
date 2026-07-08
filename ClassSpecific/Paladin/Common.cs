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
    }
}
