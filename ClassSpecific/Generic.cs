using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.Logic.Inventory;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular.ClassSpecific
{
    public static class Generic
    {
        //[Spec(TalentSpec.Any)]
        //[Behavior(BehaviorType.All)]
        //[Class(WoWClass.None)]
        //[Priority(999)]
        //[Context(WoWContext.All)]
        //[IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        //public static Composite CreateFlasksBehaviour()
        //{
        //    return new Decorator(
        //        ret => SingularSettings.Instance.UseAlchemyFlasks && !Unit.HasAnyAura(StyxWoW.Me, "Enhanced Agility", "Enhanced Intellect", "Enhanced Strength"),
        //        new PrioritySelector(
        //            Item.UseItem(58149),
        //            Item.UseItem(47499)));
        //}

        //[Spec(TalentSpec.Any)]
        //[Behavior(BehaviorType.All)]
        //[Class(WoWClass.None)]
        //[Priority(999)]
        //[Context(WoWContext.All)]
        //[IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        //public static Composite CreateTrinketBehaviour()
        //{
        //    return new PrioritySelector(
        //        new Decorator(
        //            ret => SingularSettings.Instance.Trinket1,
        //            Item.UseEquippedItem((uint)InventorySlot.Trinket0Slot)),
        //        new Decorator(
        //            ret => SingularSettings.Instance.Trinket2,
        //            Item.UseEquippedItem((uint)InventorySlot.Trinket1Slot)));
        //}

        public static Composite CreateRacialBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials,
                new PrioritySelector(
                    Spell.BuffSelf("Stoneform",
                        ret => StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding ||
                            a.Spell.DispelType == WoWDispelType.Disease ||
                            a.Spell.DispelType == WoWDispelType.Poison)),
                    Spell.BuffSelf("Escape Artist",
                        ret => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared)),
                    Spell.Cast("Every Man for Himself", on => StyxWoW.Me,
                        ret => PVP.IsCrowdControlled(StyxWoW.Me)),
                    Spell.Cast("Will of the Forsaken", on => StyxWoW.Me,
                        ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Horrified, WoWSpellMechanic.Charmed)),
                    Spell.BuffSelf("Gift of the Naaru",
                        ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP),
                    Spell.BuffSelf("Shadowmeld",
                        ret => SingularSettings.Instance.ShadowmeldThreatDrop && (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) &&
                            !StyxWoW.Me.PartyMemberInfos.Any(pm => pm.Guid == StyxWoW.Me.Guid && pm.Role == WoWPartyMember.GroupRole.Tank) &&
                            ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid)),
                    Spell.BuffSelf("Blood Fury", ret => StyxWoW.Me.IsInCombat),
                    Spell.BuffSelf("Berserking", ret => StyxWoW.Me.IsInCombat)
                    ));
        }

        //Herbalism heal(Combat only) - Lifeblood
        public static Composite CreateHerbHealingBehaviour()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => StyxWoW.Me.IsInCombat && SpellManager.CanCast("Lifeblood") && StyxWoW.Me.HealthPercent < SingularSettings.Instance.LifebloodHP,
                    Spell.Cast("Lifeblood"))
                );
        }
    }
}
