using System;
using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        #region Normal Rotation

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateMarksmanHunterNormalPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                    ret => SingularSettings.Instance.Hunter.UseDisengage && StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                new Decorator(
                    ret => StyxWoW.Me.IsCasting && StyxWoW.Me.CastingSpell.Name == "Steady Shot",
                    new Action(ret => DoubleSteadyCast = true)),
                Spell.WaitForCast(true),

                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),

                Spell.Cast("Concussive Shot", ret => StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid),
                Spell.Buff("Hunter's Mark"),
                // WotLK QC: Dragonhawk (L74) replaces Hawk  try it first, fall back to Hawk for <74
                Spell.BuffSelf("Aspect of the Dragonhawk"),
                Spell.BuffSelf("Aspect of the Hawk", ret => !SpellManager.HasSpell("Aspect of the Dragonhawk")),
                // Defensive Stuff
                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),
                Common.CreateHunterTrapOnAddBehavior("Freezing Trap"),
                Spell.Cast("Mend Pet",
                    ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                    (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                // Cooldowns only when there are multiple mobs on normal rotation
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2,
                    new PrioritySelector(
                        Spell.BuffSelf("Readiness",
                            ret => !StyxWoW.Me.HasAura("Rapid Fire") && SpellManager.HasSpell("Rapid Fire") &&
                                   SpellManager.Spells["Rapid Fire"].CooldownTimeLeft.TotalSeconds > 5),
                        Spell.BuffSelf("Rapid Fire",
                            ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                                   !PetManager.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                                   !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)))),

                // Rotation
                Spell.Buff("Serpent Sting", true),
                Spell.Cast("Chimera Shot"),
                Spell.Cast("Steady Shot", ret => DoubleSteadyCast),
                Spell.Cast("Kill Shot"),
                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Improved Steady Shot")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.ManaPercent > 40),
                Spell.Cast("Steady Shot"),

                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateMarksmanHunterPvPPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(false),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage", ret => StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                new Decorator(
                    ret => StyxWoW.Me.IsCasting && StyxWoW.Me.CastingSpell.Name == "Steady Shot",
                    new Action(ret => DoubleSteadyCast = true)),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),

                Spell.Cast("Concussive Shot", ret => StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid),
                Spell.Buff("Hunter's Mark"),
                // WotLK QC: Dragonhawk (L74) replaces Hawk  try it first, fall back to Hawk for <74
                Spell.BuffSelf("Aspect of the Dragonhawk"),
                Spell.BuffSelf("Aspect of the Hawk", ret => !SpellManager.HasSpell("Aspect of the Dragonhawk")),
                // Defensive Stuff
                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),

                Common.CreateHunterTrapOnAddBehavior("Freezing Trap"),

                Spell.Cast("Mend Pet",
                    ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                    (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                // Cooldowns
                Spell.BuffSelf("Readiness",
                    ret => !StyxWoW.Me.HasAura("Rapid Fire") && SpellManager.HasSpell("Rapid Fire") &&
                           SpellManager.Spells["Rapid Fire"].Cooldown),
                Spell.BuffSelf("Rapid Fire",
                    ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                           !PetManager.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                           !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)),

                // Rotation (WotLK priority: Kill Shot ? Kill Command ? Serpent Sting ? Explosive Trap ? Chimera Shot ? Aimed Shot ? Arcane Shot ? Steady Shot)
                Spell.Cast("Kill Shot"),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Wing Clip"),
                Spell.Cast("Scatter Shot", ret => StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Spell.Cast("Raptor Strike"),
                Spell.Buff("Serpent Sting", true),
                Common.CreateHunterTrapBehavior("Explosive Trap", false),
                Spell.Cast("Chimera Shot"),
                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Improved Steady Shot")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.ManaPercent > 40),
                Spell.Cast("Silencing Shot"),
                Spell.Cast("Steady Shot", ret => DoubleSteadyCast),
                Spell.CastOnGround("Flare", ret => StyxWoW.Me.Location),
                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),
                Spell.Cast("Steady Shot"),

                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.MarksmanshipHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateMarksmanHunterInstancePullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                    ret => SingularSettings.Instance.Hunter.UseDisengage && StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                new Decorator(
                    ret => StyxWoW.Me.IsCasting && StyxWoW.Me.CastingSpell.Name == "Steady Shot",
                    new Action(ret => DoubleSteadyCast = true)),

                Spell.WaitForCast(true),

                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),

                Spell.Buff("Hunter's Mark"),
                // WotLK QC: Dragonhawk (L74) replaces Hawk  try it first, fall back to Hawk for <74
                Spell.BuffSelf("Aspect of the Dragonhawk"),
                Spell.BuffSelf("Aspect of the Hawk", ret => !SpellManager.HasSpell("Aspect of the Dragonhawk")),

                Spell.Cast("Mend Pet",
                    ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                    (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                // Cooldowns
                Spell.BuffSelf("Readiness", 
                    ret => !StyxWoW.Me.HasAura("Rapid Fire") && SpellManager.HasSpell("Rapid Fire") && 
                           SpellManager.Spells["Rapid Fire"].Cooldown),
                Spell.BuffSelf("Rapid Fire",
                    ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                           !PetManager.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                           !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Common.CreateHunterTrapBehavior("Explosive Trap"),
                        Spell.Cast("Multi-Shot"),
                        Spell.Cast("Steady Shot"),
                        Movement.CreateMoveToTargetBehavior(true, 32f)
                        )
                    ),

                // WotLK QC: Removed Cata "Careful Aim" health-threshold rotation (Cata 4.0.1)
                // In WotLK, Careful Aim simply increases ranged AP by 33/66/100% of Intellect—no target HP mechanic
                /* Cata Careful Aim block removed:
                new Decorator(
                    ret => TalentManager.GetCount(2, 6) > 0 && StyxWoW.Me.CurrentTarget.HealthPercent > 90,
                    new PrioritySelector(
                        new Decorator(
                            ret => StyxWoW.Me.IsMoving,
                            new PrioritySelector(
                                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Improved Steady Shot")),
                                Spell.Buff("Serpent Sting", true),
                                Spell.Cast("Chimera Shot")
                                )),
                        Spell.Cast("Aimed Shot"),
                        Spell.Cast("Steady Shot"),
                        Movement.CreateMoveToTargetBehavior(true, 32f)
                        )),
                */

                // Rotation (WotLK priority: Kill Shot → Kill Command → Serpent Sting → Explosive Trap → Chimera Shot → Aimed Shot → Arcane Shot → Steady Shot)
                Spell.Cast("Kill Shot"),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Serpent Sting", true),
                Common.CreateHunterTrapBehavior("Explosive Trap", false),
                Spell.Cast("Chimera Shot"),
                Spell.Cast("Aimed Shot", ret => StyxWoW.Me.HasAura("Improved Steady Shot")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.ManaPercent > 40),
                Spell.Cast("Silencing Shot"),
                Spell.Cast("Steady Shot", ret => DoubleSteadyCast),
                Spell.Cast("Steady Shot"),
                Movement.CreateMoveToTargetBehavior(true, 32f)
                );
        }

        private static int _castId;
        private static int _steadyCount;
        private static bool _doubleSteadyCast;
        private static bool DoubleSteadyCast
        {
            get
            {
                if (_steadyCount > 1)
                {
                    _castId = 0;
                    _steadyCount = 0;
                    _doubleSteadyCast = false;
                    return _doubleSteadyCast;
                }
                
                return _doubleSteadyCast;
            }
            set 
            {
                if (_doubleSteadyCast && StyxWoW.Me.CurrentCastId == _castId)
                    return;

                _castId = StyxWoW.Me.CurrentCastId;
                _steadyCount++;
                _doubleSteadyCast = value;
            }
        }

        #endregion
    }
}
