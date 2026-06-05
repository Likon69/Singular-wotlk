using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        # region Properties & Fields

        // WotLK Eclipse: Solar (48517) empowers Wrath; Lunar (48518) empowers Starfire.
        private const int EclipseSolarSpellId = 48517;
        private const int EclipseLunarSpellId = 48518;

        private static int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private static bool HasEclipseSolar =>
            StyxWoW.Me.HasAura(EclipseSolarSpellId) ||
            StyxWoW.Me.HasAura("Eclipse (Solar)") ||
            StyxWoW.Me.ActiveAuras.ContainsKey("Eclipse (Solar)");

        private static bool HasEclipseLunar =>
            StyxWoW.Me.HasAura(EclipseLunarSpellId) ||
            StyxWoW.Me.HasAura("Eclipse (Lunar)") ||
            StyxWoW.Me.ActiveAuras.ContainsKey("Eclipse (Lunar)");

        private static bool CurrentTargetHasDots =>
            StyxWoW.Me.CurrentTarget != null &&
            StyxWoW.Me.CurrentTarget.HasMyAura("Moonfire") &&
            StyxWoW.Me.CurrentTarget.HasMyAura("Insect Swarm");

        static WoWUnit BestAoeTarget
        {
            get { return Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled()), ClusterType.Radius, 8f); }
        }

        private static Composite CreateBalanceSingleTargetRotation()
        {
            return new PrioritySelector(
                Spell.Cast("Moonfire",
                    ret => StyxWoW.Me.CurrentTarget != null &&
                           (!StyxWoW.Me.CurrentTarget.HasMyAura("Moonfire") ||
                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Moonfire", true).TotalSeconds < 3 ||
                            (StyxWoW.Me.IsMoving && !StyxWoW.Me.CurrentTarget.HasMyAura("Moonfire")))),

                Spell.Cast("Insect Swarm",
                    ret => StyxWoW.Me.CurrentTarget != null &&
                           (!StyxWoW.Me.CurrentTarget.HasMyAura("Insect Swarm") ||
                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Insect Swarm", true).TotalSeconds < 3)),

                Spell.Cast("Typhoon",
                    ret => SpellManager.HasSpell("Typhoon") &&
                           CurrentTargetHasDots &&
                           StyxWoW.Me.CurrentTarget != null &&
                           StyxWoW.Me.CurrentTarget.Distance <= 33),

                // Eclipse proc: spam empowered spell until the buff expires.
                Spell.Cast("Starfire", ret => HasEclipseLunar),
                // Solar proc or standard filler (Wrath spam to fish for Eclipse).
                Spell.Cast("Wrath", ret => !HasEclipseLunar)
                );
        }

        #endregion

        #region Normal Rotation

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        [Context(WoWContext.Normal)]
        public static Composite CreateBalanceDruidNormalCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Spell.WaitForCast(true),
                //Heals, will not heal if in a party or if disabled via setting
                Common.CreateNonRestoHeals(),

                // Re-enter Moonkin immediately after instant HoTs; defer only during cast-time heals (see Common.ShouldDeferBalanceForm).
                Spell.BuffSelf("Moonkin Form", ret => !Common.ShouldDeferBalanceForm()),

                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        // WotLK: Eclipse only buffs Wrath (Solar) or Starfire (Lunar) — not Starfall/Treant schools.
                        Spell.CastOnGround("Force of Nature", 
                            ret => StyxWoW.Me.CurrentTarget.Location),
                        Spell.Cast("Starfall", 
                            ret => StyxWoW.Me, 
                            ret => SingularSettings.Instance.Druid.UseStarfall),
                
                        Spell.Cast("Moonfire", 
                            ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => 
                                        u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Moonfire"))),
                        Spell.Cast("Insect Swarm", 
                            ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => 
                                        u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Insect Swarm")))
                        )),

                CreateBalanceSingleTargetRotation(),
                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateBalanceDruidPvPCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Spell.WaitForCast(true),

                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),

                Spell.BuffSelf("Moonkin Form", ret => !Common.ShouldDeferBalanceForm()),
                Spell.BuffSelf("Barkskin", 
                    ret => StyxWoW.Me.IsCrowdControlled() || StyxWoW.Me.HealthPercent < 40),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Spread MF/IS
                Spell.CastOnGround("Force of Nature",
                    ret => StyxWoW.Me.CurrentTarget.Location),
                Spell.Cast("Starfall",
                    ret => StyxWoW.Me,
                    ret => SingularSettings.Instance.Druid.UseStarfall),
                Spell.Buff("Faerie Fire", 
                    ret => StyxWoW.Me.CurrentTarget.Class == WoWClass.Rogue ||
                           StyxWoW.Me.CurrentTarget.Class == WoWClass.Druid),
                CreateBalanceSingleTargetRotation(),
                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        [Context(WoWContext.Instances)]
        public static Composite CreateBalanceDruidInstanceCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Spell.WaitForCast(true),

                //Inervate
                Spell.Buff("Innervate",
                    ret => (from raidMember in StyxWoW.Me.RaidMemberInfos
                                let player = raidMember.ToPlayer()
                                where player != null && raidMember.HasRole(WoWPartyMember.GroupRole.Healer) && player.ManaPercent <= 15
                                select player).FirstOrDefault()),

                Spell.BuffSelf("Innervate", 
                    ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),
                Spell.BuffSelf("Moonkin Form", ret => !Common.ShouldDeferBalanceForm()),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // WotLK QC: Removed Eclipse gates — WotLK Eclipse doesn't buff Starfall/Treant damage schools
                Spell.Cast("Starfall", 
                    ret => StyxWoW.Me, 
                    ret => SingularSettings.Instance.Druid.UseStarfall && StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.CastOnGround("Force of Nature", 
                    ret => StyxWoW.Me.CurrentTarget.Location, 
                    ret => StyxWoW.Me.CurrentTarget.IsBoss()),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        // WotLK QC: Removed Eclipse gates from AoE cooldowns (same root cause as single-target)
                        Spell.CastOnGround("Force of Nature",
                            ret => StyxWoW.Me.CurrentTarget.Location),
                        Spell.Cast("Starfall",
                            ret => StyxWoW.Me,
                            ret => SingularSettings.Instance.Druid.UseStarfall),

                        Spell.Cast("Moonfire",
                            ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => 
                                        u.Combat && !u.IsCrowdControlled() && !u.HasMyAura("Moonfire"))),
                        Spell.Cast("Insect Swarm",
                            ret => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => 
                                        u.Combat && !u.IsCrowdControlled() &&!u.HasMyAura("Insect Swarm")))
                        )),

                CreateBalanceSingleTargetRotation(),
                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        #endregion
    }
}
