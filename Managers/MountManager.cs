using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;

namespace Singular.Managers
{
    // This class is here to deal with Ghost Wolf/Travel Form usage for shamans and druids
    // Note: MountUpEventArgs is a Cata feature, for WotLK 3.3.5a we use simplified logic
    internal static class MountManager
    {
        internal static void Init()
        {
            // Mount.OnMountUp event is not available in WotLK 3.3.5a
            // Ghost Wolf/Travel Form will be handled in class-specific combat routines
        }
        
        /// <summary>
        /// Returns true if the player should use a travel form instead of mounting.
        /// Call this before attempting to mount.
        /// </summary>
        internal static bool ShouldUseShapeshiftInsteadOfMount(WoWPoint destination)
        {
            if (destination == WoWPoint.Zero)
                return false;

            if (destination.DistanceSqr(StyxWoW.Me.Location) < 60 * 60)
            {
                // Shaman with improved ghost wolf talent (2/2 in Enhance tree, tier 2)
                if (SpellManager.HasSpell("Ghost Wolf") && TalentManager.GetCount(2, 6) == 2)
                {
                    if (!StyxWoW.Me.HasAura("Ghost Wolf"))
                    {
                        Logger.Write("Using Ghost Wolf instead of mounting");
                        SpellManager.Cast("Ghost Wolf");
                    }
                    return true;
                }
                
                // Druid travel form
                if (SpellManager.HasSpell("Travel Form"))
                {
                    if (!StyxWoW.Me.HasAura("Travel Form"))
                    {
                        Logger.Write("Using Travel Form instead of mounting.");
                        SpellManager.Cast("Travel Form");
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
