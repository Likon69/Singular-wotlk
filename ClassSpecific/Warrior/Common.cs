using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Singular.Utilities;
using Styx;
using Styx.Helpers;
using Styx.Logic.Combat;
using CommonBehaviors.Actions;
using TreeSharp;

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static readonly WaitTimer ChargeTimer = new WaitTimer(TimeSpan.FromMilliseconds(2000));

        public static bool PreventDoubleCharge
        {
            get
            {
                var tmp = ChargeTimer.IsFinished;
                if (tmp)
                    ChargeTimer.Reset();
                return tmp;
            }
        }

        private static Composite _singletonChargeBehavior;

        public static Composite CreateChargeBehavior()
        {
            if (!SingularSettings.Instance.Warrior.UseWarriorCloser)
                return new ActionAlwaysFail();

            if (_singletonChargeBehavior == null)
            {
                _singletonChargeBehavior = new Throttle(TimeSpan.FromMilliseconds(1500),
                    new Decorator(
                        ret => StyxWoW.Me.GotTarget &&
                               StyxWoW.Me.CurrentTargetGuid != EventHandlers.LastNoPathTarget,
                        Spell.Cast(
                            "Charge",
                            ret =>
                            StyxWoW.Me.GotTarget && SpellManager.HasSpell("Charge") &&
                            StyxWoW.Me.CurrentTarget.Distance.Between(
                                SpellManager.Spells["Charge"].ActualMinRange(StyxWoW.Me.CurrentTarget),
                                TalentManager.HasGlyph("Charge")
                                    ? SpellManager.Spells["Charge"].ActualMaxRange(StyxWoW.Me.CurrentTarget) + 5
                                    : SpellManager.Spells["Charge"].ActualMaxRange(StyxWoW.Me.CurrentTarget)))));
            }

            return _singletonChargeBehavior;
        }
    }
}
