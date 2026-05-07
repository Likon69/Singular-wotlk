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
        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.All)]
        [Class(WoWClass.None)]
        [Priority(999)]
        [Context(WoWContext.All)]
        [IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        public static Composite CreateFlasksBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseAlchemyFlasks && !Unit.HasAnyAura(StyxWoW.Me, "Enhanced Agility", "Enhanced Intellect", "Enhanced Strength"),
                new PrioritySelector(
                    Item.UseItem(58149),
                    Item.UseItem(47499)));
        }

        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.All)]
        [Class(WoWClass.None)]
        [Priority(999)]
        [Context(WoWContext.All)]
        [IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        public static Composite CreateTrinketBehaviour()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.Trinket1,
                    Item.UseEquippedItem((uint)InventorySlot.Trinket0Slot)),
                new Decorator(
                    ret => SingularSettings.Instance.Trinket2,
                    Item.UseEquippedItem((uint)InventorySlot.Trinket1Slot)));
        }

        [Spec(TalentSpec.Any)]
        [Behavior(BehaviorType.All)]
        [Class(WoWClass.None)]
        [Priority(999)]
        [Context(WoWContext.All)]
        [IgnoreBehaviorCount(BehaviorType.Combat), IgnoreBehaviorCount(BehaviorType.Rest)]
        public static Composite CreateRacialBehaviour()
        {
            return new Decorator(
                ret => SingularSettings.Instance.UseRacials,
                new PrioritySelector(
                    // Dwarf — remove bleed/disease/poison
                    new Decorator(
                        ret => SpellManager.CanCast("Stoneform") && StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding ||
                            a.Spell.DispelType == WoWDispelType.Disease ||
                            a.Spell.DispelType == WoWDispelType.Poison),
                        Spell.Cast("Stoneform")),
                    // Gnome — remove root/snare
                    new Decorator(
                        ret => SpellManager.CanCast("Escape Artist") && Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared),
                        Spell.Cast("Escape Artist")),
                    // Human — PvP CC break
                    new Decorator(
                        ret => SpellManager.CanCast("Every Man for Himself") && PVP.IsCrowdControlled(StyxWoW.Me),
                        Spell.Cast("Every Man for Himself")),
                    // Undead — break fear/sleep/charm
                    new Decorator(
                        ret => SpellManager.CanCast("Will of the Forsaken") && StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Horrified, WoWSpellMechanic.Charmed),
                        Spell.Cast("Will of the Forsaken")),
                    // Draenei — self-heal
                    new Decorator(
                        ret => SpellManager.CanCast("Gift of the Naaru") && StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP,
                        Spell.Cast("Gift of the Naaru")),
                    // Night Elf — threat drop in party
                    new Decorator(
                        ret => SingularSettings.Instance.ShadowmeldThreatDrop && SpellManager.CanCast("Shadowmeld") && (StyxWoW.Me.IsInParty || StyxWoW.Me.IsInRaid) &&
                            !StyxWoW.Me.PartyMemberInfos.Any(pm => pm.Guid == StyxWoW.Me.Guid && pm.Role == WoWPartyMember.GroupRole.Tank) &&
                            ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid),
                        Spell.Cast("Shadowmeld")),
                    // Orc — on-CD offensive DPS (combat only)
                    new Decorator(
                        ret => StyxWoW.Me.IsInCombat && SpellManager.CanCast("Blood Fury"),
                        Spell.Cast("Blood Fury")),
                    // Troll — on-CD offensive DPS (combat only)
                    new Decorator(
                        ret => StyxWoW.Me.IsInCombat && SpellManager.CanCast("Berserking"),
                        Spell.Cast("Berserking")),
                    // Blood Elf — resource restore + interrupt (combat only)
                    new Decorator(
                        ret => StyxWoW.Me.IsInCombat && SpellManager.CanCast("Arcane Torrent"),
                        Spell.Cast("Arcane Torrent")),
                    // Tauren — AoE stun when 2+ enemies in melee range (combat only)
                    new Decorator(
                        ret => StyxWoW.Me.IsInCombat && SpellManager.CanCast("War Stomp") &&
                            Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2,
                        Spell.Cast("War Stomp"))
                    ));
        }
    }
}
