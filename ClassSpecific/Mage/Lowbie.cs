using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                // Racial buffs
                Spell.BuffSelf("Blood Fury", ret => SpellManager.HasSpell("Blood Fury")),
                Spell.BuffSelf("Berserking", ret => SpellManager.HasSpell("Berserking")),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Common.CreateMagePolymorphOnAddBehavior(),

                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),
                Spell.Cast("Fire Blast", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 10),
                // Note: "Arcane Missiles!" proc aura doesn't exist in WotLK - Arcane Missiles is always castable
                Spell.Cast("Arcane Missiles"),
                Spell.Cast("Fireball", ret => !SpellManager.HasSpell("Frostbolt")),
                Spell.Cast("Frostbolt"),
                Movement.CreateMoveToTargetBehavior(true, 25f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateLowbieMageRest()
        {
            return Rest.CreateDefaultRestBehaviour();
        }
    }
}
