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

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        #region Normal Rotation

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateUnholyDeathKnightNormalCombat()
        {
            return new PrioritySelector(
               Safers.EnsureTarget(),
               Movement.CreateMoveToLosBehavior(),
               Movement.CreateFaceTargetBehavior(),
               Helpers.Common.CreateAutoAttack(true),
               Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

               // WotLK: Unholy starts in Unholy Presence for opener, then switches to Blood Presence
               // For normal rotation, we use Blood Presence after initial pull
               Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence") && StyxWoW.Me.IsInCombat),

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
               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),

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

               // Apply diseases
               Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

                // Start AoE section - WotLK: IT + PS + Pestilence + D&D
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                        new PrioritySelector(
                            Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                            StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                            Unit.NearbyUnfriendlyUnits.Count(u =>
                                                    u.DistanceSqr < 10 * 10 && !u.HasMyAura("Blood Plague") &&
                                                    !u.HasMyAura("Frost Fever")) > 0),
                            // WotLK: Ghoul Frenzy for pet damage buff
                            Spell.Cast("Ghoul Frenzy",
                                        ret => StyxWoW.Me.GotAlivePet && SpellManager.HasSpell("Ghoul Frenzy")),
                            // WotLK: Summon Gargoyle (major DPS cooldown)
                            Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle && StyxWoW.Me.CurrentTarget.IsBoss()),
                            // WotLK: Death and Decay spam for AoE
                            Spell.CastOnGround("Death and Decay",
                                ret => StyxWoW.Me.CurrentTarget.Location,
                                ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                            // WotLK: Blood Strike for Death Rune conversion via Reaping (Unholy Tier 5 talent)
                            Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount >= 1),
                            Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount == 2 || StyxWoW.Me.DeathRuneCount >= 2),
                            Spell.Cast("Icy Touch", ret => StyxWoW.Me.FrostRuneCount == 2 && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Scourge Strike"),
                            Spell.Cast("Death Coil"),
                            Spell.Cast("Horn of Winter"),
                            Movement.CreateMoveToMeleeBehavior(true)
                            )),

               // WotLK Single Target Priority: D&D > Blood Strike > Scourge Strike > Death Coil
               // Ghoul Frenzy for pet buff
               Spell.Cast("Ghoul Frenzy",
                           ret => StyxWoW.Me.GotAlivePet && SpellManager.HasSpell("Ghoul Frenzy")),
               
               // Death and Decay (primary Frost+Unholy rune spender)
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay &&
                                         (StyxWoW.Me.UnholyRuneCount >= 1 && StyxWoW.Me.FrostRuneCount >= 1)),
               
               // Blood Strike for Death Rune conversion via Reaping (critical for Unholy rotation)
               Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount >= 1),
               
               // Scourge Strike
               Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount >= 1),
               
               // Death Coil with Sudden Doom or high RP
               Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
               
               // Filler
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
               Movement.CreateMoveToMeleeBehavior(true)
               );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateUnholyDeathKnightPvPCombat()
        {
            return new PrioritySelector(
               Safers.EnsureTarget(),
               Movement.CreateMoveToLosBehavior(),
               Movement.CreateFaceTargetBehavior(),
               Helpers.Common.CreateAutoAttack(true),
               Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
               
               // WotLK: Blood Presence for +15% damage
               Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence") && StyxWoW.Me.IsInCombat),
               
               new Sequence(
                    Spell.Cast("Death Grip",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),
                    new DecoratorContinue(
                        ret => StyxWoW.Me.IsMoving,
                        new Action(ret => Navigator.PlayerMover.MoveStop())),
                    new WaitContinue(1, new ActionAlwaysSucceed())
                    ),
               Spell.Buff("Chains of Ice"),
               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),

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
               // WotLK QC: Removed duplicate Lichborne cast (was Spell.Buff instead of BuffSelf, missing settings guard)
               Spell.BuffSelf("Death Coil",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent &&
                               StyxWoW.Me.HasAura("Lichborne")),
               Spell.Cast("Death Strike",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.DeathKnight.DeathStrikeEmergencyPercent),

               // Apply diseases
               Spell.Buff("Icy Touch", true, "Frost Fever"),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

               // WotLK: Summon Gargoyle (major cooldown)
               Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle),
               
               // WotLK: Ghoul Frenzy for pet buff
               Spell.Cast("Ghoul Frenzy", ret => StyxWoW.Me.GotAlivePet && SpellManager.HasSpell("Ghoul Frenzy")),

               // WotLK QC: Removed duplicate Lichborne — already handled above (lines 168-173) with proper settings guards
               
               Spell.Cast("Death Strike", ret => StyxWoW.Me.HealthPercent < 30),
               
               // WotLK rotation: D&D > Blood Strike > Scourge Strike > Death Coil
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
               Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount >= 1),
               Spell.Cast("Scourge Strike"),
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
               Movement.CreateMoveToMeleeBehavior(true)
               );
        }

        #endregion

        #region Instance Rotations

        [Class(WoWClass.DeathKnight)]
        [Spec(TalentSpec.UnholyDeathKnight)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateUnholyDeathKnightInstanceCombat()
        {
            return new PrioritySelector(
               Safers.EnsureTarget(),
               Movement.CreateMoveToLosBehavior(),
               Movement.CreateFaceTargetBehavior(),
               Helpers.Common.CreateAutoAttack(true),
               Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

               // WotLK: Blood Presence for +15% damage
               Spell.BuffSelf("Blood Presence", ret => !StyxWoW.Me.HasAura("Blood Presence") && StyxWoW.Me.IsInCombat),

               Spell.BuffSelf("Raise Dead", ret => !StyxWoW.Me.GotAlivePet),
               
               // Apply diseases
               Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
               Movement.CreateMoveBehindTargetBehavior(),
               Spell.Buff("Plague Strike", true, "Blood Plague"),

                // Start AoE section
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= SingularSettings.Instance.DeathKnight.DeathAndDecayCount,
                        new PrioritySelector(
                            Spell.Cast("Pestilence",
                                        ret => StyxWoW.Me.CurrentTarget.HasMyAura("Blood Plague") &&
                                            StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") &&
                                            Unit.UnfriendlyUnitsNearTarget(10f).Count(u =>
                                                    !u.HasMyAura("Blood Plague") &&
                                                    !u.HasMyAura("Frost Fever")) > 0),
                            // WotLK: Ghoul Frenzy for AoE pet buff
                            Spell.Cast("Ghoul Frenzy",
                                        ret => StyxWoW.Me.GotAlivePet && SpellManager.HasSpell("Ghoul Frenzy")),
                            Spell.CastOnGround("Death and Decay",
                                ret => StyxWoW.Me.CurrentTarget.Location,
                                ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay),
                            // WotLK: Blood Strike for Death Rune conversion via Reaping
                            Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount >= 1),
                            Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount >= 1 || StyxWoW.Me.DeathRuneCount >= 1),
                            Spell.Cast("Icy Touch", ret => StyxWoW.Me.FrostRuneCount >= 1 && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Death Coil"),
                            Spell.Cast("Horn of Winter"),
                            Movement.CreateMoveToMeleeBehavior(true)
                            )),

               // WotLK: Summon Gargoyle on boss (major DPS cooldown)
               Spell.Cast("Summon Gargoyle", ret => SingularSettings.Instance.DeathKnight.UseSummonGargoyle && StyxWoW.Me.CurrentTarget.IsBoss()),
               
               // WotLK: Ghoul Frenzy for single target
               Spell.Cast("Ghoul Frenzy", ret => StyxWoW.Me.GotAlivePet && SpellManager.HasSpell("Ghoul Frenzy")),
               
               // WotLK Single Target: D&D > Blood Strike > Scourge Strike > Death Coil
               Spell.CastOnGround("Death and Decay",
                                  ret => StyxWoW.Me.CurrentTarget.Location,
                                  ret => SingularSettings.Instance.DeathKnight.UseDeathAndDecay &&
                                         (StyxWoW.Me.UnholyRuneCount >= 1 && StyxWoW.Me.FrostRuneCount >= 1)),
               
               // Blood Strike for Death Rune conversion via Reaping (critical)
               Spell.Cast("Blood Strike", ret => StyxWoW.Me.BloodRuneCount >= 1),
               
               Spell.Cast("Scourge Strike", ret => StyxWoW.Me.UnholyRuneCount >= 1),
               Spell.Cast("Death Coil", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Sudden Doom") || StyxWoW.Me.CurrentRunicPower >= 80),
               Spell.Cast("Death Coil"),
               Spell.Cast("Horn of Winter"),
               Movement.CreateMoveToMeleeBehavior(true)
               );
        }

        #endregion
    }
}
