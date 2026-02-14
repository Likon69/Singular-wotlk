using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Fire
    {
        #region Normal Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFireMageNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),
                new Decorator (ret => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire),
                    Spell.Cast("Frostfire Bolt")),
                Spell.Cast("Pyroblast"),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFireMageNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation",
                    ret => StyxWoW.Me.ManaPercent < 30 || (TalentManager.HasGlyph("Evocation") && StyxWoW.Me.HealthPercent < 50)),
                Spell.BuffSelf("Fire Ward", ret => StyxWoW.Me.HealthPercent <= 80),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 60),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Mirror Image")
                        )),
                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),

                // Rotation
                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) &&
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8 * 8),

                // WotLK: Fire Blast as finisher or while moving (Impact is passive in WotLK, no player buff)
                Spell.Cast("Fire Blast",
                    ret => (StyxWoW.Me.IsMoving || StyxWoW.Me.CurrentTarget.HealthPercent < 8) && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),

                new Decorator(
                    ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                    new PrioritySelector(
                        Spell.BuffSelf("Frost Nova",
                            ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                            u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") &&
                                            !u.HasAura("Frost Nova") && !u.Stunned))
                        )),

                Common.CreateMagePolymorphOnAddBehavior(),
                // Rotation
                Spell.Cast("Frostfire Bolt", ret => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),
                // WotLK: debuff is "Improved Scorch" not "Critical Mass"
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Improved Scorch", true).TotalSeconds < 1 && SpellManager.HasSpell("Improved Scorch")),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateFireMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                Spell.BuffSelf("Blink", ret => StyxWoW.Me.IsStunned() || StyxWoW.Me.IsRooted()),
                Spell.BuffSelf("Mana Shield", ret => StyxWoW.Me.HealthPercent <= 75),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),
                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),
                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Fire Ward", ret => StyxWoW.Me.HealthPercent <= 75),

                Spell.Cast("Dragon's Breath",
                    ret => StyxWoW.Me.IsSafelyFacing(StyxWoW.Me.CurrentTarget, 90) &&
                           StyxWoW.Me.CurrentTarget.DistanceSqr <= 8 * 8),

                Spell.Cast("Fire Blast",
                    ret => StyxWoW.Me.IsMoving || StyxWoW.Me.CurrentTarget.HealthPercent < 8),
                // Rotation
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Improved Scorch", true).TotalSeconds < 1 && SpellManager.HasSpell("Improved Scorch")),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateFireMageInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                // Defensive stuff
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 20 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                // Cooldowns
                Spell.BuffSelf("Evocation", ret => StyxWoW.Me.ManaPercent < 30),
                Spell.BuffSelf("Mirror Image"),
                Spell.BuffSelf("Fire Ward", ret => StyxWoW.Me.HealthPercent <= 75),

                Common.CreateUseManaGemBehavior(ret => StyxWoW.Me.ManaPercent < 80),
                // AoE comes first
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Spell.Cast("Fire Blast",
                            ret => StyxWoW.Me.IsMoving || StyxWoW.Me.CurrentTarget.HealthPercent < 8),
                        // WotLK: Blast Wave is PBAoE centered on caster, not ground-targeted
                        Spell.Cast("Blast Wave"),
                        Spell.Cast("Dragon's Breath",
                            ret => Clusters.GetClusterCount(StyxWoW.Me.CurrentTarget,
                                                            Unit.NearbyUnitsInCombatWithMe,
                                                            ClusterType.Cone, 15f) >= 3),
                        Spell.CastOnGround("Flamestrike",
                            ret => Clusters.GetBestUnitForCluster(Unit.NearbyUnitsInCombatWithMe, ClusterType.Radius, 8f).Location,
                            ret => !ObjectManager.GetObjectsOfType<WoWDynamicObject>().Any(o =>
                                        o.CasterGuid == StyxWoW.Me.Guid && o.Spell.Name == "Flamestrike" &&
                                        o.Location.Distance(
                                            Clusters.GetBestUnitForCluster(Unit.NearbyUnitsInCombatWithMe, ClusterType.Radius, 8f).Location) < o.Radius))
                        )),

                // Rotation
                Spell.Cast("Frostfire Bolt",ret => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire)),
                Spell.Cast("Scorch", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Improved Scorch", true).TotalSeconds < 1 && SpellManager.HasSpell("Improved Scorch")),
                Spell.Cast("Pyroblast", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Hot Streak")),
                Spell.Buff("Living Bomb", true),
                Spell.Cast("Fireball"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
