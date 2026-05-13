using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Logic;
using System.Collections.Generic;
using Styx.WoWInternals;
using Styx.Logic.Combat;

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }

        // IloveAnimals
        public static List<WoWUnit> EnemyUnits
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                        .Where(unit =>
                               !unit.IsFriendly
                               && (unit.IsTargetingMeOrPet
                                   || unit.IsTargetingMyPartyMember
                                   || unit.IsTargetingMyRaidMember
                                   || unit.IsPlayer)
                               && !unit.IsNonCombatPet
                               && !unit.IsCritter
                               && unit.DistanceSqr
                               <= 15 * 15).ToList();
            }
        }

        #region PreCombat Buffs

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.RestorationDruid)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        public static Composite CreateDruidPreCombatBuff()
        {
            // Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
            return new PrioritySelector( 
                Spell.Cast(
                    "Mark of the Wild",
                    ret => StyxWoW.Me,
                    // WotLK QC: Removed "Embrace of the Shale Spider" (Cata-only Shale Spider exotic pet buff)
                    ret =>!StyxWoW.Me.HasAnyAura("Mark of the Wild", "Blessing of Kings")
                        || (SingularSettings.Instance.Druid.BuffRaidWithMotw && (!StyxWoW.Me.Combat || (StyxWoW.Me.Combat && SingularSettings.Instance.Druid.CatRaidRebuff)) 
                        && !StyxWoW.Me.HasAura("Prowl") 
                        && Unit.NearbyFriendlyPlayers.Any(unit =>
                                                    unit.Distance <= 30f &&
                                                    !unit.Dead && !unit.IsGhost && unit.IsInMyPartyOrRaid &&
                                                    !unit.HasAnyAura("Mark of the Wild",
                                                                     "Blessing of Kings"))))
                );
        }

        #endregion

        #region Combat Buffs

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Spec(TalentSpec.RestorationDruid)]
        [Context(WoWContext.Instances)]
        public static Composite CreateDruidInstanceCombatBuffs()
        {
            const uint mapleSeedId = 17034;

            return new PrioritySelector(
                ctx =>
                Group.Tanks.FirstOrDefault(t => !t.IsMe && t.Dead) ??
                Group.Healers.FirstOrDefault(h => !h.IsMe && h.Dead),
                new Decorator(
                    ret => ret != null && Item.HasItem(mapleSeedId),
                    new PrioritySelector(
                        Spell.WaitForCast(true),
                        Movement.CreateMoveToLosBehavior(ret => (WoWPlayer)ret),
                        new Decorator(ret => SingularSettings.Instance.Druid.CatRaidRezz,
                                      Spell.Cast("Rebirth", ret => (WoWPlayer)ret)),
                        Movement.CreateMoveToTargetBehavior(true, 32f)))
                );
        }

        #endregion

        #region Rest

        [Class(WoWClass.Druid)]
        [Behavior(BehaviorType.Rest)]
        [Spec(TalentSpec.BalanceDruid)]
        [Spec(TalentSpec.FeralDruid)]
        [Context(WoWContext.All)]
        public static Composite CreateBalanceAndFeralDruidRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.IsInRaid && !StyxWoW.Me.IsInInstance && !Battlegrounds.IsInsideBattleground
                           &&
                           (StyxWoW.Me.ZoneId != 3702 && StyxWoW.Me.ZoneId != 4378 && StyxWoW.Me.ZoneId != 3698 &&
                            StyxWoW.Me.ZoneId != 3968 && StyxWoW.Me.ZoneId != 4406),
                    CreateNonRestoHeals()),
                new Decorator(
                    ret =>
                    (StyxWoW.Me.IsInRaid ||
                     StyxWoW.Me.IsInInstance) && SingularSettings.Instance.Druid.RaidHealNonCombat && !StyxWoW.Me.Combat,
                    CreateNonRestoHeals()),
                new Decorator(
                    ret =>
                    (Battlegrounds.IsInsideBattleground ||
                     StyxWoW.Me.ZoneId == 3702 || StyxWoW.Me.ZoneId == 4378 || StyxWoW.Me.ZoneId == 3698 ||
                     StyxWoW.Me.ZoneId == 3968 || StyxWoW.Me.ZoneId == 4406) &&
                    (SingularSettings.Instance.Druid.PvPpHealBool == true ||
                     (SingularSettings.Instance.Druid.PvPpHealBool == false && !StyxWoW.Me.Combat)),
                    CreateNonRestoPvPHeals()),
                Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Revive")
                );
        }

        #endregion

        #region Non Resto Healing

        public static Composite CreateNonRestoHeals()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.Druid.NoHealBalanceAndFeral && !StyxWoW.Me.HasAura("Drink"),
                    new PrioritySelector(
                        Spell.WaitForCast(false, false),
                        Spell.Heal("Healing Touch",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoprocc &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness")),
                        Spell.Heal("Regrowth",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoprocc &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness") &&
                                   !SpellManager.HasSpell("Healing Touch")),
                        Spell.Heal("Regrowth",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoRegrowth &&
                                   !StyxWoW.Me.HasAura("Regrowth")),
                        Spell.Heal("Lifebloom",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoLifebloom &&
                                   !StyxWoW.Me.HasAura("Lifebloom", 3)),
                        Spell.Heal("Rejuvenation",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoRejuvenation &&
                                   !StyxWoW.Me.HasAura("Rejuvenation")),
                        Spell.Heal("Healing Touch",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.NonRestoHealingTouch)
                        )
                    );
        }

        public static Composite CreateNonRestoPvPHeals()
        {
            return
                new PrioritySelector(
                    new Decorator(ret => SingularSettings.Instance.Druid.PvPGrasp && EnemyUnits.Count >= 2,
                                  Spell.Cast("Nature's Grasp")
                        ),
                    new Decorator(
                        ret =>
                        (SingularSettings.Instance.Druid.PvPpHealBool == true  ||
                         (SingularSettings.Instance.Druid.PvPpHealBool == false && !StyxWoW.Me.Combat)) &&
                        !StyxWoW.Me.HasAura("Drink"),
                        new PrioritySelector(
                            Spell.WaitForCast(false, false),
                            Spell.Heal("Healing Touch",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPProcc &&
                                       StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness")),
                            Spell.Heal("Regrowth",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPProcc &&
                                       StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness") &&
                                       !SpellManager.HasSpell("Healing Touch")),
                            Spell.Heal("Regrowth",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPRegrowth &&
                                       !StyxWoW.Me.HasAura("Regrowth")),
                            Spell.Heal("Lifebloom",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPLifeBloom &&
                                       !StyxWoW.Me.HasAura("Lifebloom", 3)),
                            Spell.Heal("Rejuvenation",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPReju &&
                                       !StyxWoW.Me.HasAura("Rejuvenation")),
                            Spell.Heal("Healing Touch",
                                       ret =>
                                       StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.PvPHealingTouch)
                            )
                        )
                    );
        }

        public static Composite CreateRaidCatHeal()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.Druid.NoHealBalanceAndFeral,
                    new PrioritySelector(
                        Spell.WaitForCast(false, false),
                        Spell.Heal("Healing Touch",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RaidCatProccHeal &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness")),
                        Spell.Heal("Regrowth",
                                   ret =>
                                   StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RaidCatProccHeal &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness") &&
                                   !SpellManager.HasSpell("Healing Touch"))
                        )
                    );
        }

        #endregion

        public static Composite CreateEscapeFromCc()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Dash",
                               ret =>
                               SingularSettings.Instance.Druid.PvPRooted &&
                               StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                               StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat),
                    new Decorator(
                        ret =>
                        (SingularSettings.Instance.Druid.PvPSnared &&
                         StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !StyxWoW.Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Cat Form\")")
                                )
                            )
                        ),
                    new Decorator(
                        ret =>
                        (SingularSettings.Instance.Druid.PvPSnared &&
                         StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !StyxWoW.Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         // WotLK QC: Check both Bear Form and Dire Bear Form (level 40+ druids use Dire Bear)
                         (StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear || StyxWoW.Me.Shapeshift == ShapeshiftForm.DireBear)),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Dire Bear Form\")"))
                            )
                        )
                    );
        }

        public static Composite CreateCycloneAdd()
        {
            return
                new PrioritySelector(
                    ctx =>
                    Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForCyclone),
                    new Decorator(
                        ret =>
                        ret != null && SingularSettings.Instance.Druid.PvPccAdd &&
                        StyxWoW.Me.ActiveAuras.ContainsKey("Predator's Swiftness") &&
                        // WotLK QC: Fixed Polymorph → Cyclone (Druids don't cast Polymorph)
                        Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Cyclone")),
                        new PrioritySelector(
                            Spell.Buff("Cyclone", ret => (WoWUnit)ret))));
        }

        private static bool IsViableForCyclone(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (StyxWoW.Me.IsInParty &&
                StyxWoW.Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }


        
    }
}
