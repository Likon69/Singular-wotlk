using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Protection
    {
        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinRest()
        {
            return new PrioritySelector(
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(),
                // Can we res people?
                Spell.Resurrect("Redemption"));
        }


        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinCombat()
        {
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => (WoWUnit)ret),
                // WotLK 969 rotation: keep Holy Shield up for block chance + holy damage on block
                Spell.BuffSelf("Holy Shield"),

                // Seal twisting. If our mana gets stupid low, just throw on wisdom to get some mana back quickly, then put our main seal back on.
                // This is Seal of Vengeance (Alliance) / Seal of Corruption (Horde) once we get it, Righteousness when we dont.
                // WotLK uses Seal of Wisdom for mana regen (renamed to Seal of Insight in Cata 4.0.1)
                Spell.BuffSelf("Seal of Wisdom", ret => StyxWoW.Me.ManaPercent < 25),
                Spell.BuffSelf("Seal of Vengeance", ret => StyxWoW.Me.ManaPercent >= 25 && !SpellManager.HasSpell("Seal of Corruption")),
                Spell.BuffSelf("Seal of Corruption", ret => StyxWoW.Me.ManaPercent >= 25),
                Spell.BuffSelf("Seal of Righteousness", ret => StyxWoW.Me.ManaPercent >= 25 && !SpellManager.HasSpell("Seal of Vengeance") && !SpellManager.HasSpell("Seal of Corruption")),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                Spell.BuffSelf("Divine Shield",
                    ret => StyxWoW.Me.CurrentMap.IsBattleground && StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                Spell.Cast("Hand of Reckoning",
                    ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                    ret => SingularSettings.Instance.EnableTaunting && StyxWoW.Me.IsInInstance),

                //Multi target
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Any(),
                    new PrioritySelector(
			Spell.Cast("Divine Plea", ret => StyxWoW.Me.ManaPercent < 75),
                        Spell.Cast("Hammer of the Righteous"),
                        Spell.Cast("Hammer of Justice", ctx => !StyxWoW.Me.IsInParty),
                        Spell.Cast("Consecration", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= SingularSettings.Instance.Paladin.ProtConsecrationCount 
                            || StyxWoW.Me.CurrentTarget?.IsBoss() == true),
                        Spell.Cast("Holy Wrath"),
                        Spell.Cast("Avenger's Shield", ret => !SingularSettings.Instance.Paladin.AvengersPullOnly),
                        Spell.Cast("Judgement of Wisdom"),
                        Spell.Cast("Shield of Righteousness"), // WotLK: Crusader Strike is Ret-only, Shield of Righteousness is the Prot filler (L75, 6s CD)
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),
                //Single target
		Spell.Cast("Divine Plea", ret => StyxWoW.Me.ManaPercent < 75),
                Spell.Cast("Shield of Righteousness"), // WotLK: Crusader Strike is Ret-only, Shield of Righteousness is the Prot filler (L75, 6s CD)
                Spell.Cast("Hammer of Justice"),
                Spell.Cast("Judgement of Wisdom"),
                Spell.Cast("Hammer of Wrath", ret => ((WoWUnit)ret).HealthPercent <= 20),
                Spell.Cast("Avenger's Shield", ret => !SingularSettings.Instance.Paladin.AvengersPullOnly),
                // Don't waste mana on cons if its not a boss.
                Spell.Cast("Consecration", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= SingularSettings.Instance.Paladin.ProtConsecrationCount),
                Spell.Cast("Holy Wrath"),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Avenger's Shield"),
                    Spell.Cast("Judgement of Wisdom"),
                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.Cast(
                        "Hand of Reckoning",
                        ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                        ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Count != 0),
                    Spell.BuffSelf("Avenging Wrath"),
                    Spell.BuffSelf(
                        "Lay on Hands",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealth && !StyxWoW.Me.HasAura("Forbearance")),
                    // WotLK: Ardent Defender is a passive talent, not an active cooldown (became active in Cata)
                    Spell.BuffSelf(
                        "Divine Protection",
                        ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthProt)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.ProtectionPaladin)]
        [Behavior(BehaviorType.PullBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateProtectionPaladinPullBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Divine Plea")
                    );
        }
    }
}
