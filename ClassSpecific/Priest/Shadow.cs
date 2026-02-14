using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Priest
{
    public class Shadow
    {
        #region Normal Rotation

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Normal)]
        public static Composite CreateShadowPriestNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                // WotLK: Pre-shield if talented
                Spell.BuffSelf("Power Word: Shield", 
                    ret => SingularSettings.Instance.Priest.UseShieldPrePull && !StyxWoW.Me.HasAura("Weakened Soul")),
                
                // Shadow immune targets
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                
                // WotLK Opener (CRITICAL for Shadow Weaving stacks before SWP):
                // 1. Vampiric Touch (Shadow Weaving stack 1)
                Spell.Buff("Vampiric Touch", true),
                // 2. Devouring Plague (Shadow Weaving stack 2)
                Spell.Buff("Devouring Plague", true, 
                    ret => SingularSettings.Instance.Priest.DevouringPlagueFirst),
                // 3. Mind Blast (Shadow Weaving stack 3 + Replenishment proc)
                Spell.Cast("Mind Blast"),
                // 4. Mind Flay (Shadow Weaving stacks 4-5, interrupt after 2nd tick)
                Spell.Cast("Mind Flay"),
                // 5. Shadow Word: Pain LAST with 5 stacks (will snapshot Shadow Weaving for entire fight)
                Spell.Buff("Shadow Word: Pain", true),
                
                // Smite fallback for lowbies without Mind Blast
                Spell.Cast("Smite", ret => !SpellManager.HasSpell("Mind Blast")),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateShadowPriestNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Shadowform"),

                // Defensive
                Spell.BuffSelf("Power Word: Shield", 
                    ret => !StyxWoW.Me.HasAura("Weakened Soul") &&
                           StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest.ShieldHealthPercent),
                Spell.BuffSelf("Dispersion", ret => StyxWoW.Me.ManaPercent < SingularSettings.Instance.Priest.DispersionMana),
                Spell.BuffSelf("Psychic Scream", 
                    ret => SingularSettings.Instance.Priest.UsePsychicScream &&
                           Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= SingularSettings.Instance.Priest.PsychicScreamAddCount),
                
                // Emergency healing
                Spell.Heal("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Priest.ShadowFlashHealHealth),
                new Decorator(ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.Priest.DontHealPercent,
                    new PrioritySelector(
                        Spell.Heal("Flash Heal", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40)
                        )),
                
                // Shadow immune NPCs
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                // WotLK Shadow Priority System (DoT management + filler)
                // Priority 1: Shadow Word: Pain (lowest priority because refreshed by Mind Flay via Pain and Suffering talent)
                Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                
                // Priority 2: Vampiric Touch (15s duration, high priority, procs Replenishment with Mind Blast)
                Spell.Buff("Vampiric Touch", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                
                // Priority 3: Devouring Plague (24s duration, also instant cast for movement)
                Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.HealthPercent > 40),
                
                // Priority 4: Inner Focus + Mind Blast combo (DPS cooldown)
                new Decorator(
                    ret => SpellManager.HasSpell("Inner Focus") && SpellManager.CanCast("Inner Focus"),
                    new Sequence(
                        Spell.BuffSelf("Inner Focus"),
                        new Action(ret => System.Threading.Thread.Sleep(100)), // Small delay
                        Spell.Cast("Mind Blast")
                    )),
                
                // Priority 5: Mind Blast (on cooldown, procs Replenishment)
                Spell.Cast("Mind Blast"),
                
                // Priority 6: Shadow Word: Death (execute phase <25% HP OR while moving)
                Spell.Cast("Shadow Word: Death", 
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 25 || 
                           (StyxWoW.Me.IsMoving && !StyxWoW.Me.IsCasting)),
                
                // Priority 7: Devouring Plague while moving (instant cast with Improved DP talent adds upfront damage)
                Spell.Cast("Devouring Plague", 
                    ret => StyxWoW.Me.IsMoving && !StyxWoW.Me.IsCasting && 
                           !StyxWoW.Me.CurrentTarget.HasMyAura("Devouring Plague")),
                
                // Priority 8: Shadowfiend (use as DPS cooldown, early or before Bloodlust)
                // At max level, mana isn't an issue so use it for damage
                Spell.Cast("Shadowfiend", 
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent >= 60 &&
                           (StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Priest.ShadowfiendMana || 
                            StyxWoW.Me.CurrentTarget.Elite)),
                
                // Priority 9: Mind Flay (filler, refreshes SWP via Pain and Suffering)
                Spell.Cast("Mind Flay", ret => StyxWoW.Me.ManaPercent >= SingularSettings.Instance.Priest.MindFlayMana),
                
                // Fallback to wand if OOM
                Helpers.Common.CreateUseWand(ret => SingularSettings.Instance.Priest.UseWand),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateShadowPriestPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Spell.BuffSelf("Shadowform"),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Power Word: Shield", ret => !StyxWoW.Me.HasAura("Weakened Soul")),
                Spell.BuffSelf("Dispersion", ret => StyxWoW.Me.HealthPercent < 40),
                Spell.BuffSelf("Psychic Scream", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) >= 1),

                // WotLK PvP Priority (aggressive DoT pressure)
                Spell.Buff("Shadow Word: Pain", true),
                Spell.Buff("Vampiric Touch", true),
                Spell.Buff("Devouring Plague", true),
                
                // Inner Focus + Mind Blast combo for burst
                new Decorator(
                    ret => SpellManager.HasSpell("Inner Focus") && SpellManager.CanCast("Inner Focus"),
                    new Sequence(
                        Spell.BuffSelf("Inner Focus"),
                        new Action(ret => System.Threading.Thread.Sleep(100)),
                        Spell.Cast("Mind Blast")
                    )),
                    
                Spell.Cast("Mind Blast"),
                Spell.Cast("Shadow Word: Death", 
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 25 || 
                           (StyxWoW.Me.IsMoving && !StyxWoW.Me.IsCasting)),
                Spell.Cast("Shadowfiend"),
                Spell.Cast("Mind Flay"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.Instances)]
        public static Composite CreateShadowPriestRest()
        {
            return new PrioritySelector(
                Spell.Resurrect("Resurrection"),
                Rest.CreateDefaultRestBehaviour()
                );
        }

        [Class(WoWClass.Priest)]
        [Spec(TalentSpec.ShadowPriest)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateShadowPriestInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Shadowform"),

                // Use Fade to drop aggro
                Spell.Cast("Fade", ret => (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) && Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0),

                // Shadow immune NPCs
                Spell.Cast("Holy Fire", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),
                Spell.Cast("Smite", ctx => StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Shadow)),

                // WotLK AoE: Mind Sear on 3+ targets (2-5 targets section from guide)
                // Target tank's target for best positioning
                new PrioritySelector(
                    ret => Group.Tanks.FirstOrDefault(t => 
                                Clusters.GetClusterCount(t, Unit.NearbyUnfriendlyUnits,ClusterType.Radius, 10f) >= 3),
                    new Decorator(
                        ret => ret != null,
                        Spell.Cast("Mind Sear", ret => (WoWUnit)ret))),
                        
                // Fallback AoE for guild raids without tanks
                new Decorator(
                    ret => !Group.Tanks.Any() && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    Spell.Cast("Mind Sear")),

                // WotLK Single Target Boss Rotation (Priority System)
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                        // Priority: SWP -> VT -> DP -> IF+MB -> MB -> SW:D -> Shadowfiend -> MF
                        Spell.Buff("Shadow Word: Pain", true),
                        Spell.Buff("Vampiric Touch", true),
                        Spell.Buff("Devouring Plague", true),
                        
                        // Inner Focus + Mind Blast combo for maximum DPS
                        new Decorator(
                            ret => SpellManager.HasSpell("Inner Focus") && SpellManager.CanCast("Inner Focus"),
                            new Sequence(
                                Spell.BuffSelf("Inner Focus"),
                                new Action(ret => System.Threading.Thread.Sleep(100)),
                                Spell.Cast("Mind Blast")
                            )),
                            
                        Spell.Cast("Mind Blast"),
                        Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 25),
                        
                        // Shadowfiend early or before Bloodlust for maximum damage
                        Spell.Cast("Shadowfiend", ret => StyxWoW.Me.CurrentTarget.HealthPercent >= 60),
                        
                        Spell.Cast("Mind Flay"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )),

                // WotLK Trash Rotation (apply DoTs only if target will live 12+ seconds)
                // For fast dying trash, just Mind Blast + SW:D + Mind Flay
                Spell.Buff("Shadow Word: Pain", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.MaxHealth > 50000),
                Spell.Buff("Vampiric Touch", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.MaxHealth > 50000),
                Spell.Buff("Devouring Plague", true, ret => StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.MaxHealth > 50000),
                Spell.Cast("Mind Blast"),
                Spell.Cast("Shadow Word: Death", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 25),
                Spell.Cast("Mind Flay"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
