using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
{
    internal class CombatLogEventArgs : LuaEventArgs
    {
        public CombatLogEventArgs(string eventName, uint fireTimeStamp, object[] args)
            : base(eventName, fireTimeStamp, args)
        {
        }

        public double Timestamp { get { return (double)Args[0]; } }

        public string Event { get { return Args[1].ToString(); } }

        // WotLK 3.3.5a: No hideCaster field — added in Cata 4.1.0
        // WotLK 3.3.5a: No sourceRaidFlags/destRaidFlags — added in Cata 4.2.0
        // WotLK base params: timestamp(0), subevent(1), sourceGUID(2), sourceName(3),
        //   sourceFlags(4), destGUID(5), destName(6), destFlags(7)
        // Spell prefix starts at Args[8] (vs Args[11] in Cata 4.2+)

        public ulong SourceGuid { get { return ulong.Parse(Args[2].ToString().Replace("0x", string.Empty), NumberStyles.HexNumber); } }

        public WoWUnit SourceUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == SourceGuid || o.DescriptorGuid == SourceGuid));
            }
        }

        public string SourceName { get { return Args[3].ToString(); } }

        public int SourceFlags { get { return (int)(double)Args[4]; } }

        public ulong DestGuid { get { return ulong.Parse(Args[5].ToString().Replace("0x", string.Empty), NumberStyles.HexNumber); } }

        public WoWUnit DestUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == DestGuid || o.DescriptorGuid == DestGuid));
            }
        }

        public string DestName { get { return Args[6].ToString(); } }

        public int DestFlags { get { return (int)(double)Args[7]; } }

        public int SpellId { get { return (int)(double)Args[8]; } }

        public WoWSpell Spell { get { return WoWSpell.FromId(SpellId); } }

        public string SpellName { get { return Args[9].ToString(); } }

        public WoWSpellSchool SpellSchool { get { return (WoWSpellSchool)(int)(double)Args[10]; } }

        public object[] SuffixParams
        {
            get
            {
                var args = new List<object>();
                for (int i = 8; i < Args.Length; i++)
                {
                    if (Args[i] != null)
                    {
                        args.Add(Args[i]);
                    }
                }
                return args.ToArray();
            }
        }
    }
}