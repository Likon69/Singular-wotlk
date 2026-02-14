using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class BeastMaster
    {
        #region Normal Rotation
        
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateBeastMasterHunterNormalPullAndCombat()
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
                Spell.Cast( "Mend Pet",
                    ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                    (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))), 
              
                // Cooldowns only when there are multiple mobs on normal rotation
                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2,
                    new PrioritySelector(
                        Spell.BuffSelf("Rapid Fire",
                            ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                                   !StyxWoW.Me.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                                   !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)),

                        Spell.BuffSelf("Bestial Wrath",
                            ret => StyxWoW.Me.GotAlivePet && (!SpellManager.HasSpell("Kill Command") || 
                                   SpellManager.Spells["Kill Command"].CooldownTimeLeft.TotalSeconds < 2)))),

                // WotLK BM Rotation: Kill Command -> Kill Shot -> Explosive Trap -> Serpent Sting -> Multi-Shot -> Arcane Shot -> Steady Shot
                Spell.Cast("Raptor Strike"),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Cast("Kill Shot"),
                Common.CreateHunterTrapBehavior("Explosive Trap", false),
                Spell.Buff("Serpent Sting", true),
                Spell.Cast("Multi-Shot", ret => StyxWoW.Me.ManaPercent > 50),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.ManaPercent > 40),
                Spell.Cast("Steady Shot"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateBeastMasterHunterPvPPullAndCombat()
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
                Spell.BuffSelf("Rapid Fire",
                    ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                           !StyxWoW.Me.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                           !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)),

                Spell.BuffSelf("Bestial Wrath",
                    ret => StyxWoW.Me.GotAlivePet && (!SpellManager.HasSpell("Kill Command") || 
                            SpellManager.Spells["Kill Command"].CooldownTimeLeft.TotalSeconds < 2)),

                // WotLK BM PvP Rotation
                Spell.Buff("Wing Clip"),
                Spell.Cast("Scatter Shot", ret => StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Spell.Cast("Raptor Strike"),
                Spell.Cast("Kill Command", ret => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Cast("Kill Shot"),
                Common.CreateHunterTrapBehavior("Explosive Trap", false),
                Spell.Buff("Serpent Sting", true),
                Spell.Cast("Multi-Shot", ret => StyxWoW.Me.ManaPercent > 50),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.ManaPercent > 40),
                Spell.CastOnGround("Flare", ret => StyxWoW.Me.Location),
                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),
                Spell.Cast("Steady Shot"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation
        
        [Class(WoWClass.Hunter)]
        [Spec(TalentSpec.BeastMasteryHunter)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateBeastMasterHunterInstancePullAndCombat()
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
                Spell.BuffSelf("Rapid Fire",
                    ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                           !StyxWoW.Me.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                           !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "The Beast Within" /* WotLK QC: Removed Time Warp (Cata Mage) */)),

                Spell.BuffSelf("Bestial Wrath",
                    ret => StyxWoW.Me.GotAlivePet && (!SpellManager.HasSpell("Kill Command") || SpellManager.Spells["Kill Command"].CooldownTimeLeft.TotalSeconds < 2)),

                // WotLK AoE: Explosive Trap + Multi-Shot + Steady Shot
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Common.CreateHunterTrapBehavior("Explosive Trap"),
                        Spell.Cast("Multi-Shot"),
                        Spell.Cast("Steady Shot"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )
                    ),
                    
                // WotLK BM Single Target: Kill Command -> Kill Shot -> Serpent Sting -> Multi-Shot -> Arcane Shot -> Steady Shot
                Spell.Cast("Kill Command", ret => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Cast("Kill Shot"),
                Common.CreateHunterTrapBehavior("Explosive Trap", false),
                Spell.Buff("Serpent Sting", true),
                Spell.Cast("Multi-Shot", ret => StyxWoW.Me.ManaPercent > 50),
                Spell.Cast("Arcane Shot",ret => StyxWoW.Me.ManaPercent > 40),
                Spell.Cast("Steady Shot"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
