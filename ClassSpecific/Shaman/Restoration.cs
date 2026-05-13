using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Shaman
{
    class Restoration
    {
        // WotLK QC: Removed T12 (Firelands, Cata 4.2) tier set constants and dead code — not applicable to WotLK


        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateRestoShamanHealingBuffs()
        {
            return new PrioritySelector(
                // Keep WS up at all times. Period.
                Spell.BuffSelf("Water Shield"),

                new Decorator(
                    ret => !StyxWoW.Me.Inventory.Equipped.MainHand.TemporaryEnchantment.IsValid && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole,
                    Spell.Cast("Earthliving Weapon"))
                );
        }


        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateRestoShamanRest()
        {
            return new PrioritySelector(
                CreateRestoShamanHealingBuffs(),
                CreateRestoShamanHealingOnlyBehavior(true),
                Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Ancestral Spirit"),
                CreateRestoShamanHealingOnlyBehavior(false,false)
                );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateRestoShamanCombatBehavior()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                        new PrioritySelector(
                            Safers.EnsureTarget(),
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateFaceTargetBehavior(),
                            Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                            Spell.BuffSelf("Fire Elemental Totem",
                                ret => (StyxWoW.Me.CurrentTarget.Elite || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3) &&
                                       !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                            Spell.BuffSelf("Searing Totem",
                                ret => StyxWoW.Me.CurrentTarget.Distance < Totems.GetTotemRange(WoWTotem.Searing) - 2f &&
                                        !StyxWoW.Me.Totems.Any(
                                            t => t.Unit != null && t.WoWTotem == WoWTotem.Searing &&
                                                    t.Unit.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Totems.GetTotemRange(WoWTotem.Searing)) &&
                                        !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                            Spell.Cast("Earth Shock"),
                            Spell.Cast("Lightning Bolt"),
                            Movement.CreateMoveToTargetBehavior(true, 32f)
                            ))
                    );
        }

        [Class(WoWClass.Shaman)]
        [Spec(TalentSpec.RestorationShaman)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateRestoShamanHealBehavior()
        {
            return
                new PrioritySelector(
                    CreateRestoShamanHealingOnlyBehavior());
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior()
        {
            return CreateRestoShamanHealingOnlyBehavior(false, false);
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly)
        {
            return CreateRestoShamanHealingOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateRestoShamanHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                new Decorator(
                    ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                        Totems.CreateSetTotems(),
                        // Mana tide...
                        Spell.Cast("Mana Tide Totem", ret => StyxWoW.Me.ManaPercent < 80),
                        // Grounding...
                        Spell.Cast("Grounding Totem", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 40 && u.IsTargetingMeOrPet && u.IsCasting)),

                        // Just pop RT on CD. GetBestRiptideTarget will find someone without RT.
                        Spell.Heal("Riptide", ret => GetBestRiptideTarget((WoWPlayer)ret)),
                        // And deal with some edge PVP cases.

                        Spell.Heal("Earth Shield", 
                            ret => (WoWUnit)ret, 
                            ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Earth Shield"))),

                        // Pop NS if someone is in some trouble.
                        Spell.BuffSelf("Nature's Swiftness", ret => ((WoWUnit)ret).HealthPercent < 15),
                        // WotLK: "Greater Healing Wave" renamed to "Healing Wave". The big heal is just "Healing Wave" in WotLK
                        Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 50),
                        // WotLK QC: Added Lesser Healing Wave — fast, efficient single-target heal (core Resto spell)
                        Spell.Heal("Lesser Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 70),
                        // Most (if not all) will leave this at 90. Its lower prio, high HPM, low HPS
                        Spell.Heal("Healing Wave", ret => (WoWUnit)ret, ret => ((WoWUnit)ret).HealthPercent < 60),


                        // CH/HR only in parties/raids
                        new Decorator(
                            ret => StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid,
                            new PrioritySelector(
                                // This seems a bit tricky, but its really not. This is just how we cache a somewhat expensive lookup.
                                // Set the context to the "best unit" for the cluster, so we don't have to do that check twice.
                                // Then just use the context when passing the unit to throw the heal on, and the target of the heal from the cluster count.
                                // Also ensure it will jump at least 3 times. (CH is pointless to cast if it won't jump 3 times!)
                                new PrioritySelector(
                                    context => Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, 12f),
                                    Spell.Heal(
                                        "Chain Heal", ret => (WoWPlayer)ret,
                                        ret => Clusters.GetClusterCount((WoWPlayer)ret, ChainHealPlayers, ClusterType.Chained, 12f) > 2)))),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                            new PrioritySelector(
                                Movement.CreateMoveToLosBehavior(),
                                Movement.CreateFaceTargetBehavior(),
                                Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                                Spell.BuffSelf("Fire Elemental Totem",
                                    ret => (StyxWoW.Me.CurrentTarget.Elite || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3) &&
                                           !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                                Spell.BuffSelf("Searing Totem",
                                    ret => StyxWoW.Me.CurrentTarget.Distance < Totems.GetTotemRange(WoWTotem.Searing) - 2f &&
                                           !StyxWoW.Me.Totems.Any(
                                                t => t.Unit != null && t.WoWTotem == WoWTotem.Searing &&
                                                     t.Unit.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Totems.GetTotemRange(WoWTotem.Searing)) &&
                                           !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                                Spell.Cast("Earth Shock"),
                                Spell.Cast("Lightning Bolt"),
                                Movement.CreateMoveToTargetBehavior(true, 32f)
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToTargetBehavior(true, 38f, ret => (WoWUnit)ret))

                )));
        }

        private static IEnumerable<WoWUnit> ChainHealPlayers
        {
            get
            {
                // WotLK: Chain Heal does consume Riptide — target players already missing Riptide for best results.
                return Unit.NearbyFriendlyPlayers.Where(u => u.HealthPercent < 90).Select(u => (WoWUnit)u);
            }
        }

        private static WoWPlayer GetBestRiptideTarget(WoWPlayer originalTarget)
        {
            if (!originalTarget.HasMyAura("Riptide"))
                return originalTarget;

            // Target already has RT. So lets find someone else to throw it on. Lowest health first preferably.
            return Unit.NearbyFriendlyPlayers.OrderBy(u => u.HealthPercent).Where(u => !u.HasMyAura("Riptide")).FirstOrDefault();
        }
    }
}
