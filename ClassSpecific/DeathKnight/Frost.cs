using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
    {
        #region Normal Rotations

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFrostDeathKnightNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // WotLK: Frost DPS uses Blood Presence for +15% damage
                Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence")),

                Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                // Anti-magic shell
                Spell.BuffSelf("Anti-Magic Shell",
                        ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                    (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                    u.CurrentTargetGuid == StyxWoW.Me.Guid &&
                                    SingularSettings.Instance.DeathKnight.UseAntiMagicShell)),
                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.BuffSelf("Lichborne", ret => SingularSettings.Instance.DeathKnight.UseLichborne &&
                                                   (StyxWoW.Me.IsCrowdControlled() ||
                                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.LichbornePercent)),
                Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),
                Spell.Cast("Death Strike",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

                // Cooldowns
                Spell.BuffSelf("Raise Dead", ret => SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet),
                Spell.BuffSelf("Empower Rune Weapon", ret => SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 && StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 && !SpellManager.CanCast("Frost Strike")),
                Spell.BuffSelf("Unbreakable Armor", ret => SpellManager.HasSpell("Unbreakable Armor")),

                // Start AoE section - WotLK: IT + PS + Pestilence + HB spam
                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                              new PrioritySelector(
                                  // Apply diseases to main target
                                  Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  // Spread diseases with Pestilence
                                  Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                               StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                               Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10 && !u.HasMyAura("Frost Fever")) > 0),
                                  // Spam Howling Blast for AoE damage
                                  Spell.Cast("Howling Blast",
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                                  Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.Cast("Horn of Winter"),
                                  Movement.CreateMoveToMeleeBehavior(true)
                                  )),

                // WotLK Single Target Priority: Diseases ? Obliterate ? Pestilence/BS ? FS (KM) ? HB (Rime) ? FS
                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                
                // Obliterate priority
                Spell.Cast("Obliterate"),
                
                // Pestilence to refresh diseases (WotLK: Glyph of Disease)
                Spell.Cast("Pestilence",
                    ret => StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                           StyxWoW.Me.CurrentTarget.Auras["Frost Fever"].TimeLeft.TotalSeconds < 3),
                
                // Blood Strike if diseases need refresh or no Oblit
                Spell.Cast("Blood Strike",
                    ret => !SpellManager.HasSpell("Obliterate") ||
                           (!StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") && StyxWoW.Me.FrostRuneCount == 0) ||
                           (!StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") && StyxWoW.Me.UnholyRuneCount == 0)),
                
                // Frost Strike with Killing Machine proc
                Spell.Cast("Frost Strike",
                    ret => StyxWoW.Me.HasAura("Killing Machine") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                
                // Howling Blast with Rime proc (free HB)
                Spell.Cast("Howling Blast",
                    ret => StyxWoW.Me.HasAura("Freezing Fog") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                
                // Frost Strike without proc
                Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateFrostDeathKnightPvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // WotLK: Blood Presence for +15% damage
                Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence")),

                new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
                Spell.Buff("Chains of Ice", ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                // Anti-magic shell
                Spell.BuffSelf("Anti-Magic Shell",
                        ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                    (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                    u.CurrentTargetGuid == StyxWoW.Me.Guid &&
                                    SingularSettings.Instance.DeathKnight.UseAntiMagicShell)),

                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.BuffSelf("Lichborne", ret => SingularSettings.Instance.DeathKnight.UseLichborne &&
                                                   (StyxWoW.Me.IsCrowdControlled() ||
                                                   StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.LichbornePercent)),
                Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),
                Spell.Cast("Death Strike",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

                // Cooldowns
                Spell.BuffSelf("Raise Dead", ret => SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet),
                Spell.BuffSelf("Empower Rune Weapon", ret => SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 && StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 && !SpellManager.CanCast("Frost Strike")),
                Spell.BuffSelf("Unbreakable Armor", ret => SpellManager.HasSpell("Unbreakable Armor")),

                // PvP Priority
                Spell.Buff("Icy Touch", true, "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike", ret => StyxWoW.Me.HasAura("Killing Machine")),
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog")),
                Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Obliterate")),
                Spell.Cast("Frost Strike"),
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotations

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.FrostDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateFrostDeathKnightInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // WotLK: Blood Presence for +15% damage
                Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence")),

                // Cooldowns
                Spell.BuffSelf("Raise Dead",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseRaiseDead && !StyxWoW.Me.GotAlivePet &&
                           StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.BuffSelf("Empower Rune Weapon",
                           ret =>
                           SingularSettings.Instance.DeathKnight.UseEmpowerRuneWeapon && StyxWoW.Me.UnholyRuneCount == 0 &&
                           StyxWoW.Me.FrostRuneCount == 0 && StyxWoW.Me.DeathRuneCount == 0 &&
                           !SpellManager.CanCast("Frost Strike") && StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.BuffSelf("Unbreakable Armor", ret => SpellManager.HasSpell("Unbreakable Armor") && StyxWoW.Me.CurrentTarget.IsBoss()),

                Spell.BuffSelf("Icebound Fortitude",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.IceboundFortitudePercent &&
                               SingularSettings.Instance.DeathKnight.UseIceboundFortitude),
                Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

                Movement.CreateMoveBehindTargetBehavior(),
                // WotLK AoE: IT + PS + Pestilence + HB spam
                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                              new PrioritySelector(
                                  Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                                  Spell.Buff("Plague Strike", true, "Blood Plague"),
                                  Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                               StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                               Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10 && !u.HasMyAura("Frost Fever")) > 0),
                                  Spell.Cast("Howling Blast",
                                             ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.CastOnGround("Death and Decay",
                                        ret => StyxWoW.Me.CurrentTarget.Location,
                                        ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                                  Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                  Spell.Cast("Horn of Winter"),
                                  Movement.CreateMoveToMeleeBehavior(true)
                                  )),

                // Single Target
                Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                Spell.Buff("Plague Strike", true, "Blood Plague"),
                
                // Obliterate priority
                Spell.Cast(
                    "Obliterate",
                    ret =>
                    (StyxWoW.Me.FrostRuneCount == 2 && StyxWoW.Me.UnholyRuneCount == 2) ||
                    StyxWoW.Me.DeathRuneCount == 2 || StyxWoW.Me.HasAura("Killing Machine")),
                
                // Pestilence for disease refresh
                Spell.Cast("Pestilence",
                    ret => StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                           StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                           StyxWoW.Me.CurrentTarget.Auras["Frost Fever"].TimeLeft.TotalSeconds < 3),
                
                Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Obliterate")),
                
                // Frost Strike with Killing Machine
                Spell.Cast("Frost Strike", ret => StyxWoW.Me.HasAura("Killing Machine") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                
                // Howling Blast with Rime (Freezing Fog)
                Spell.Cast("Howling Blast", ret => StyxWoW.Me.HasAura("Freezing Fog") && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                
                Spell.Cast("Obliterate"),
                Spell.Cast("Frost Strike", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                Spell.Cast("Horn of Winter"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion
    }
}
