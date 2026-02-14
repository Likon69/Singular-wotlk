using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Retribution
    {

        #region Properties & Fields

        // WotLK QC: Removed T13 (Dragon Soul, Cata 4.3) dead code — doesn't exist in WotLK, was never referenced.

        #endregion

        #region Heal
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinHeal()
        {
            return new PrioritySelector(
                //Spell.WaitForCast(),
                Spell.Heal("Holy Light", ret => StyxWoW.Me,
                           ret =>
                           !SpellManager.HasSpell("Flash of Light") &&
                           StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.RetributionHealHealth),
                Spell.Heal("Flash of Light", ret => StyxWoW.Me,
                           ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.RetributionHealHealth));
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinRest()
        {
            return new PrioritySelector( // use ooc heals if we have mana to
                new Decorator(ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                    CreateRetributionPaladinHeal()),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Redemption"));
        }
        #endregion

        #region Normal Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Heal)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateRetributionPaladinNormalPullAndCombat()
        {
            return new PrioritySelector(

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                    Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance") && (!StyxWoW.Me.HasAura("Horde Flag") || !StyxWoW.Me.HasAura("Alliance Flag"))),
                    Spell.BuffSelf("Divine Protection", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthRet),

                    //2	Let's keep up Insight instead of Truth for grinding.  Keep up Righteousness if we need to AoE.  
                     // WotLK: Seal of Vengeance (Alliance) / Seal of Corruption (Horde) for single target
                     Spell.BuffSelf("Seal of Vengeance", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4 && !SpellManager.HasSpell("Seal of Corruption")),
                     Spell.BuffSelf("Seal of Corruption", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4),
                    Spell.BuffSelf("Seal of Righteousness", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                    //7	Blow buffs seperatly.  No reason for stacking while grinding.
                    Spell.BuffSelf("Avenging Wrath", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 3),
                    Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Lifeblood", ret => SpellManager.HasSpell("Lifeblood") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),

                    //Exo is above HoW if we're fighting Undead / Demon
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War") && StyxWoW.Me.CurrentTarget.IsUndeadOrDemon()),
                    //Hammer of Wrath if target < 20% HP
                    Spell.Cast("Hammer of Wrath", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20), // WotLK: Sanctified Wrath does not unlock HoW above 20% (Cata-only)
                    //Exo if we have Art of War
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),

                    // In WotLK 3.3.5a, Paladins don't have Holy Power - use simpler rotation
                    Spell.Cast("Crusader Strike"),
                    Spell.Cast("Divine Storm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),
                    Spell.Cast("Judgement of Light"),
                    Spell.Cast("Holy Wrath", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),
                //consecration,not_flying=1,if=mana>16000
                    Spell.Cast("Consecration", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= SingularSettings.Instance.Paladin.ConsecrationCount && StyxWoW.Me.CurrentTarget.Distance <= 5),
                    // WotLK QC: Fixed CurrentMana → ManaPercent (setting is a percentage, not raw mana)
                    Spell.Cast("Divine Plea", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Paladin.DivinePleaMana && StyxWoW.Me.HealthPercent > 70),

                    // Move to melee is LAST. Period.
                    Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.Battlegrounds)]

        public static Composite CreateRetributionPaladinPvPPullAndCombat()
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                   // Defensive
                    Spell.BuffSelf("Hand of Freedom",
                    ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                           StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                    Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance") && (!StyxWoW.Me.HasAura("Horde Flag") || !StyxWoW.Me.HasAura("Alliance Flag"))),
                    Spell.BuffSelf("Divine Protection", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthRet),

                    //  Buffs
                    Spell.BuffSelf("Retribution Aura"),
                    Spell.BuffSelf("Seal of Vengeance", ret => StyxWoW.Me.CurrentTarget.Entry != 28781 && !StyxWoW.Me.CurrentTarget.HasAura("Horde Flag") && !StyxWoW.Me.CurrentTarget.HasAura("Alliance Flag") && !SpellManager.HasSpell("Seal of Corruption")),
                    Spell.BuffSelf("Seal of Corruption", ret => StyxWoW.Me.CurrentTarget.Entry != 28781 && !StyxWoW.Me.CurrentTarget.HasAura("Horde Flag") && !StyxWoW.Me.CurrentTarget.HasAura("Alliance Flag")),
                    Spell.BuffSelf("Seal of Justice", ret => StyxWoW.Me.CurrentTarget.Entry == 28781 || StyxWoW.Me.CurrentTarget.HasAura("Horde Flag") || StyxWoW.Me.CurrentTarget.HasAura("Alliance Flag")),

                    Spell.BuffSelf("Avenging Wrath", ret => StyxWoW.Me.CurrentTarget.Distance <= 8),
                    Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Lifeblood", ret => SpellManager.HasSpell("Lifeblood") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),

                    //Hammer of Wrath if target < 20% HP
                    Spell.Cast("Hammer of Wrath", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20), // WotLK: Sanctified Wrath does not unlock HoW above 20% (Cata-only)
                    //Exo if we have Art of War
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),

                    // WotLK: Holy Power doesn't exist - simplified rotation
                    Spell.Cast("Crusader Strike", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4 || !SpellManager.HasSpell("Divine Storm")),
                    Spell.Cast("Divine Storm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),
                    Spell.Cast("Judgement of Light"),
                    Spell.Cast("Holy Wrath"),
                    Spell.Cast("Consecration", ret => StyxWoW.Me.CurrentTarget.Distance <= Spell.MeleeRange && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= SingularSettings.Instance.Paladin.ConsecrationCount),
                    Spell.Cast("Divine Plea", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Paladin.DivinePleaMana && StyxWoW.Me.HealthPercent > 70),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateRetributionPaladinInstancePullAndCombat()
        {
            return new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                    // Defensive
                    Spell.BuffSelf("Hand of Freedom",
                        ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                                StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                               WoWSpellMechanic.Disoriented,
                                                               WoWSpellMechanic.Frozen,
                                                               WoWSpellMechanic.Incapacitated,
                                                               WoWSpellMechanic.Rooted,
                                                               WoWSpellMechanic.Slowed,
                                                               WoWSpellMechanic.Snared)),

                    Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance") && (!StyxWoW.Me.HasAura("Horde Flag") || !StyxWoW.Me.HasAura("Alliance Flag"))),
                    Spell.BuffSelf("Divine Protection", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthRet),

                    //2	seal_of_truth (WotLK: Seal of Vengeance/Corruption)
                    Spell.BuffSelf("Seal of Vengeance", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4 && !SpellManager.HasSpell("Seal of Corruption")),
                    Spell.BuffSelf("Seal of Corruption", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4),
                    Spell.BuffSelf("Seal of Righteousness", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                    Spell.BuffSelf("Avenging Wrath", ret => StyxWoW.Me.CurrentTarget.IsBoss()),
                    Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),
                    Spell.BuffSelf("Lifeblood", ret => SpellManager.HasSpell("Lifeblood") && StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")),

                    //Exo is above HoW if we're fighting Undead / Demon
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War") && StyxWoW.Me.CurrentTarget.IsUndeadOrDemon()),
                    //Hammer of Wrath if target < 20% HP
                    Spell.Cast("Hammer of Wrath", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20), // WotLK: Sanctified Wrath does not unlock HoW above 20% (Cata-only)
                    //Exo is above HoW if we're fighting Undead / Demon
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),

                    //crusader_strike - simplified for WotLK (no Holy Power checks)
                    Spell.Cast("Crusader Strike", ret =>
                        Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 4 || !SpellManager.HasSpell("Divine Storm")),
                //Replace CS with DS during AoE
                    Spell.Cast("Divine Storm", ret =>
                        Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),
                //judgement - simplified for WotLK
                    Spell.Cast("Judgement of Light"),
                //holy_wrath
                    Spell.Cast("Holy Wrath"),
                //consecration,not_flying=1,if=mana>16000
                    Spell.Cast("Consecration", ret => StyxWoW.Me.CurrentTarget.Distance <= Spell.MeleeRange && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= SingularSettings.Instance.Paladin.ConsecrationCount),
                //wait,sec=0.1,if=cooldown.crusader_strike.remains<0.2&cooldown.crusader_strike.remains>0
                    Spell.Cast("Divine Plea", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Paladin.DivinePleaMana),

                    // Move to melee is LAST. Period.
                    Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        /*
        #region Normal Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateRetributionPaladinNormalPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Heals
                Spell.Heal("Holy Light", ret => StyxWoW.Me, ret => !SpellManager.HasSpell("Flash of Light") && StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.RetributionHealHealth),
                Spell.Heal("Flash of Light", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.RetributionHealHealth),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= SingularSettings.Instance.Paladin.ConsecrationCount,
                    new PrioritySelector(
                // Cooldowns
                        Spell.BuffSelf("Avenging Wrath"),
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation - simplified for WotLK (no Holy Power abilities)
                Spell.Cast("Hammer of Justice", ret => StyxWoW.Me.HealthPercent <= 40),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement of Light"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateRetributionPaladinPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                           StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),
                Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                // Cooldowns
                Spell.BuffSelf("Avenging Wrath"),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation - simplified for WotLK (no Holy Power abilities)
                Spell.Cast("Hammer of Justice", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 40),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement of Light"),
                Spell.BuffSelf("Holy Wrath"),
                Spell.BuffSelf("Consecration"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion


        #region Instance Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateRetributionPaladinInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Movement.CreateMoveBehindTargetBehavior(),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                           StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),
                Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                // Cooldowns
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                    Spell.BuffSelf("Avenging Wrath"))),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= SingularSettings.Instance.Paladin.ConsecrationCount,
                    new PrioritySelector(
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation - simplified for WotLK (no Holy Power abilities)
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement of Light"),
                Spell.BuffSelf("Holy Wrath"),
                Spell.BuffSelf("Consecration"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
         */
    }
}
