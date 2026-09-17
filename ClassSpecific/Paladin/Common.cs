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
        Auto, Kings, Might, Wisdom, Sanctuary // WotLK: Wisdom separate (merged into Might in Cata), Sanctuary is Prot talent
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

        private static bool HasAnyBlessing(WoWPlayer p, string type)
        {
            return p.HasAura("Blessing of " + type) || p.HasAura("Greater Blessing of " + type);
        }

        private static List<WoWPlayer> GetBlessTargets()
        {
            var players = new List<WoWPlayer>();
            if (StyxWoW.Me.IsInRaid)
                players.AddRange(StyxWoW.Me.RaidMembers);
            else if (StyxWoW.Me.IsInParty)
                players.AddRange(StyxWoW.Me.PartyMembers);
            players.Add(StyxWoW.Me);
            return players;
        }

        private static Composite CreatePaladinBlessBehavior()
        {
            bool useGreater = SingularSettings.Instance.Paladin.UseGreaterBlessings;
            string sanctuarySpell = useGreater ? "Greater Blessing of Sanctuary" : "Blessing of Sanctuary";
            string wisdomSpell = useGreater ? "Greater Blessing of Wisdom" : "Blessing of Wisdom";
            string kingsSpell = useGreater ? "Greater Blessing of Kings" : "Blessing of Kings";
            string mightSpell = useGreater ? "Greater Blessing of Might" : "Blessing of Might";

            return
                new PrioritySelector(
                    new Throttle(2, Spell.Cast(sanctuarySpell,
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings != PaladinBlessings.Sanctuary &&
                                !(SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Auto &&
                                  TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin))
                                return false;
                            return GetBlessTargets().Any(
                                p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                     !HasAnyBlessing(p, "Sanctuary"));
                        })),
                    new Throttle(2, Spell.Cast(wisdomSpell,
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings != PaladinBlessings.Wisdom)
                                return false;
                            return GetBlessTargets().Any(
                                p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                     !HasAnyBlessing(p, "Wisdom"));
                        })),
                    new Throttle(2, Spell.Cast(kingsSpell,
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might ||
                                SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Wisdom ||
                                SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Sanctuary)
                                return false;
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Auto &&
                                TalentManager.CurrentSpec == TalentSpec.ProtectionPaladin &&
                                SpellManager.HasSpell("Blessing of Sanctuary"))
                                return false;
                            return GetBlessTargets().Any(
                                p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                     !HasAnyBlessing(p, "Kings") &&
                                     !p.HasAura("Mark of the Wild"));
                        })),
                    new Throttle(2, Spell.Cast(mightSpell,
                        ret => StyxWoW.Me,
                        ret =>
                        {
                            if (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Wisdom ||
                                SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Sanctuary)
                                return false;
                            return GetBlessTargets().Any(
                                p => p.DistanceSqr < 40 * 40 && p.IsAlive &&
                                     !HasAnyBlessing(p, "Might") &&
                                     (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Might ||
                                     (SingularSettings.Instance.Paladin.Blessings == PaladinBlessings.Auto &&
                                      !SpellManager.HasSpell("Blessing of Kings") &&
                                      !SpellManager.HasSpell("Blessing of Sanctuary")) ||
                                     ((HasAnyBlessing(p, "Kings") && !p.HasMyAura("Blessing of Kings") && !p.HasMyAura("Greater Blessing of Kings")) ||
                                       p.HasAura("Mark of the Wild"))));
                        }))
                    );
        }
    }
}
