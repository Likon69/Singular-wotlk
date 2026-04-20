using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonBehaviors.Actions;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Rogue
{
    class Assassination
    {
        #region Normal Rotation

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Normal)]
        public static Composite CreateAssaRogueNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => SingularSettings.Instance.Rogue.UseStealthOnPull && StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth", ret => SingularSettings.Instance.Rogue.UseStealthOnPull),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Cheap Shot", ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Throw", ret => Item.RangedIsType(WoWItemWeaponClass.Thrown)),
                        Spell.Cast("Shoot", ret => Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun)),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateAssaRogueNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Don't do anything if we casted vanish
                new Decorator(
                    ret => StyxWoW.Me.HasAura("Vanish"),
                    new ActionAlwaysSucceed()),

                // Forcer le déplacement si hors de portée melee (pattern Singular)
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget != null && 
                           StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange,
                    Movement.CreateMoveToMeleeBehavior(false)),

                // Raciales offensives
                Spell.BuffSelf("Blood Fury"),
                Spell.BuffSelf("Berserking"),
                Spell.BuffSelf("Lifeblood"),

                // Defensive
                Spell.BuffSelf("Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 2),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                Common.CreateRogueBlindOnAddBehavior(),

                Spell.BuffSelf("Vanish",
                    ret => StyxWoW.Me.HealthPercent < 20),

                Spell.Buff("Rupture", true, ret => StyxWoW.Me.ComboPoints >= 4 && StyxWoW.Me.CurrentTarget.Elite),
                Spell.BuffSelf("Slice and Dice",
                    ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3),
                // WotLK QC: Hunger for Blood — 51-point Assassination talent, removed in Cata. Must maintain on self.
                Spell.BuffSelf("Hunger for Blood",
                    ret => SpellManager.HasSpell("Hunger for Blood") &&
                           StyxWoW.Me.CurrentTarget != null &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") || StyxWoW.Me.CurrentTarget.HasMyAura("Garrote"))),
                Spell.BuffSelf("Cold Blood",
                    ret => StyxWoW.Me.ComboPoints >= 4 && StyxWoW.Me.CurrentTarget.HealthPercent >= 35 ||
                           StyxWoW.Me.ComboPoints == 5 || !SpellManager.HasSpell("Envenom")),

                Spell.Cast("Eviscerate",
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 40 && StyxWoW.Me.ComboPoints >= 2),
                Spell.Cast("Eviscerate",
                    ret => (StyxWoW.Me.CurrentTarget.HealthPercent <= 40 || !SpellManager.HasSpell("Envenom") || !StyxWoW.Me.CurrentTarget.Elite) && 
                           StyxWoW.Me.ComboPoints >= 4),

                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HealthPercent >= 35 && StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.Elite && StyxWoW.Me.CurrentTarget.HealthPercent < 35 && StyxWoW.Me.ComboPoints == 5),

                // WotLK QC: Removed Backstab sub-35% logic (Cata "Murderous Intent" talent doesn't exist in WotLK)
                // Assassination rogues always use Mutilate as their builder in WotLK
                Spell.Cast("Mutilate", ret => !StyxWoW.Me.HasAura("Cold Blood")),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateAssaRoguePvPPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => SingularSettings.Instance.Rogue.UseStealthOnPull && StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth", ret => SingularSettings.Instance.Rogue.UseStealthOnPull),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote", 
                    ret => StyxWoW.Me.CurrentTarget.MeIsBehind && StyxWoW.Me.CurrentTarget.PowerType == WoWPowerType.Mana),
                Spell.Cast("Cheap Shot", 
                    ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5 * 5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Throw", ret => Item.RangedIsType(WoWItemWeaponClass.Thrown)),
                        Spell.Cast("Shoot", ret => Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun)),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateAssaRoguePvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Evasion",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 1),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                // WotLK QC: Overkill = Assassination tree (tab 1), index 19. Tabs are 1-based in TalentManager. Original was correct.
                Spell.BuffSelf("Vanish",
                    ret => TalentManager.GetCount(1, 19) > 0 && StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") &&
                           StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Cast("Garrote",
                    ret => (StyxWoW.Me.HasAura("Vanish") || StyxWoW.Me.IsStealthed) &&
                           StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Buff("Rupture", true, ret => StyxWoW.Me.ComboPoints >= 4),
                Spell.BuffSelf("Slice and Dice",
                    ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3),
                // WotLK QC: Hunger for Blood — 51-point Assassination talent, removed in Cata. Must maintain on self.
                Spell.BuffSelf("Hunger for Blood",
                    ret => SpellManager.HasSpell("Hunger for Blood") &&
                           StyxWoW.Me.CurrentTarget != null &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") || StyxWoW.Me.CurrentTarget.HasMyAura("Garrote"))),
                Spell.BuffSelf("Cold Blood",
                    ret => StyxWoW.Me.ComboPoints >= 4 && StyxWoW.Me.CurrentTarget.HealthPercent >= 35 ||
                           StyxWoW.Me.ComboPoints == 5 || !SpellManager.HasSpell("Envenom")),
                Spell.Cast("Eviscerate",
                    ret => (StyxWoW.Me.CurrentTarget.HealthPercent <= 40 || !SpellManager.HasSpell("Envenom")) && StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Kidney Shot",
                    ret => StyxWoW.Me.ComboPoints >= 4 && !StyxWoW.Me.CurrentTarget.IsStunned()),
                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent >= 35 && StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35 && StyxWoW.Me.ComboPoints == 5),
                // QC3: Removed Murderous Intent (Cata-only) Backstab sub-35% logic — WotLK Assassination always uses Mutilate
                Spell.Cast("Mutilate", ret => !StyxWoW.Me.HasAura("Cold Blood")),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Instances)]
        public static Composite CreateAssaRogueInstancePull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Sprint", ret => SingularSettings.Instance.Rogue.UseStealthOnPull && StyxWoW.Me.IsMoving && StyxWoW.Me.HasAura("Stealth")),
                Spell.BuffSelf("Stealth", ret => SingularSettings.Instance.Rogue.UseStealthOnPull),
                // Garrote if we can, SS is kinda meh as an opener.
                Spell.Cast("Garrote", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Cheap Shot", ret => !SpellManager.HasSpell("Garrote") || !StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Ambush", ret => !SpellManager.HasSpell("Cheap Shot") && StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Mutilate", ret => !SpellManager.HasSpell("Cheap Shot") && !StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying || StyxWoW.Me.CurrentTarget.Distance2DSqr < 5*5 && Math.Abs(StyxWoW.Me.Z - StyxWoW.Me.CurrentTarget.Z) >= 5,
                    new PrioritySelector(
                        Spell.Cast("Throw", ret => Item.RangedIsType(WoWItemWeaponClass.Thrown)),
                        Spell.Cast("Shoot", ret => Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun)),
                        Spell.Cast("Stealth", ret => StyxWoW.Me.HasAura("Stealth"))
                        )),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Class(WoWClass.Rogue)]
        [Spec(TalentSpec.AssasinationRogue)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateAssaRogueInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Vanish"),
                    Helpers.Common.CreateAutoAttack(true)),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Evasion", 
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 6 * 6 && u.IsTargetingMeOrPet) >= 1),

                Spell.BuffSelf("Cloak of Shadows",
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && u.IsCasting) >= 1),

                // Agro management
                Spell.Cast(
                    "Tricks of the Trade", 
                    ret => Common.BestTricksTarget,
                    ret => SingularSettings.Instance.Rogue.UseTricksOfTheTrade),

                Spell.Cast("Feint", ret => StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 80),

                Movement.CreateMoveBehindTargetBehavior(),

                // WotLK QC: Overkill = Assassination tree (tab 1), index 19. Tabs are 1-based in TalentManager. Original was correct.
                Spell.BuffSelf("Vanish",
                    ret => TalentManager.GetCount(1, 19) >0 && StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") && 
                           StyxWoW.Me.HasAura("Slice and Dice")),
                Spell.Cast("Garrote", 
                    ret => (StyxWoW.Me.HasAura("Vanish") || StyxWoW.Me.IsStealthed) &&
                           StyxWoW.Me.CurrentTarget.MeIsBehind),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) >= 3,
                    // WotLK QC: Removed thrown weapon gate — Fan of Knives works with any ranged weapon in WotLK
                    Spell.BuffSelf("Fan of Knives")),

                Spell.Buff("Rupture", true, ret => StyxWoW.Me.ComboPoints >= 4),
                Spell.BuffSelf("Slice and Dice", 
                    ret => StyxWoW.Me.RawComboPoints > 0 && StyxWoW.Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds < 3),
                // WotLK QC: Hunger for Blood — 51-point Assassination talent, removed in Cata. Must maintain on self.
                Spell.BuffSelf("Hunger for Blood",
                    ret => SpellManager.HasSpell("Hunger for Blood") &&
                           StyxWoW.Me.CurrentTarget != null &&
                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rupture") || StyxWoW.Me.CurrentTarget.HasMyAura("Garrote"))),
                Spell.BuffSelf("Cold Blood",
                    ret => StyxWoW.Me.ComboPoints >= 4 && StyxWoW.Me.CurrentTarget.HealthPercent >= 35 ||
                           StyxWoW.Me.ComboPoints == 5 || !SpellManager.HasSpell("Envenom")),
                Spell.Cast("Eviscerate", 
                    ret => (!StyxWoW.Me.CurrentTarget.Elite || !SpellManager.HasSpell("Envenom")) && StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent >= 35 && StyxWoW.Me.ComboPoints >= 4),
                Spell.Cast("Envenom",
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35 && StyxWoW.Me.ComboPoints == 5),
                // WotLK QC: Removed Backstab sub-35% (Cata Murderous Intent). Assassination always uses Mutilate.
                Spell.Cast("Mutilate", ret => !StyxWoW.Me.HasAura("Cold Blood")),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}
