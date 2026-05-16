using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using CommonBehaviors.Actions;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Shaman
{
    internal static class Totems
    {
        public static Composite CreateSetTotems()
        {
            if (SingularSettings.Instance.Shaman.DisableTotems)
                return new ActionAlwaysFail();

            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.Mounted && !SpellManager.HasSpell("Call of the Elements"),
                    new PrioritySelector(
                        // Earth totem: match by slot type (reliable), check Active state via GetTotemInfo Lua
                        new PrioritySelector(
                            ctx => StyxWoW.Me.Totems.FirstOrDefault(t => t.Type == WoWTotemType.Earth),
                            new Decorator(
                                // WotLK fix: .Active stays true even after a totem expires naturally.
                                // Correct check is whether the totem unit object still exists in the world.
                                ret => GetEarthTotem() != WoWTotem.None && (ret == null || ((WoWTotemInfo)ret).Unit == null),
                                new Sequence(
                                    new Action(ret => Logger.Write("Casting {0} Totem", GetEarthTotem().ToString().CamelToSpaced())),
                                    new Action(ret => SpellManager.CastSpellById(GetEarthTotem().GetTotemSpellId()))))),
                        // Air totem: match by slot type
                        new PrioritySelector(
                            ctx => StyxWoW.Me.Totems.FirstOrDefault(t => t.Type == WoWTotemType.Air),
                            new Decorator(
                                // WotLK fix: same as Earth — use .Unit == null instead of !.Active
                                ret => GetAirTotem() != WoWTotem.None && (ret == null || ((WoWTotemInfo)ret).Unit == null),
                                new Sequence(
                                    new Action(ret => Logger.Write("Casting {0} Totem", GetAirTotem().ToString().CamelToSpaced())),
                                    new Action(ret => SpellManager.CastSpellById(GetAirTotem().GetTotemSpellId()))))),
                        // Water totem: match by slot type
                        new PrioritySelector(
                            ctx => StyxWoW.Me.Totems.FirstOrDefault(t => t.Type == WoWTotemType.Water),
                            new Decorator(
                                // WotLK fix: same as Earth/Air — use .Unit == null instead of !.Active
                                ret => GetWaterTotem() != WoWTotem.None && (ret == null || ((WoWTotemInfo)ret).Unit == null),
                                new Sequence(
                                    new Action(ret => Logger.Write("Casting {0} Totem", GetWaterTotem().ToString().CamelToSpaced())),
                                    new Action(ret => SpellManager.CastSpellById(GetWaterTotem().GetTotemSpellId())))))
                        )),
                new Decorator(
                    ret =>
                        {
                            // Hell yeah this is long, but its all clear to read
                            // Can't cast totems while mounted — prevent 6s retry spam during flight.
                            if (StyxWoW.Me.Mounted)
                                return false;

                            if (!SpellManager.HasSpell("Call of the Elements"))
                                return false;

                            var bestAirTotem = GetAirTotem();
                            var currentAirTotem = StyxWoW.Me.Totems.FirstOrDefault(t => t.WoWTotem == bestAirTotem);

                            if (currentAirTotem == null)
                            {
                                return true;
                            }

                            var airTotemAsUnit = currentAirTotem.Unit;

                            if (airTotemAsUnit == null)
                            {
                                return true;
                            }

                            if (airTotemAsUnit.Distance > GetTotemRange(bestAirTotem))
                            {
                                return true;
                            }

                            var bestEarthTotem = GetEarthTotem();
                            var currentEarthTotem = StyxWoW.Me.Totems.FirstOrDefault(t => t.WoWTotem == bestEarthTotem);

                            if (currentEarthTotem == null)
                            {
                                return true;
                            }

                            var earthTotemAsUnit = currentEarthTotem.Unit;

                            if (earthTotemAsUnit == null)
                            {
                                return true;
                            }

                            if (earthTotemAsUnit.Distance > GetTotemRange(bestEarthTotem))
                            {
                                return true;
                            }

                            var bestWaterTotem = GetWaterTotem();
                            var currentWaterTotem = StyxWoW.Me.Totems.FirstOrDefault(t => t.WoWTotem == bestWaterTotem);

                            if (currentWaterTotem == null)
                            {
                                return true;
                            }

                            var waterTotemAsUnit = currentWaterTotem.Unit;

                            if (waterTotemAsUnit == null)
                            {
                                return true;
                            }

                            if (waterTotemAsUnit.Distance > GetTotemRange(bestWaterTotem))
                            {
                                return true;
                            }

                            return false;
                        },
                    new Sequence(
                        new Action(ret => SetupTotemBar()),
                        new Decorator(
                            ret => (System.DateTime.UtcNow - _lastCallOfElementsAttempt) > CallElementsCooldown,
                            new Sequence(
                                new Action(ret => { _lastCallOfElementsAttempt = System.DateTime.UtcNow; return RunStatus.Success; }),
                                Spell.BuffSelf("Call of the Elements")
                            )
                        )))
                            
                );
        }

        public static void SetupTotemBar()
        {
            // If the user has given specific totems to use, then use them. Otherwise, fall back to our automagical ones
            WoWTotem earth = SingularSettings.Instance.Shaman.EarthTotem;
            WoWTotem air = SingularSettings.Instance.Shaman.AirTotem;
            WoWTotem water = SingularSettings.Instance.Shaman.WaterTotem;
            WoWTotem fire = SingularSettings.Instance.Shaman.FireTotem;

            SetTotemBarSlot(MultiCastSlot.ElementsEarth, earth != WoWTotem.None ? earth : GetEarthTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsAir, air != WoWTotem.None ? air : GetAirTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsWater, water != WoWTotem.None ? water : GetWaterTotem());
            SetTotemBarSlot(MultiCastSlot.ElementsFire, fire != WoWTotem.None ? fire : GetFireTotem());
        }

        /// <summary>
        /// Returns the best fire totem to use for the current situation.
        /// Logic mirrors GetEarth/GetAir/GetWater style: prefer raid/group buffs when appropriate,
        /// fall back to single-target damage totems when solo. Uses TotemIsKnown() so it is safe.
        /// </summary>
        public static WoWTotem GetFireTotem()
        {
            LocalPlayer me = StyxWoW.Me;
            bool isEnhance = TalentManager.CurrentSpec == TalentSpec.EnhancementShaman;
            bool isElemental = TalentManager.CurrentSpec == TalentSpec.ElementalShaman;

            // Restoration shamans seldom want a fire totem.
            if (TalentManager.CurrentSpec == TalentSpec.RestorationShaman)
            {
                return WoWTotem.None;
            }

            // Solo play: prefer single-target damage totems (Searing), then Magma, then Flametongue for minor utility
            if (!me.IsInParty && !me.IsInRaid)
            {
                if (TotemIsKnown(WoWTotem.Searing))
                    return WoWTotem.Searing;
                if (TotemIsKnown(WoWTotem.Magma))
                    return WoWTotem.Magma;
                if (TotemIsKnown(WoWTotem.Flametongue))
                    return WoWTotem.Flametongue;
                if (TotemIsKnown(WoWTotem.TotemOfWrath))
                    return WoWTotem.TotemOfWrath;

                return WoWTotem.None;
            }

            // In group/raid: prefer Totem of Wrath (party DPS buff), then Magma for AoE, then Searing for single-target
            if (TotemIsKnown(WoWTotem.TotemOfWrath))
                return WoWTotem.TotemOfWrath;
            if (TotemIsKnown(WoWTotem.Magma))
                return WoWTotem.Magma;
            if (TotemIsKnown(WoWTotem.Searing))
                return WoWTotem.Searing;
            if (TotemIsKnown(WoWTotem.Flametongue))
                return WoWTotem.Flametongue;

            return WoWTotem.None;
        }

        /// <summary>
        ///   Recalls any currently 'out' totems. This will use Totemic Recall if its known, otherwise it will destroy each totem one by one.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        public static Composite CreateRecallTotems()
        {
            if (SingularSettings.Instance.Shaman.DisableTotems)
                return new ActionAlwaysFail();

            // Throttle(2): allow at most 1 recall-cast success per 2-second window.
            // Prevents spam when ObjectManager hasn't removed the totem units yet after Totemic Recall fires.
            // Pattern from Singular 6.X.X Totems.cs CreateRecallTotems().
            return new Throttle(2, new Action(r => RecallTotems() ? RunStatus.Success : RunStatus.Failure));
        }

        public static bool RecallTotems()
        {
            if (!NeedToRecallTotems)
                return false;

            Logger.Write("Recalling totems!");
            if (SpellManager.HasSpell("Totemic Recall"))
            {
                SpellManager.Cast("Totemic Recall");
                return true;
            }

            WoWTotemInfo[] totems = StyxWoW.Me.Totems.ToArray();
            foreach (WoWTotemInfo t in totems)
            {
                if (t != null && t.Unit != null)
                {
                    DestroyTotem(t.Type);
                }
            }

            return true;
        }

        /// <summary>
        ///   Destroys the totem described by type.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "type">The type.</param>
        public static void DestroyTotem(WoWTotemType type)
        {
            if (type == WoWTotemType.None)
            {
                return;
            }

            Lua.DoString("DestroyTotem({0})", (int)type);
        }

        /// <summary>
        ///   Sets a totem bar slot to the specified totem!.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "slot">The slot.</param>
        /// <param name = "totem">The totem.</param>
        public static void SetTotemBarSlot(MultiCastSlot slot, WoWTotem totem)
        {
            // Make sure we have the totem bars to set. Highest first kthx
            if (slot >= MultiCastSlot.SpiritsFire && !SpellManager.HasSpell("Call of the Spirits"))
            {
                return;
            }
            if (slot >= MultiCastSlot.AncestorsFire && !SpellManager.HasSpell("Call of the Ancestors"))
            {
                return;
            }
            if (!SpellManager.HasSpell("Call of the Elements"))
            {
                return;
            }

            if (LastSetTotems.ContainsKey(slot) && LastSetTotems[slot] == totem)
            {
                return;
            }

            if (!LastSetTotems.ContainsKey(slot))
            {
                LastSetTotems.Add(slot, totem);
            }
            else
            {
                LastSetTotems[slot] = totem;
            }

            Logger.Write("Setting totem slot Call of the" + slot.ToString().CamelToSpaced() + " to " + totem.ToString().CamelToSpaced());

            Lua.DoString("SetMultiCastSpell({0}, {1})", (int)slot, totem.GetTotemSpellId());
        }

        private static System.DateTime _lastCallOfElementsAttempt = System.DateTime.MinValue;
        private static readonly System.TimeSpan CallElementsCooldown = System.TimeSpan.FromSeconds(6);
        private static readonly Dictionary<MultiCastSlot, WoWTotem> LastSetTotems = new Dictionary<MultiCastSlot, WoWTotem>();

        private static WoWTotem GetEarthTotem()
        {
            LocalPlayer me = StyxWoW.Me;
            bool isEnhance = TalentManager.CurrentSpec == TalentSpec.EnhancementShaman;
            // Solo play
            if (!me.IsInParty && !me.IsInRaid)
            {
                // Enhance, lowbie
                if (isEnhance || TalentManager.CurrentSpec == TalentSpec.Lowbie)
                {
                    if (TotemIsKnown(WoWTotem.StrengthOfEarth))
                    {
                        return WoWTotem.StrengthOfEarth;
                    }

                    return WoWTotem.None;
                }

                // Ele, resto
                if (TotemIsKnown(WoWTotem.Stoneskin))
                {
                    return WoWTotem.Stoneskin;
                }

                return WoWTotem.None;
            }

            // Raids and stuff

            // Enhance
            if (isEnhance)
            {
                if (TotemIsKnown(WoWTotem.StrengthOfEarth))
                {
                    return WoWTotem.StrengthOfEarth;
                }

                return WoWTotem.None;
            }

            if (TotemIsKnown(WoWTotem.Stoneskin))
            {
                return WoWTotem.Stoneskin;
            }

            if (TotemIsKnown(WoWTotem.StrengthOfEarth))
            {
                return WoWTotem.StrengthOfEarth;
            }

            return WoWTotem.None;
        }

        public static WoWTotem GetAirTotem()
        {
            if (TalentManager.CurrentSpec == TalentSpec.Lowbie)
            {
                return WoWTotem.None;
            }

            LocalPlayer me = StyxWoW.Me;
            bool isEnhance = TalentManager.CurrentSpec == TalentSpec.EnhancementShaman;
            
            if (!me.IsInParty && !me.IsInRaid)
            {
                if (isEnhance)
                {
                    if (TotemIsKnown(WoWTotem.Windfury))
                    {
                        return WoWTotem.Windfury;
                    }

                    return WoWTotem.None;
                }

                if (TotemIsKnown(WoWTotem.WrathOfAir))
                {
                    return WoWTotem.WrathOfAir;
                }

                return WoWTotem.None;
            }

            // WotLK QC: In WotLK, Moonkin Aura provides 5% spell crit, NOT 5% spell haste
            // It does NOT overlap with Wrath of Air Totem (5% spell haste). Keep Wrath of Air even with Moonkin present.
            // Cata changed Moonkin Aura to spell haste, causing the overlap — that logic is wrong for WotLK.
            // if (StyxWoW.Me.RaidMembers.Any(p => p.Class == WoWClass.Druid && p.Shapeshift == ShapeshiftForm.Moonkin) ||
            //     StyxWoW.Me.PartyMembers.Any(p => p.Class == WoWClass.Druid && p.Shapeshift == ShapeshiftForm.Moonkin))
            // {
            //     if (TotemIsKnown(WoWTotem.Windfury))
            //     {
            //         return WoWTotem.Windfury;
            //     }
            // }

            if (!isEnhance && TotemIsKnown(WoWTotem.WrathOfAir))
            {
                return WoWTotem.WrathOfAir;
            }

            if (TotemIsKnown(WoWTotem.Windfury))
            {
                return WoWTotem.Windfury;
            }

            return WoWTotem.None;
        }
        
        public static WoWTotem GetWaterTotem()
        {
            // Plain and simple. If we're resto, we never want a different water totem out. Thats all there is to it.
            if (TalentManager.CurrentSpec == TalentSpec.RestorationShaman)
            {
                if (TotemIsKnown(WoWTotem.HealingStream))
                {
                    return WoWTotem.HealingStream;
                }

                return WoWTotem.None;
            }

            // Solo play. Only healing stream
            if (!StyxWoW.Me.IsInParty && !StyxWoW.Me.IsInRaid)
            {
                if (TotemIsKnown(WoWTotem.HealingStream))
                {
                    return WoWTotem.HealingStream;
                }

                return WoWTotem.None;
            }

            // WotLK QC: In WotLK, Blessing of Might provides AP only (no mp5).
            // The mp5 blessing is "Blessing of Wisdom" — that's what overlaps with Mana Spring Totem.
            if (!StyxWoW.Me.HasAura("Blessing of Wisdom") && TotemIsKnown(WoWTotem.ManaSpring))
            {
                return WoWTotem.ManaSpring;
            }

            // ... yea
            if (TotemIsKnown(WoWTotem.HealingStream))
            {
                return WoWTotem.HealingStream;
            }

            return WoWTotem.None;
        }

        #region Helper shit

        public static bool TotemsDisabled => SingularSettings.Instance.Shaman.DisableTotems;
        public static bool NeedToRecallTotems { get { return TotemsInRange == 0 && StyxWoW.Me.Totems.Count(t => t.Unit != null) != 0; } }
        public static int TotemsInRange { get { return TotemsInRangeOf(StyxWoW.Me); } }

        public static int TotemsInRangeOf(WoWUnit unit)
        {
            return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.WoWTotem));
        }

        public static bool TotemIsKnown(WoWTotem totem)
        {
            return SpellManager.HasSpell(totem.GetTotemSpellId());
        }

        /// <summary>
        ///   Finds the max range of a specific totem, where you'll still receive the buff.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "totem">The totem.</param>
        /// <returns>The calculated totem range.</returns>
        public static float GetTotemRange(WoWTotem totem)
        {
            // WotLK: Totemic Reach doesn't exist (Cata 4.0.1). No totem range talent in WotLK.
            // (2, 7) = Improved Shields in WotLK Enhancement tree, not Totemic Reach.
            float talentFactor = 1f;

            switch (totem)
            {
                case WoWTotem.Flametongue:
                case WoWTotem.Stoneskin:
                case WoWTotem.StrengthOfEarth:
                case WoWTotem.Windfury:
                case WoWTotem.WrathOfAir:
                case WoWTotem.ManaSpring:
                    return 40f * talentFactor;

                // case WoWTotem.ElementalResistance: // Not in WotLK 3.3.5a
                case WoWTotem.HealingStream:
                // case WoWTotem.TranquilMind: // Not in WotLK 3.3.5a
                case WoWTotem.Tremor:
                    return 30f * talentFactor;

                case WoWTotem.Searing:
                    return 20f * talentFactor;

                case WoWTotem.Earthbind:
                    return 10f * talentFactor;

                case WoWTotem.Grounding:
                case WoWTotem.Magma:
                    return 8f * talentFactor;

                case WoWTotem.Stoneclaw:
                    // stoneclaw isn't effected by Totemic Reach (according to basically everything online)
                    return 8f;

                case WoWTotem.EarthElemental:
                case WoWTotem.FireElemental:
                    // Not really sure about these 3.
                    return 20f;
                case WoWTotem.ManaTide:
                    // Again... not sure :S
                    return 30f * talentFactor;
            }
            return 0f;
        }

        #endregion

        #region Nested type: MultiCastSlot

        /// <summary>
        ///   A small enum to make specifying specific totem bar slots easier.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        internal enum MultiCastSlot
        {
            // Enums increment by 1 after the first defined value. So don't touch this. Its the way it is for a reason.
            // If these numbers change in the future, feel free to fill this out completely. I'm too lazy to do it - Apoc
            //
            // Note: To get the totem 'slot' just do MultiCastSlot & 3 - will return 0-3 for the totem slot this is for.
            // I'm not entirely sure how WoW shows which ones are 'current' in the slot, so we'll just set it up for ourselves
            // and remember which is which.
            ElementsFire = 133,
            ElementsEarth,
            ElementsWater,
            ElementsAir,

            AncestorsFire,
            AncestorsEarth,
            AncestorsWater,
            AncestorsAir,

            SpiritsFire,
            SpiritsEarth,
            SpiritsWater,
            SpiritsAir
        }

        #endregion
    }
}