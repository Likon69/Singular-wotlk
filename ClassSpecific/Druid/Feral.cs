using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using CommonBehaviors.Actions;
using Action = TreeSharp.Action;
using System.Drawing;
using System.Collections.Generic;
using Styx.Plugins;
using Styx.Helpers;
using Styx.Logic.Pathing;
using System.Diagnostics;


namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        #region Properties & Fields
        private static DruidSettings Settings { get { return SingularSettings.Instance.Druid; } }

        // WotLK: Cata tier set bonuses (T11, T13) do not exist in 3.3.5a
        // These always return false — kept as stubs to avoid compilation errors from remaining references
        private static bool Has4PieceTier11Bonus { get { return false; } }
        private static bool Has2PieceTier13Bonus { get { return false; } }
        private static bool Has4PieceTier13Bonus { get { return false; } }

        #region ILoveAnimals section
        /* Credits to Shaddar! */

        public static int CurrentEnergy
        {
            get { return Lua.GetReturnVal<int>("return UnitMana(\"player\");", 0); }
        }

        private static float EnergyRegen
        {
            get { return Lua.GetReturnVal<float>("return GetPowerRegen()", 1); }
        }

        private static bool IsrakeTfed
        {
            get
            {
                if (StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                    StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds >= 6)
                    return false;

                if ((StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                     StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds > 5) &&
                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 14)
                    )
                    return false;

                if ((StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                     StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds > 4) &&
                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 13)
                    )
                    return false;

                if ((StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                     StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds > 3) &&
                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 11)
                    )
                    return false;

                if ((StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                     StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds > 1) &&
                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 10)
                    )
                    return false;

                return (!StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") ||
                        StyxWoW.Me.GetAuraTimeLeft("Tiger's Fury", true).TotalSeconds < 1)
                       ||
                       (!StyxWoW.Me.CurrentTarget.HasMyAura("Rip") ||
                        StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds >= 9);
            }
        }

        private static int Finisherhealth
        {
            get
            {
                int health = 120;
                if (StyxWoW.Me.Level == 80 && StyxWoW.Me.IsInRaid && StyxWoW.Me.NumRaidMembers >= 11)
                    health = 500000;
                if (StyxWoW.Me.Level == 80 && StyxWoW.Me.IsInRaid && StyxWoW.Me.NumRaidMembers <= 10)
                    health = 250000;
                if (StyxWoW.Me.Level == 80 && StyxWoW.Me.IsInInstance)
                    health = 100000;
                if (StyxWoW.Me.Level == 80 && !StyxWoW.Me.IsInInstance && !StyxWoW.Me.IsInRaid)
                    health = 1000;
                if (StyxWoW.Me.Level >= 70 && StyxWoW.Me.Level <= 79)
                    health = StyxWoW.Me.Level * 30;
                if (StyxWoW.Me.Level >= 10 && StyxWoW.Me.Level <= 69)
                    health = StyxWoW.Me.Level * 20;
                return health;
            }
        }

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
                               && !unit.IsTotem
                               && unit.DistanceSqr
                               <= 15 * 15).ToList();
            }
        }

        public static List<WoWUnit> EnemyUnitsPvP
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                        .Where(unit =>
                               unit.IsTargetingMeOrPet
                               && !unit.IsNonCombatPet
                               && !unit.IsCritter
                               && !unit.IsTotem
                               && unit.DistanceSqr
                               <= 15 * 15).ToList();
            }
        }
        #endregion

        #endregion

        #region Normal Rotation
        [Spec(TalentSpec.FeralDruid)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                new Decorator(
                    ret => StyxWoW.Me.Level < 20,
                    CreateFeralLevel1020Pull()),

                new Decorator(
                    ret => StyxWoW.Me.Level < 46,
                    CreateFeralLevel2046Pull()),

                Spell.BuffSelf("Cat Form"),
                Spell.BuffSelf("Prowl", ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 30 * 30),
                Spell.Cast("Feral Charge - Cat"),
                Spell.Cast("Dash",
                    ret => StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange + 2f &&
                           (!SpellManager.HasSpell("Feral Charge - Cat") ||
                           SpellManager.Spells["Feral Charge - Cat"].CooldownTimeLeft.TotalSeconds >= 3)),
                Spell.Cast("Pounce"),
                Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Mangle (Cat)"),
                Spell.Cast("Moonfire", ret => StyxWoW.Me.CurrentTarget.Distance2DSqr < 10 * 10 && Math.Abs(StyxWoW.Me.CurrentTarget.Z - StyxWoW.Me.Z) > 5),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        [Spec(TalentSpec.FeralDruid)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Normal)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateNonRestoHeals(),
                Spell.BuffSelf("Cat Form"),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive spells
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent < Settings.FeralBarkskin),
                Spell.BuffSelf("Survival Instincts", ret => StyxWoW.Me.HealthPercent < Settings.SurvivalInstinctsHealth),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                    new PrioritySelector(
                        Spell.Cast("Ferocious Bite",
                            ret => StyxWoW.Me.ComboPoints == 5 ||
                                   StyxWoW.Me.ComboPoints >= 2 && StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Swipe (Cat)"),
                        Spell.Cast("Mangle (Cat)")
                        )),

                new Decorator(
                    ret => StyxWoW.Me.Level < 20,
                    CreateFeralLevel1020Combat()),

                new Decorator(
                    ret => StyxWoW.Me.Level < 46,
                    CreateFeralLevel2046Combat()),

                Movement.CreateMoveBehindTargetBehavior(),
                Spell.BuffSelf("Tiger's Fury"),
                Spell.Cast("Ferocious Bite",
                    ret => StyxWoW.Me.ComboPoints == 5 ||
                           StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
				Spell.Buff("Rake", true, ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Mangle (Cat)"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateFeralLevel1020Pull()
        {
            return new PrioritySelector(
                Spell.Buff("Moonfire"),
                Movement.CreateMoveToTargetBehavior(true, 30f)
                );
        }

        private static Composite CreateFeralLevel1020Combat()
        {
            return new PrioritySelector(
                Spell.Cast("Ferocious Bite",
                    ret => StyxWoW.Me.ComboPoints == 5 ||
                           StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Buff("Rake", true, ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Mangle (Cat)"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateFeralLevel2046Pull()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form"),
                Spell.BuffSelf("Prowl"),
                Spell.Cast("Feral Charge - Cat"),				
				Spell.Cast("Pounce"),
				Spell.Buff("Rake", true, ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Mangle (Cat)"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateFeralLevel2046Combat()
        {
            return new PrioritySelector(
                Spell.Cast("Ferocious Bite",
                    ret => StyxWoW.Me.ComboPoints == 5 ||
                           StyxWoW.Me.ComboPoints > 1 && StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Buff("Rake", true, ret => StyxWoW.Me.CurrentTarget),
                Spell.Cast("Mangle (Cat)"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region ILoveAnimals Initialize Pull

        [Spec(TalentSpec.FeralDruid)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Battlegrounds)]
        [Context(WoWContext.Instances)]

        public static Composite InitPull()
        {
            return new PrioritySelector(
                new Decorator(ret => Battlegrounds.IsInsideBattleground || StyxWoW.Me.CurrentMap.IsArena,
                              CreateFeralPvPPull()
                    ),
                new Decorator(
                    ret => (StyxWoW.Me.IsInInstance || StyxWoW.Me.IsInRaid) && !Battlegrounds.IsInsideBattleground
                           && !StyxWoW.Me.CurrentMap.IsArena,
                    CreateFeralInstanceCombat())
                );
        }

        #endregion

        #region ILoveAnimals Init Combat

        [Spec(TalentSpec.FeralDruid)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Druid)]
        [Context(WoWContext.Battlegrounds)]
        [Context(WoWContext.Instances)]
        public static Composite InitCombat()
        {
            return new PrioritySelector(
                new Decorator(ret => Battlegrounds.IsInsideBattleground ||
                                     StyxWoW.Me.ZoneId == 3702 || StyxWoW.Me.ZoneId == 4378 || StyxWoW.Me.ZoneId == 3698 ||
                                     StyxWoW.Me.ZoneId == 3968 || StyxWoW.Me.ZoneId == 4406,
                              CreateFeralPvPCombat()
                    ),
                new Decorator(
                    ret => (StyxWoW.Me.IsInInstance || StyxWoW.Me.IsInRaid) && !Battlegrounds.IsInsideBattleground
                           &&
                           (StyxWoW.Me.ZoneId != 3702 && StyxWoW.Me.ZoneId != 4378 && StyxWoW.Me.ZoneId != 3698 &&
                            StyxWoW.Me.ZoneId != 3968 && StyxWoW.Me.ZoneId != 4406),
                    CreateFeralInstanceCombat())
                );
        }
        #endregion

        #region Battleground Rotation

        // Start of ILoveAnimals Battleground Rotation

        private static Composite CreateFeralBearPvPPull()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Dire Bear Form"),
                Spell.Cast("Faerie Fire (Feral)"),
                Spell.Cast("Feral Charge - Bear"),
                Spell.Cast("Moonfire",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance2D < 10 &&
                           Math.Abs(StyxWoW.Me.CurrentTarget.Z - StyxWoW.Me.Z) > 5),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        private static Composite CreateFeralCatPvPPull()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form"),
                Spell.BuffSelf("Prowl",
                               ret => Settings.CatNormalPullStealth && StyxWoW.Me.CurrentTarget.Distance < 30),
                Spell.Cast("Feral Charge - Cat"),
                Spell.Cast("Faerie Fire (Feral)",
                           ret =>
                           !Settings.CatNormalPullStealth || !StyxWoW.Me.HasAura("Prowl")),
                Spell.Cast("Dash",
                           ret => StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange + 2f &&
                                  (!SpellManager.HasSpell("Feral Charge - Cat") ||
                                   SpellManager.Spells["Feral Charge - Cat"].CooldownTimeLeft.TotalSeconds >= 3)),
                Spell.Cast("Pounce"),
                Spell.Cast("Shred", ret => StyxWoW.Me.CurrentTarget.MeIsBehind),
                Spell.Cast("Mangle (Cat)"),
                Spell.Cast("Moonfire",
                           ret =>
                           StyxWoW.Me.CurrentTarget.Distance2D < 10 &&
                           Math.Abs(StyxWoW.Me.CurrentTarget.Z - StyxWoW.Me.Z) > 5),
                Movement.CreateMoveToMeleeBehavior(true));
        }

        public static Composite CreateFeralPvPPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.Level < 20,
                    CreateFeralLevel1020Pull()),
                new Decorator(
                    ret =>
                    StyxWoW.Me.Level < 46 &&
                    (SingularSettings.Instance.Druid.Shapeform == 0 || SingularSettings.Instance.Druid.Shapeform == 1 ||
                     (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Cat Form"))),
                    CreateFeralLevel2046Pull()),
                new Decorator(
                    ret =>
                    SingularSettings.Instance.Druid.Shapeform == 2 ||
                    (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Dire Bear Form")),
                    CreateFeralBearPvPPull()),
                new Decorator(
                    ret =>
                    SingularSettings.Instance.Druid.Shapeform == 0 || SingularSettings.Instance.Druid.Shapeform == 1 ||
                    (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Cat Form")),
                    CreateFeralCatPvPPull())
                );
        }

        public static Composite CreateFeralPvPCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                new Decorator(
                    ret =>
                    SingularSettings.Instance.Druid.Shapeform == 2 ||
                    (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Dire Bear Form"))
                    ||
                    (SingularSettings.Instance.Druid.Shapeform == 0 &&
                     StyxWoW.Me.HealthPercent < Settings.PvPHealthSwitch)
                    ||
                    (SingularSettings.Instance.Druid.Shapeform == 0 && EnemyUnitsPvP.Count >= Settings.PvPAddSwitch)
                    ||
                    (SingularSettings.Instance.Druid.Shapeform == 0 &&
                     StyxWoW.Me.ActiveAuras.ContainsKey("Frenzied Regeneration")),
                    CreateFeralBearPvPCombat()),
                new Decorator(
                    ret =>
                    (SingularSettings.Instance.Druid.Shapeform == 0 && EnemyUnitsPvP.Count < Settings.PvPAddSwitch &&
                     StyxWoW.Me.HealthPercent >= Settings.PvPHealthSwitch) ||
                    SingularSettings.Instance.Druid.Shapeform == 1 ||
                    (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Cat Form")),
                    CreateFeralCatPvPCombat())
                );
        }

        private static Composite CreateFeralBearPvPCombat()
        {
            return new PrioritySelector(
                Common.CreateNonRestoPvPHeals(),
                Common.CreateEscapeFromCc(),
                Common.CreateCycloneAdd(),
                Spell.BuffSelf("Dire Bear Form"),
                Spell.Cast("Feral Charge - Bear",
                           ret => StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                                  StyxWoW.Me.CurrentTarget.Distance <= 23),
                // Defensive CDs are hard to 'roll' from this type of logic, so we'll simply use them more as 'oh shit' buttons, than anything.
                // Barkskin should be kept on CD, regardless of what we're tanking
                Spell.BuffSelf("Barkskin",
                               ret =>
                               (StyxWoW.Me.HealthPercent <= 40 && EnemyUnitsPvP.Count == 2) ||
                               (StyxWoW.Me.HealthPercent <= 70 && EnemyUnitsPvP.Count > 2) ||
                               (StyxWoW.Me.HealthPercent <= 30 && EnemyUnitsPvP.Count == 1)),
                // WotLK: Enrage increases damage taken by 10% but provides rage generation and King of the Jungle DPS boost.
                Spell.BuffSelf("Enrage"),
                // Only pop SI if we're taking a bunch of damage.
                Spell.BuffSelf("Survival Instincts",
                               ret => (StyxWoW.Me.HealthPercent <= 40 && EnemyUnitsPvP.Count == 2) ||
                                      (StyxWoW.Me.HealthPercent <= 50 && EnemyUnitsPvP.Count > 2) ||
                                      (StyxWoW.Me.HealthPercent <= 30 && EnemyUnitsPvP.Count == 1)),
                // We only want to pop FR < 30%. Users should not be able to change this value, as FR automatically pushes us to 30% hp.
                Spell.BuffSelf("Frenzied Regeneration",
                               ret => (StyxWoW.Me.HealthPercent <= 40 && EnemyUnitsPvP.Count == 2) ||
                                      (StyxWoW.Me.HealthPercent <= 50 && EnemyUnitsPvP.Count > 2) ||
                                      (StyxWoW.Me.HealthPercent <= 30 && EnemyUnitsPvP.Count == 1)),
                // Make sure we deal with interrupts...
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Helpers.Common.CreateAutoAttack(true),
                // If we have 3+ units not targeting us, and are within 10yds, then pop our AOE taunt. (These are ones we have 'no' threat on, or don't hold solid threat on)
                new Decorator(
                    ret =>
                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk"),
                    new PrioritySelector(
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Mangle (Bear)"),
                        Movement.CreateMoveToMeleeBehavior(true)                        )
                    ),
                new Decorator(
                    ret =>
                    EnemyUnitsPvP.Count >= 2,
                    new PrioritySelector(
                        Spell.Cast("Berserk",
                                   ret =>
                                   SpellManager.HasSpell("Berserk") &&
                                   !SpellManager.Spells["Berserk"].Cooldown && !SpellManager.GlobalCooldown 
                            ),
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Swipe (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Swipe (Bear)") &&
                                   !SpellManager.Spells["Swipe (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 25)
                            ),
                        new Decorator(
                            ret =>
                            (SpellManager.HasSpell("Swipe (Bear)") &&
                             SpellManager.Spells["Swipe (Bear)"].CooldownTimeLeft.TotalSeconds > 1)
                            ||
                            (!SpellManager.HasSpell("Swipe (Bear)")),
                            new PrioritySelector(
                                Spell.Cast("Mangle (Bear)",
                                           ret =>
                                           SpellManager.HasSpell("Mangle (Bear)") &&
                                           !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                           StyxWoW.Me.CurrentRage >=
                                           (
                                               (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                                StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                               )
                                                   ? 0
                                                   : 15) && !StyxWoW.Me.CurrentTarget.HasAura("Infected Wounds")
                                    ),
                                Spell.Cast("Mangle (Bear)",
                                           ret =>
                                           SpellManager.HasSpell("Mangle (Bear)") &&
                                           !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                           StyxWoW.Me.CurrentRage >=
                                           ((StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                             StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                            )
                                                ? 0
                                                : 15)
                                    ),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           !StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    ),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           SpellManager.HasSpell("Lacerate") &&
                                           StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                           StyxWoW.Me.CurrentTarget.Auras["Lacerate"].StackCount < 3 &&
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    ),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    )
                                )
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),
                new Decorator(
                    ret =>
                    EnemyUnitsPvP.Count < 2,
                    new PrioritySelector(
                        Spell.Cast("Berserk",
                                   ret =>
                                   !Settings.PvPBerserksafe &&
                                   SpellManager.HasSpell("Berserk") &&
                                   !SpellManager.Spells["Berserk"].Cooldown && !SpellManager.GlobalCooldown &&
                                   StyxWoW.Me.CurrentTarget.HasSunders() &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds >
                                   (TalentManager.HasGlyph("Berserk") ? 25 : 15)
                            ),
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Mangle (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Mangle (Bear)") &&
                                   !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   (
                                       (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                        StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                       )
                                           ? 0
                                           : 15) && !StyxWoW.Me.CurrentTarget.HasAura("Infected Wounds")
                            ),
                        Spell.Cast("Mangle (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Mangle (Bear)") &&
                                   !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   ((StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                     StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                    )
                                        ? 0
                                        : 15)
                            ),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   !StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   SpellManager.HasSpell("Lacerate") && StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                   StyxWoW.Me.CurrentTarget.Auras["Lacerate"].StackCount < 3 &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)
                        ))
                );
        }

        private static Composite CreateFeralCatPvPCombat()
        {
            return new PrioritySelector(
                Common.CreateNonRestoPvPHeals(),
                Common.CreateEscapeFromCc(),
                Common.CreateCycloneAdd(),
                Spell.Cast("Hibernate",
                           ret =>
                           (StyxWoW.Me.CurrentTarget.HasAura("Dire Bear Form") ||
                            StyxWoW.Me.CurrentTarget.HasAura("Cat Form")) && Settings.PvPRoot &&
                           StyxWoW.Me.CurrentTarget.Fleeing && StyxWoW.Me.CurrentTarget.IsBeast),
                Spell.Cast("Cyclone", ret => Settings.PvPRoot && StyxWoW.Me.CurrentTarget.Fleeing),
                Spell.Cast("Entangling Roots", ret => Settings.PvPRoot && StyxWoW.Me.CurrentTarget.Fleeing),
                Spell.BuffSelf("Cat Form"),
                Spell.Cast(
                    "Feral Charge - Cat",
                    ret => StyxWoW.Me.CurrentTarget.CanCharge() &&
                           StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                           StyxWoW.Me.CurrentTarget.Distance <= 23),
                Spell.Cast("Dash",
                           ret => Settings.CatRaidDash && StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange + 2f),
                Spell.BuffSelf("Barkskin", ret => (StyxWoW.Me.HealthPercent <= 40 && EnemyUnitsPvP.Count == 2) ||
                                                  (StyxWoW.Me.HealthPercent <= 70 && EnemyUnitsPvP.Count > 2) ||
                                                  (StyxWoW.Me.HealthPercent <= 30 && EnemyUnitsPvP.Count == 1)),
                Spell.BuffSelf("Survival Instincts",
                               ret => (StyxWoW.Me.HealthPercent <= 40 && EnemyUnitsPvP.Count == 2) ||
                                      (StyxWoW.Me.HealthPercent <= 70 && EnemyUnitsPvP.Count > 2) ||
                                      (StyxWoW.Me.HealthPercent <= 30 && EnemyUnitsPvP.Count == 1)),
                /*Bases on Mew!*/

                /*Tiger's Fury!*/
                // #1
                Spell.BuffSelf("Tiger's Fury",
                               ret => CurrentEnergy <= 26 &&
                                      !SpellManager.GlobalCooldown &&
                                      StyxWoW.Me.CurrentTarget.Level < 80),
                // #2
                Spell.BuffSelf("Tiger's Fury",
                               ret => CurrentEnergy <= 35 &&
                                      !StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") &&
                                      !SpellManager.GlobalCooldown &&
                                      StyxWoW.Me.CurrentTarget.Level >= 80),
                /*Berserk!*/

                //#4
                Spell.Cast("Berserk",
                           ret => ((Settings.PvPBerserksafe && EnemyUnitsPvP.Count >= 2) || !Settings.PvPBerserksafe) &&
                                  StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") && !SpellManager.GlobalCooldown),
                //#5 
                Spell.Cast("Berserk",
                           ret =>
                           ((Settings.PvPBerserksafe && EnemyUnitsPvP.Count >= 2) || !Settings.PvPBerserksafe) &&
                           !SpellManager.GlobalCooldown &&
                           SpellManager.HasSpell("Tiger's Fury") &&
                           SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds > 6),
                Helpers.Common.CreateAutoAttack(true),
                /*AOE*/
                new Decorator(
                    ret => EnemyUnitsPvP.Count >=
                           4,
                    new PrioritySelector(
                        Spell.Cast("Swipe (Cat)"),
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.HealthPercent >= 45 &&
                                   (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <= 1)
                                   )
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),
                Movement.CreateMoveBehindTargetBehavior(),
                new Decorator(
                    ret => EnemyUnitsPvP.Count <
                           4,
                    new PrioritySelector(
                //#6
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.HealthPercent >= 45 &&
                                   (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <= 1)
                                   )
                            ),
                //#7
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   !StyxWoW.Me.CurrentTarget.HasBleedDebuff() ||
                                   (StyxWoW.Me.CurrentTarget.HasMyAura("Mangle") &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Mangle", true).TotalSeconds <= 2)
                            ),
                /*Ravage!*/

                        //#8
                /* WotLK QC: Blood in the Water doesn't exist in WotLK — FB does NOT refresh Rip.
                   Below 20% use FB as execute. Above 20%, #11 maintains Rip normally. */

                        //#9 — WotLK execute: FB below 20%
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.HealthPercent <= 20
                            ),


                        //#11
                        Spell.Cast("Rip",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.HealthPercent >= 21 &&
                                          (!StyxWoW.Me.CurrentTarget.HasMyAura("Rip") ||
                                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 2.0)
                                          )
                            ),
                //#12       
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          CurrentEnergy >= 12 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 5 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 3),
                //#13
                        Spell.Cast("Rake",
                // ReSharper disable PossibleLossOfFraction
                                   ret => // ReSharper restore PossibleLossOfFraction
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                                   StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                   !IsrakeTfed),
                /*	if (timeToDie >= 8.5 && (!isRakeUp || rakeRemaining < 3.0) && (isBerserkUp || rakeRemaining - 0.8 <= tigersFuryCD || energy >= 71))
return Action.RAKE;*/
                //#14
                        Spell.Cast("Rake",
                // ReSharper disable PossibleLossOfFraction
                                   ret => // ReSharper restore PossibleLossOfFraction
                                   (!StyxWoW.Me.CurrentTarget.HasMyAura("Rake") ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3.0)
                                   ) &&
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                     SpellManager.HasSpell("Tiger's Fury") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds <=
                                     SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds) ||
                                    CurrentEnergy >= 71)
                            ),
                //#15
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")
                            ),
                //#16
                //#new

                        //#18
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.HealthPercent <= 20
                                          && CurrentEnergy >= 60),
                //#19
                        Spell.Cast("Ferocious Bite",
                                   ret => (!StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                           CurrentEnergy < 25) &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds >= 8 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 4 &&
                                          StyxWoW.Me.CurrentTarget.Level < 80),
                        Spell.Cast("Ferocious Bite",
                                   ret => (!StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                           CurrentEnergy < 111 /*25*/) &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds >= 12 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 10 &&
                                          StyxWoW.Me.CurrentTarget.Level >= 80),
                //#20 
                //#21
                //Ignore 4x T11 Bonus
                //#22
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") ||
                                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                   )
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") ||
                                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                   )
                            ),
                //#23
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ComboPoints < 5 &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 3)
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ComboPoints < 5 &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).
                                        TotalSeconds <= 3)
                            ),
                //#24
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   SpellManager.HasSpell("Tiger's Fury") &&
                                   SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds <= 3),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   SpellManager.HasSpell("Tiger's Fury") &&
                                   SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().
                                       TotalSeconds <= 3),
                //#25
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                       // ReSharper disable PossibleLossOfFraction
                                   StyxWoW.Me.CurrentTarget.HealthPercent <= 44),
                // ReSharper restore PossibleLossOfFraction
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                       // ReSharper disable PossibleLossOfFraction
                                   StyxWoW.Me.CurrentTarget.HealthPercent <= 44),
                // ReSharper restore PossibleLossOfFraction
                //#26
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   CurrentEnergy >= (EnergyRegen)
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   CurrentEnergy >= (EnergyRegen)
                            )
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        #endregion

        #region Instance Rotation
        // Start of ILoveAnimals Instance Rotation
        public static Composite CreateFeralInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(ret => Settings.Interrupt,
                              Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget)),
                new Decorator(
                    ret =>
                    (SingularSettings.Instance.Druid.Shapeform == 0 && Group.MeIsTank)
                    || SingularSettings.Instance.Druid.Shapeform == 2
                    || (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Dire Bear Form")),
                    CreateFeralBearInstanceCombat()),
                new Decorator(
                    ret =>
                    (SingularSettings.Instance.Druid.Shapeform == 1)
                    || (SingularSettings.Instance.Druid.Shapeform == 0 && !Group.MeIsTank)
                    || (SingularSettings.Instance.Druid.Shapeform == 3 && StyxWoW.Me.HasAura("Cat Form")),
                    CreateFeralCatInstanceCombat())
                );
        }

        private static Composite CreateFeralBearInstanceCombat()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Dire Bear Form"),
                Spell.Cast("Feral Charge - Bear",
                           ret => StyxWoW.Me.CurrentTarget.CanCharge() &&
                                  Settings.BearRaidUseFeralCharge && StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                                  StyxWoW.Me.CurrentTarget.Distance <= 23),
                // Defensive CDs are hard to 'roll' from this type of logic, so we'll simply use them more as 'oh shit' buttons, than anything.
                // Barkskin should be kept on CD, regardless of what we're tanking
                Spell.BuffSelf("Barkskin",
                               ret =>
                               SingularSettings.Instance.Druid.BearRaidCooldown &&
                               StyxWoW.Me.HealthPercent < Settings.FeralBarkskin),
                // WotLK: Enrage increases damage taken by 10% but provides rage generation and King of the Jungle DPS boost.
                Spell.BuffSelf("Enrage"),
                // Only pop SI if we're taking a bunch of damage.
                Spell.BuffSelf("Survival Instincts",
                               ret =>
                               SingularSettings.Instance.Druid.BearRaidCooldown &&
                               StyxWoW.Me.HealthPercent < Settings.SurvivalInstinctsHealth),
                // We only want to pop FR < 30%. Users should not be able to change this value, as FR automatically pushes us to 30% hp.
                Spell.BuffSelf("Frenzied Regeneration",
                               ret =>
                               SingularSettings.Instance.Druid.BearRaidCooldown &&
                               StyxWoW.Me.HealthPercent < Settings.FrenziedRegenerationHealth),
                // Make sure we deal with interrupts...
                new Decorator(ret => Settings.Interrupt,
                              Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget)),
                Helpers.Common.CreateAutoAttack(true),
                // If we have 3+ units not targeting us, and are within 10yds, then pop our AOE taunt. (These are ones we have 'no' threat on, or don't hold solid threat on)
                Spell.Cast(
                    "Challenging Roar", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                    ret =>
                    SingularSettings.Instance.EnableTaunting &&
                    TankManager.Instance.NeedToTaunt.Count(u => u.Distance <= 10) >= 3),
                // If there's a unit that needs taunting, do it.
                Spell.Cast(
                    "Growl", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                    ret => SingularSettings.Instance.EnableTaunting),
                new Decorator(ret => SingularSettings.Instance.Druid.BearRaidBerserkFun
                                     && StyxWoW.Me.ActiveAuras.ContainsKey("Berserk"),
                              new PrioritySelector(
                                  Spell.Cast("Maul",
                                             ret =>
                                             SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                             StyxWoW.Me.CurrentRage > 45),
                                  Spell.Cast("Mangle (Bear)"))),
                new Decorator(
                    ret =>
                    EnemyUnits.Count >= 2 &&
                    EnemyUnits.Count <= 3 &&
                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk"),
                    new PrioritySelector(
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Demoralizing Roar",
                                   ret =>
                                   Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasDemoralizing()
                                       )
                            ),
                        Spell.Cast("Mangle (Bear)"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),
                new Decorator(
                    ret =>
                    EnemyUnits.Count >= SingularSettings.Instance.Druid.BearRaidAoe,
                    new PrioritySelector(
                        Spell.Cast("Berserk",
                                   ret =>
                                   SingularSettings.Instance.Druid.BearRaidBerserk &&
                                   SpellManager.HasSpell("Berserk") &&
                                   !SpellManager.Spells["Berserk"].Cooldown && !SpellManager.GlobalCooldown &&
                                   StyxWoW.Me.CurrentTarget.HasSunders() &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds >
                                   (TalentManager.HasGlyph("Berserk") ? 25 : 15)
                            ),
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Swipe (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Swipe (Bear)") &&
                                   !SpellManager.Spells["Swipe (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 25)
                            ),
                        Spell.Cast("Demoralizing Roar",
                                   ret =>
                                   Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasDemoralizing()
                                       )
                            ),
                        new Decorator(
                            ret =>
                            (SpellManager.HasSpell("Swipe (Bear)") &&
                             SpellManager.Spells["Swipe (Bear)"].CooldownTimeLeft.TotalSeconds > 1)
                            ||
                            (!SpellManager.HasSpell("Swipe (Bear)")),
                            new PrioritySelector(
                                Spell.Cast("Mangle (Bear)",
                                           ret =>
                                           SpellManager.HasSpell("Mangle (Bear)") &&
                                           !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                           StyxWoW.Me.CurrentRage >=
                                           (
                                               (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                                StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                               )
                                                   ? 0
                                                   : 15) && !StyxWoW.Me.CurrentTarget.HasAura("Infected Wounds")
                                    ),
                                Spell.Cast("Faerie Fire (Feral)",
                                           ret =>
                                           SpellManager.HasSpell("Faerie Fire (Feral)") &&
                                           !SpellManager.Spells["Faerie Fire (Feral)"].Cooldown &&
                                           (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                            (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                             StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <=
                                             1.5)
                                           )
                                    ),
                                Spell.Cast("Mangle (Bear)",
                                           ret =>
                                           SpellManager.HasSpell("Mangle (Bear)") &&
                                           !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                           StyxWoW.Me.CurrentRage >=
                                           ((StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                             StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                            )
                                                ? 0
                                                : 15)
                                    ),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           !StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    ),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           SpellManager.HasSpell("Lacerate") &&
                                           StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                           StyxWoW.Me.CurrentTarget.Auras["Lacerate"].StackCount < 3 &&
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    ),
                                Spell.Cast("Faerie Fire (Feral)",
                                           ret =>
                                           SpellManager.HasSpell("Faerie Fire (Feral)") &&
                                           !SpellManager.Spells["Faerie Fire (Feral)"].Cooldown),
                                Spell.Cast("Lacerate",
                                           ret =>
                                           StyxWoW.Me.CurrentRage >=
                                           (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                                    )
                                )
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),
                new Decorator(
                    ret =>
                    EnemyUnits.Count < SingularSettings.Instance.Druid.BearRaidAoe,
                    new PrioritySelector(
                        Spell.Cast("Berserk",
                                   ret =>
                                   SingularSettings.Instance.Druid.BearRaidBerserk &&
                                   SpellManager.HasSpell("Berserk") &&
                                   !SpellManager.Spells["Berserk"].Cooldown && !SpellManager.GlobalCooldown &&
                                   StyxWoW.Me.CurrentTarget.HasSunders() &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds >
                                   (TalentManager.HasGlyph("Berserk") ? 25 : 15)
                            ),
                        Spell.Cast("Maul",
                                   ret =>
                                   SpellManager.HasSpell("Maul") && !SpellManager.Spells["Maul"].Cooldown &&
                                   StyxWoW.Me.CurrentRage > 45),
                        Spell.Cast("Demoralizing Roar",
                                   ret =>
                                   Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 10 && !u.HasDemoralizing()
                                       )
                            ),
                        Spell.Cast("Mangle (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Mangle (Bear)") &&
                                   !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   (
                                       (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                        StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                       )
                                           ? 0
                                           : 15) && !StyxWoW.Me.CurrentTarget.HasAura("Infected Wounds")
                            ),
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   SpellManager.HasSpell("Faerie Fire (Feral)") &&
                                   !SpellManager.Spells["Faerie Fire (Feral)"].Cooldown &&
                                   (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <= 1.5)
                                   )
                            ),
                        Spell.Cast("Mangle (Bear)",
                                   ret =>
                                   SpellManager.HasSpell("Mangle (Bear)") &&
                                   !SpellManager.Spells["Mangle (Bear)"].Cooldown &&
                                   StyxWoW.Me.CurrentRage >=
                                   ((StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                     StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                    )
                                        ? 0
                                        : 15)
                            ),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   !StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   SpellManager.HasSpell("Lacerate") && StyxWoW.Me.CurrentTarget.HasMyAura("Lacerate") &&
                                   StyxWoW.Me.CurrentTarget.Auras["Lacerate"].StackCount < 3 &&
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   SpellManager.HasSpell("Faerie Fire (Feral)") &&
                                   !SpellManager.Spells["Faerie Fire (Feral)"].Cooldown),
                        Spell.Cast("Lacerate",
                                   ret =>
                                   StyxWoW.Me.CurrentRage >=
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") ? 0 : 15)
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)))
                );
        }

        private static Composite CreateFeralCatInstanceCombat()
        {
            return new PrioritySelector(
                Common.CreateRaidCatHeal(),
                Spell.BuffSelf("Cat Form"),
                Spell.Cast(
                    "Feral Charge - Cat",
                    ret => StyxWoW.Me.CurrentTarget.CanCharge() &&
                           Settings.CatRaidUseFeralCharge && StyxWoW.Me.CurrentTarget.Distance >= 10 &&
                           StyxWoW.Me.CurrentTarget.Distance <= 23),
                Spell.Cast("Dash",
                           ret => Settings.CatRaidDash && StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange + 2f),
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent < Settings.FeralBarkskin),
                Spell.BuffSelf("Survival Instincts", ret => StyxWoW.Me.HealthPercent < Settings.SurvivalInstinctsHealth),
                /*Bases on Mew!*/
                new Decorator(
                    ret =>
                    Settings.CatRaidWarning && !StyxWoW.Me.CurrentTarget.MeIsBehind && StyxWoW.Me.CurrentTarget.IsBoss() &&
                    !StyxWoW.Me.CurrentTarget.CanShredBoss(),
                    new Action(delegate
                    {
                        RaidWarning();
                        return RunStatus.Failure;
                    })),
                new Decorator(
                    ret =>
                    (StyxWoW.Me.IsInRaid || StyxWoW.Me.IsInParty) && StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 89 &&
                    Settings.RaidCatProwl,
                    Spell.Cast("Cower")),
                /*Tiger's Fury!*/
                // #1
                Spell.BuffSelf("Tiger's Fury",
                               ret => SingularSettings.Instance.Druid.CatRaidTigers && CurrentEnergy <= 26 &&
                                      !SpellManager.GlobalCooldown &&
                                      StyxWoW.Me.CurrentTarget.Level < 80),
                // #2
                Spell.BuffSelf("Tiger's Fury",
                               ret => SingularSettings.Instance.Druid.CatRaidTigers && CurrentEnergy <= 35 &&
                                      !StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting") &&
                                      !SpellManager.GlobalCooldown &&
                                      StyxWoW.Me.CurrentTarget.Level >= 80),
                /*Berserk!*/

                //#4
                Spell.Cast("Berserk",
                           ret => SingularSettings.Instance.Druid.CatRaidBerserk &&
                                  StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") && !SpellManager.GlobalCooldown),
                //#5 
                Spell.Cast("Berserk",
                           ret => SingularSettings.Instance.Druid.CatRaidBerserk && !SpellManager.GlobalCooldown &&
                                  StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth < 25 &&
                                  SpellManager.HasSpell("Tiger's Fury") &&
                                  SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds > 6),
                Helpers.Common.CreateAutoAttack(true),
                /*AOE*/
                new Decorator(
                    ret => EnemyUnits.Count >=
                           SingularSettings.Instance.Druid.CatRaidAoe,
                    new PrioritySelector(
                        Spell.Cast("Swipe (Cat)"),
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.IsBoss() &&
                                   (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <= 1)
                                   )
                            ),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),
                Movement.CreateMoveBehindTargetBehavior(),
                new Decorator(
                    ret => EnemyUnits.Count <
                           SingularSettings.Instance.Druid.CatRaidAoe,
                    new PrioritySelector(
                //#6
                        Spell.Cast("Faerie Fire (Feral)",
                                   ret =>
                                   StyxWoW.Me.CurrentTarget.IsBoss() &&
                                   (!StyxWoW.Me.CurrentTarget.HasSunders() ||
                                    (StyxWoW.Me.CurrentTarget.HasMyAura("Faerie Fire") &&
                                     StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Faerie Fire", true).TotalSeconds <= 1)
                                   )
                            ),
                //#7
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   !StyxWoW.Me.CurrentTarget.HasBleedDebuff() ||
                                   (StyxWoW.Me.CurrentTarget.HasMyAura("Mangle") &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Mangle", true).TotalSeconds <= 2)
                            ),
                /*Ravage!*/

                        //#8
                /* WotLK QC: Blood in the Water doesn't exist in WotLK — FB does NOT refresh Rip.
                   Below 20% use FB as execute. Above that, #11 maintains Rip normally. */

                        //#9 — WotLK execute: FB below 20%
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.HealthPercent <= 20
                            ),


                        //#11
                        Spell.Cast("Rip",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth >= 6 &&
                                          SpellManager.HasSpell("Tiger's Fury") &&
                                          (!StyxWoW.Me.CurrentTarget.HasMyAura("Rip") ||
                                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 2.0)
                                          )
                                          && (StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                              StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds + 1.9 <=
                                              SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds)
                            ),
                //#12       
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          CurrentEnergy >= 12 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 5 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 3),
                //#13
                        Spell.Cast("Rake",
                // ReSharper disable PossibleLossOfFraction
                                   ret => StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth >= 8.5 &&
                                       // ReSharper restore PossibleLossOfFraction
                                          StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") &&
                                          (
                                              (StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                               !IsrakeTfed) || !StyxWoW.Me.CurrentTarget.HasMyAura("Rake")
                                          )
                            ),

                //#14
                        Spell.Cast("Rake",
                // ReSharper disable PossibleLossOfFraction
                                   ret => StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth >= 8.5 &&
                                       // ReSharper restore PossibleLossOfFraction
                                          (!StyxWoW.Me.CurrentTarget.HasMyAura("Rake") ||
                                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3.0)
                                          ) &&
                                          (StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                           (StyxWoW.Me.CurrentTarget.HasMyAura("Rake") &&
                                            SpellManager.HasSpell("Tiger's Fury") &&
                                            StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds <=
                                            SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds) ||
                                           CurrentEnergy >= 71)
                            ),
                //#15
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   StyxWoW.Me.ActiveAuras.ContainsKey("Clearcasting")
                            ),
                //#16
                        Spell.BuffSelf("Savage Roar",
                                       ret => StyxWoW.Me.ComboPoints > 0 &&
                                              (!StyxWoW.Me.ActiveAuras.ContainsKey("Savage Roar") ||
                                               StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds <= 2)
                            ),
                //#17
                //#new
                        Spell.BuffSelf("Savage Roar",
                                       ret => StyxWoW.Me.ComboPoints == 5 &&
                                              StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth >= 9 &&
                                              StyxWoW.Me.CurrentTarget.HasMyAura("Rip") &&
                                              StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 12
                                              &&
                                              (StyxWoW.Me.ActiveAuras.ContainsKey("Savage Roar") &&
                                               StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds
                                               <= StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds + 6)
                            ),
                //#18
                        Spell.Cast("Ferocious Bite",
                                   ret => StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth <= 7),
                //#19
                        Spell.Cast("Ferocious Bite",
                                   ret => (!StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                           CurrentEnergy < 25) &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds >= 8 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 4 &&
                                          StyxWoW.Me.CurrentTarget.Level < 80),
                        Spell.Cast("Ferocious Bite",
                                   ret => (!StyxWoW.Me.ActiveAuras.ContainsKey("Berserk") ||
                                           CurrentEnergy < 111 /*25*/) &&
                                          StyxWoW.Me.ComboPoints == 5 &&
                                          StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds >= 12 &&
                                          StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds >= 10 &&
                                          StyxWoW.Me.CurrentTarget.Level >= 80),
                //#20 
                //#21
                //Ignore 4x T11 Bonus
                //#22
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") ||
                                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                   )
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ActiveAuras.ContainsKey("Tiger's Fury") ||
                                    StyxWoW.Me.ActiveAuras.ContainsKey("Berserk")
                                   )
                            ),
                //#23
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ComboPoints < 5 &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds <= 3)
                                   ||
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ComboPoints == 0 &&
                                    StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds <= 2)
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   (StyxWoW.Me.ComboPoints < 5 &&
                                    StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).
                                        TotalSeconds <= 3)
                                   ||
                                   (StyxWoW.Me.ComboPoints == 0 &&
                                    StyxWoW.Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds <=
                                    2)
                            ),
                //#24
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   SpellManager.HasSpell("Tiger's Fury") &&
                                   SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().TotalSeconds <= 3),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   SpellManager.HasSpell("Tiger's Fury") &&
                                   SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft().
                                       TotalSeconds <= 3),
                //#25
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                       // ReSharper disable PossibleLossOfFraction
                                   StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth <= 8.5),
                // ReSharper restore PossibleLossOfFraction
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                       // ReSharper disable PossibleLossOfFraction
                                   StyxWoW.Me.CurrentTarget.CurrentHealth / Finisherhealth <= 8.5),
                // ReSharper restore PossibleLossOfFraction
                //#26
                        Spell.Cast("Shred",
                                   ret =>
                                   (StyxWoW.Me.CurrentTarget.MeIsBehind || StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   CurrentEnergy >= (EnergyRegen)
                            ),
                        Spell.Cast("Mangle (Cat)",
                                   ret =>
                                   (!StyxWoW.Me.CurrentTarget.MeIsBehind && !StyxWoW.Me.CurrentTarget.CanShredBoss()) &&
                                   CurrentEnergy >= (EnergyRegen)
                            )
                        )
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }



        #endregion

        #region ILoveAnimals Misc
        /* Credits to Shaddar! */
        private static Stopwatch raidWarningSW = new Stopwatch();
        private static void RaidWarning()
        {
            if (!raidWarningSW.IsRunning)
                raidWarningSW.Start();
            if (raidWarningSW.ElapsedMilliseconds >= 6000)
            {
                Lua.DoString(
                    "RaidNotice_AddMessage(RaidWarningFrame, \"YOU ARE NOT BEHIND THE BOSS!!\", ChatTypeInfo[\"RAID_WARNING\"]);");
                raidWarningSW.Reset();
            }
        }

        public static void MoveBehind(WoWUnit Unit)
        {
            WoWPoint behindLocation = WoWMathHelper.CalculatePointBehind(Unit.Location, Unit.Rotation, 2.3f);
            Navigator.MoveTo(behindLocation);
        }
        #endregion

    }

    #region Boss Extentions from ILoveAnimals
    static class DruidBossExts
    {
        private static readonly HashSet<uint> _shred = new HashSet<uint>
            {
                // No WotLK bosses currently require special shred-from-front handling
            };

        private static readonly HashSet<uint> _charge = new HashSet<uint>
            {
                // No WotLK bosses currently require charge restriction
            };

        public static bool CanShredBoss(this WoWUnit unit)
        {
            return _shred.Contains(unit.Entry);
        }

        public static bool CanCharge(this WoWUnit unit)
        {
            return !_charge.Contains(unit.Entry);
        }
    }
    #endregion
}
