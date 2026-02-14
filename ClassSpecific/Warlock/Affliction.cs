using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic.Combat;

namespace Singular.ClassSpecific.Warlock
{
    public class Affliction
    {
        #region Common

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        [Priority(1)]
        public static Composite CreateAfflictionWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(false),
                Pet.CreateSummonPet("Felhunter"),
                new Decorator(
                    ret => !SpellManager.HasSpell("Summon Felhunter"),
                    Pet.CreateSummonPet("Voidwalker"))
                );
        }

        #endregion

        #region Normal Rotation

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Normal)]
        public static Composite CreateAfflictionWarlockNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Buff("Unstable Affliction", true),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateAfflictionWarlockNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Cooldowns
                Spell.BuffSelf("Soulshatter", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet)),
                Spell.Cast("Death Coil", ret => StyxWoW.Me.HealthPercent <= 70),

                // AoE rotation
                Spell.BuffSelf("Shadowflame",
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10 && StyxWoW.Me.IsSafelyFacing(u, 90)) >= 3),

                Spell.BuffSelf("Howl of Terror", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= 3),
                Spell.Buff("Fear", ret => Targeting.Instance.TargetList.ElementAtOrDefault(1), ret => !StyxWoW.Me.CurrentTarget.HasAura("Fear")),
                Spell.Buff("Fear", ret => StyxWoW.Me.HealthPercent < 80),

                // Single target rotation
                // WotLK: Only one curse per target — use Curse of Agony (Affliction DPS curse), skip CoE to avoid ping-pong
                Spell.Buff("Haunt", true),
                Spell.Buff("Curse of Agony", true),
                Spell.Buff("Corruption", true),
                Spell.Buff("Unstable Affliction", true, ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Unstable Affliction", true).TotalSeconds < 3, ""),
                // WotLK QC: Removed Soulburn aura check (Cata 4.0.1 — does not exist in WotLK)
                Spell.Cast("Drain Life", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Drain Soul", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 25),
                Spell.Cast("Shadow Bolt"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateAfflictionWarlockPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Cooldowns
                Spell.BuffSelf("Soulshatter", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet)),
                Spell.Cast("Death Coil", ret => StyxWoW.Me.HealthPercent <= 70),

                // AoE rotation
                Spell.BuffSelf("Shadowflame",
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10 && StyxWoW.Me.IsSafelyFacing(u, 90)) >= 3),

                Spell.BuffSelf("Howl of Terror", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) >= 3),
                // Dimishing returns fucks Fear up. Avoid using it until a proper DR logic.
                //Spell.Buff("Fear", ret => Targeting.Instance.TargetList.ElementAtOrDefault(1)),

                // Single target rotation
                // WotLK: Only one curse per target — use Curse of Agony in PvP, skip CoE to avoid ping-pong
                Spell.Buff("Haunt", true),
                Spell.Buff("Curse of Agony", true),
                Spell.Buff("Corruption", true),
                Spell.Buff("Unstable Affliction", true, ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Unstable Affliction", true).TotalSeconds < 3, ""),
                // WotLK QC: Removed Soulburn aura check (Cata 4.0.1 — does not exist in WotLK)
                Spell.Cast("Drain Life", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Drain Soul", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 25),
                Spell.Cast("Shadow Bolt"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateAfflictionWarlockInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Cooldowns
                Spell.BuffSelf("Soulshatter", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet)),
                Spell.Cast("Death Coil", ret => StyxWoW.Me.HealthPercent <= 70),

                // AoE rotation
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet || u.IsTargetingMyPartyMember || u.IsTargetingMyRaidMember) >= 3,
                    new PrioritySelector(
                        ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => 
                                    (u.IsTargetingMeOrPet || u.IsTargetingMyPartyMember || u.IsTargetingMyRaidMember) &&
                                    !u.HasMyAura("Seed of Corruption") && u.InLineOfSpellSight),
                        Spell.BuffSelf("Shadowflame", 
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10 && StyxWoW.Me.IsSafelyFacing(u, 90)) >= 3),
                        Spell.Buff("Seed of Corruption", true, ret => (WoWUnit)ret)
                        )),

                // Single target rotation
                // WotLK: Only one curse per target — CoE for bosses (raid debuff), CoA for trash (DPS)
                Spell.Buff("Curse of the Elements", ret => StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.Buff("Haunt", true),
                Spell.Buff("Curse of Agony", true, ret => !StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.Buff("Corruption", true),
                Spell.Buff("Unstable Affliction", true, ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Unstable Affliction", true).TotalSeconds < 3, ""),
                Spell.Cast("Drain Soul", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 25),
                Spell.Cast("Shadow Bolt"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}