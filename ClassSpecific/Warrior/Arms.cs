using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Logic.Combat;
using Styx.Helpers;
using System;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Warrior
{
    public class Arms
    {
        // WotLK compatibility: RagePercent property doesn't exist, so calculate it manually
        private static double RagePercent { get { return (StyxWoW.Me.CurrentRage / (double)StyxWoW.Me.MaxRage) * 100; } }

        private static string[] _slows;

        #region Common
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                    Helpers.Common.CreateDismount("Pulling")),
                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Throw", ret => StyxWoW.Me.CurrentTarget.IsFlying && Item.RangedIsType(WoWItemWeaponClass.Thrown)),
                        Spell.Cast("Shoot", ret => StyxWoW.Me.CurrentTarget.IsFlying &&
                            (Item.RangedIsType(WoWItemWeaponClass.Bow) || Item.RangedIsType(WoWItemWeaponClass.Gun))),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),

                //Buff up
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => RagePercent < 20 && SingularSettings.Instance.Warrior.UseWarriorShouts == false),

                //Charge
                Spell.Cast(
                    "Charge",
                    ret =>
                    StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < 25 &&
                    SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorCloser &&
                    Common.PreventDoubleCharge),
                Spell.Cast(
                    "Heroic Throw",
                    ret =>
                    !Unit.HasAura(StyxWoW.Me.CurrentTarget, "Charge Stun") && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Normal
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateArmsNormalPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateArmsNormalCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana &&
                    SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false &&
                    SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up — WotLK: T12 (Firelands Cata 4.2) doesn't exist, removed UseWarriorT12
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Normal)]
        public static Composite CreateArmsNormalCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                // Dispel Bubbles
                new Decorator(
                    ret =>
                    StyxWoW.Me.CurrentTarget.IsPlayer &&
                    (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") ||
                     StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") ||
                     StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) &&
                    SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                              Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint) WoWInventorySlot.Hands),

                //Stance Dancing
                //Pop over to Zerker
                Spell.BuffSelf("Berserker Stance",
                               ret =>
                               StyxWoW.Me.CurrentTarget.HasMyAura("Rend") &&
                               !StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood") && RagePercent < 75 &&
                               StyxWoW.Me.CurrentTarget.IsBoss() &&
                               SingularSettings.Instance.Warrior.UseWarriorStanceDance),
                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance",
                               ret =>
                               !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") ||
                               ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") ||
                                 StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) &&
                                SpellManager.Spells["Mortal Strike"].Cooldown) && RagePercent <= 75 &&
                               SingularSettings.Instance.Warrior.UseWarriorKeepStance),

                Spell.Cast("Charge",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 &&
                           SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false &&
                           SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),

                // ranged slow
                Spell.Buff("Piercing Howl",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer &&
                           !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) &&
                           SingularSettings.Instance.Warrior.UseWarriorSlows &&
                           SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // Melee slow
                Spell.Cast("Hamstring",
                           ret =>
                           StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) &&
                           SingularSettings.Instance.Warrior.UseWarriorSlows &&
                           SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //freebie dps - use it if it's available
                Spell.Cast("Victory Rush"),

                // AOE
                new Decorator(
                    ret =>
                    Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 &&
                    SingularSettings.Instance.Warrior.UseWarriorAOE,
                    new PrioritySelector(
                // WotLK 3.1+: Recklessness usable in any stance
                        Spell.BuffSelf("Recklessness",
                                       ret =>
                                       SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns &&
                                       SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm",
                                   ret =>
                                   SingularSettings.Instance.Warrior.UseWarriorBladestorm &&
                                   StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                // WotLK: Blood and Thunder doesn't exist (Cata 4.0.1+), Rend doesn't spread via TC — cast Rend normally
                        Spell.Cast("Rend", ret => !StyxWoW.Me.CurrentTarget.HasAura("Rend")),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false))),

                        new Decorator(ret => StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.CurrentTarget.HealthPercent <= 25,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Execute under 20% or Sudden Death proc
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20 || StyxWoW.Me.HasAura("Sudden Death")),

                //Default Rotation
                Spell.Buff("Rend"),
                Spell.Cast("Mortal Strike"),
                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 && SingularSettings.Instance.Warrior.UseWarriorBladestorm),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => RagePercent > 40 && SingularSettings.Instance.Warrior.UseWarriorSlamTalent),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Pvp
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpPreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana &&
                    SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false &&
                    SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up — WotLK: T12 (Firelands Cata 4.2) doesn't exist, removed UseWarriorT12
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateArmsPvpCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                Spell.BuffSelf("Battle Shout", ret => !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => RagePercent < 20),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") || ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") || StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) && SpellManager.Spells["Mortal Strike"].Cooldown) && RagePercent <= 75 && SingularSettings.Instance.Warrior.UseWarriorKeepStance),

                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Melee Heal
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 && SingularSettings.Instance.Warrior.UseWarriorAOE,
                    new PrioritySelector(
                // WotLK 3.1+: Recklessness usable in any stance
                        Spell.BuffSelf("Recklessness", ret => SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm", ret => SingularSettings.Instance.Warrior.UseWarriorBladestorm && StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                        Spell.Cast("Rend", ret => !StyxWoW.Me.CurrentTarget.HasAura("Rend")), // Blood and Thunder = Cata 4.0.1+, position (3,3) WotLK = Trauma
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                // Only pop TD on elites/players
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),                

                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),
                //Execute under 20% or Sudden Death proc
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20 || StyxWoW.Me.HasAura("Sudden Death")),

                //Default Rotation
                Spell.Cast("Overpower"),
                Spell.Buff("Rend"),
                Spell.Cast("Mortal Strike"),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Instances
        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Instances)]
        public static Composite CreateArmsInstancePreCombatBuffs()
        {
            return new PrioritySelector(
                //Buff up
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout")),
                Spell.BuffSelf("Commanding Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts == false)
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Instances)]
        public static Composite CreateArmsInstanceCombatBuffs()
        {
            return new PrioritySelector(
                // get enraged to heal up
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HealthPercent < 70 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

                //Retaliation if fighting elite or targeting player that swings
                Spell.Buff("Retaliation", ret => StyxWoW.Me.HealthPercent < 66 && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 &&
                    (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) &&
                    StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana &&
                    SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false &&
                    SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),
                // Recklessness if caster or elite
                Spell.Buff("Recklessness", ret => (StyxWoW.Me.CurrentTarget.IsPlayer || StyxWoW.Me.CurrentTarget.Elite) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns),

                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                // Buff up — WotLK: T12 (Firelands Cata 4.2) doesn't exist, removed UseWarriorT12
                Spell.BuffSelf("Battle Shout", ret => SingularSettings.Instance.Warrior.UseWarriorShouts && !StyxWoW.Me.HasAnyAura("Horn of Winter", "Strength of Earth Totem", "Battle Shout"))
                );
        }

        [Spec(TalentSpec.ArmsWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.Instances)]
        public static Composite CreateArmsInstanceCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                // Dispel Bubbles
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && (StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") || StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")) && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30f)
                        )),

                //Rocket belt!
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.Distance > 20,
                Item.UseEquippedItem((uint)WoWInventorySlot.Waist)),

                // Hands
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Stance Dancing
                //Pop over to Zerker
                Spell.BuffSelf("Berserker Stance", ret => StyxWoW.Me.CurrentTarget.HasMyAura("Rend") && !StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood") && RagePercent < 75 && StyxWoW.Me.CurrentTarget.IsBoss() && SingularSettings.Instance.Warrior.UseWarriorStanceDance),
                //Keep in Battle Stance
                Spell.BuffSelf("Battle Stance", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Rend") || ((StyxWoW.Me.ActiveAuras.ContainsKey("Overpower") || StyxWoW.Me.ActiveAuras.ContainsKey("Taste for Blood")) && SpellManager.Spells["Mortal Strike"].Cooldown) && RagePercent <= 75 && SingularSettings.Instance.Warrior.UseWarriorKeepStance),

                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25 && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false && SingularSettings.Instance.Warrior.UseWarriorCloser && Common.PreventDoubleCharge),

                Movement.CreateMoveBehindTargetBehavior(),

                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                // Melee slow
                Spell.Cast("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows) && SingularSettings.Instance.Warrior.UseWarriorSlows && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),

             //freebie dps - use it if it's available
                Spell.Cast("Victory Rush"),

                // AOE
                new Decorator(ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 3 && SingularSettings.Instance.Warrior.UseWarriorAOE,
                    new PrioritySelector(
                // WotLK 3.1+: Recklessness usable in any stance
                        Spell.BuffSelf("Recklessness", ret => SingularSettings.Instance.Warrior.UseWarriorDpsCooldowns && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false),
                        Spell.BuffSelf("Sweeping Strikes"),
                        Spell.Cast("Bladestorm", ret => SingularSettings.Instance.Warrior.UseWarriorBladestorm && StyxWoW.Me.CurrentTarget.DistanceSqr < 36),
                        Spell.Cast("Rend", ret => !StyxWoW.Me.CurrentTarget.HasAura("Rend")), // Blood and Thunder = Cata 4.0.1+, position (3,3) WotLK = Trauma
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Mortal Strike"))),

                //Interupts
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorInterupts,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                        Spell.Buff("Intimidating Shout", ret => StyxWoW.Me.CurrentTarget.Distance < 8 && StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.IsCasting && SingularSettings.Instance.Warrior.UseWarriorBasicRotation == false))),

                        new Decorator(ret => StyxWoW.Me.CurrentTarget.IsBoss() && StyxWoW.Me.CurrentTarget.HealthPercent <= 25,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Movement.CreateEnsureMovementStoppedBehavior(),
                        Spell.Cast("Shattering Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 30))),

                // Use Engineering Gloves
                //Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                //Default Rotation
                Spell.Buff("Rend"),
                Spell.Cast("Mortal Strike"),
                //Execute under 20% or Sudden Death proc
                Spell.Cast("Execute", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 20 || StyxWoW.Me.HasAura("Sudden Death")),

                //Bladestorm after dots and MS if against player
                Spell.Cast("Bladestorm", ret => StyxWoW.Me.CurrentTarget.IsPlayer && StyxWoW.Me.CurrentTarget.DistanceSqr < 36 && SingularSettings.Instance.Warrior.UseWarriorBladestorm),
                Spell.Cast("Overpower"),
                Spell.Cast("Slam", ret => RagePercent > 40 && SingularSettings.Instance.Warrior.UseWarriorSlamTalent),

                Spell.Cast("Cleave", ret =>
                    // Only even think about Cleave for more than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) >= 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),
                Spell.Cast("Heroic Strike", ret =>
                    // Only even think about HS for less than 2 mobs. (We're probably best off using melee range)
                                Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 6f) < 2 &&
                                    // WotLK: Incite (Prot T1) is passive +crit, no proc aura to check
                                CanUseRageDump()),

                //ensure were in melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
        #endregion

        #region Utils

        static bool CanUseRageDump()
        {
            // Check if we have 60 rage to use cleave.
            return RagePercent > 60;
        }
        #endregion
    }
}
