using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular.ClassSpecific.Warrior
{
    public class Lowbie
    {
        // WotLK compatibility: RagePercent property doesn't exist, so calculate it manually
        private static double RagePercent { get { return (StyxWoW.Me.CurrentRage / (double)StyxWoW.Me.MaxRage) * 100; } }

        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateLowbieWarriorCombat()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS Check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Offensive racials / buffs (follow Singular patterns)
                Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury")),
                Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking")),
                Spell.BuffSelf("Lifeblood", ret => SpellManager.HasSpell("Lifeblood")),
                // Party/self attack power buff (avoid overwriting other class buffs)
                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                // Heal
                Spell.Cast("Victory Rush"),
                //rend
                Spell.Buff("Rend"),
                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 2,
                    new PrioritySelector(
                        Spell.Cast("Victory Rush"),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Heroic Strike"))),
                // DPS
                Spell.Cast("Heroic Strike"),
                Spell.Cast("Thunder Clap", ret => RagePercent > 50),
                //move to melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateLowbieWarriorPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // LOS
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                // charge
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance < 25),
                // Offensive racials / buffs on pull
                Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury")),
                Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking")),
                Spell.BuffSelf("Lifeblood", ret => SpellManager.HasSpell("Lifeblood")),
                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Roar of Courage", "Strength of Earth Totem", "Battle Shout")),
                Spell.Cast("Throw", ret => StyxWoW.Me.CurrentTarget.IsFlying && Item.RangedIsType(WoWItemWeaponClass.Thrown)), Spell.Cast(
                    "Shoot",
                    ret =>
                    StyxWoW.Me.CurrentTarget.IsFlying && (Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun))),
                // move to melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }
    }
}
