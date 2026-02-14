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
                            Spell.BuffSelf("Seal of Vengeance", ret => !SpellManager.HasSpell("Seal of Corruption")),
                            Spell.BuffSelf("Seal of Corruption"),
                            Spell.BuffSelf("Seal of Righteousness", ret => !SpellManager.HasSpell("Seal of Vengeance") && !SpellManager.HasSpell("Seal of Corruption"))
                            )),
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
                    );
        }

        private static Composite CreatePaladinBlessBehavior()
        {
            // TODO WotLK: Add Blessing of Wisdom support for mana users (healers/casters).
            // In WotLK, Wisdom is a separate blessing from Might (merged in Cata 4.0.1).
            // Currently only Kings+Might are supported.
            return
                new PrioritySelector(
                    Spell.Cast("Blessing of Kings",
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might)
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
                        }),
                    Spell.Cast("Blessing of Might",
                        ret => StyxWoW.Me,
                        ret =>
                        {
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
                        })
                    );
        }
    }
}
