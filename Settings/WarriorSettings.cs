using System.ComponentModel;

using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(SingularSettings.SettingsPath + "_Warrior.xml")
        {
        }

        #region Protection
        [Setting]
        [DefaultValue(50)]
        [Category("Protection")]
        [DisplayName("Enraged Regeneration Health")]
        [Description("Enrage Regeneration will be used when your health drops below this value")]
        public int WarriorEnragedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Protection")]
        [DisplayName("Shield Wall Health")]
        [Description("Shield Wall will be used when your health drops below this value")]
        public int WarriorProtShieldWallHealth { get; set; }


        #endregion

        #region DPS
        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Damage Cooldowns")]
        [Description("True / False if you would like the cc to use damage cooldowns")]
        public bool UseWarriorDpsCooldowns { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Interupts")]
        [Description("True / False if you would like the cc to use Interupts")]
        public bool UseWarriorInterupts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("true for Battle Shout, false for Commanding")]
        [Description("True / False if you would like the cc to use Battleshout/Commanding")]
        public bool UseWarriorShouts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Slows")]
        [Description("True / False if you would like the cc to use slows ie. Hammstring, Piercing Howl")]
        public bool UseWarriorSlows { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("DPS")]
        [DisplayName("Basic Rotation Only")]
        [Description("True / False if you would like the cc to use just the basic DPS rotation only")]
        public bool UseWarriorBasicRotation { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use AOE")]
        [Description("True / False if you would like the cc to use AOE with more than 3 mobs")]
        public bool UseWarriorAOE { get; set; }

        // WotLK QC: Removed T12 (Firelands, Cata 4.2) setting — doesn't exist in WotLK. Was orphaned (no code reads it).
        // [Setting] [DefaultValue(false)] [Browsable(false)] public bool UseWarriorT12 { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Force proper stance?")]
        [Description("True / False on whether you would like the cc to keep the toon in the proper stance for the spec. Arms:Battle, Fury:Berserker")]
        public bool UseWarriorKeepStance { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Charge/Intercept?")]
        [Description("True / False if you would like the cc to use any gap closers")]
        public bool UseWarriorCloser { get; set; }
        #endregion

        #region Arms
        [Setting]
        [DefaultValue(true)]
        [Category("Arms")]
        [DisplayName("Improved Slam Talented?")]
        [Description("True / False if you have Improved Slam Talented")]
        public bool UseWarriorSlamTalent { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Arms")]
        [DisplayName("Bladestorm?")]
        [Description("True / False if you would like the cc to use bladestorm")]
        public bool UseWarriorBladestorm { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Arms")]
        [DisplayName("Stance Dance?")]
        [Description("True / False if you want the cc to stance dance dps on bosses")]
        public bool UseWarriorStanceDance { get; set; } 
        #endregion

        // WotLK QC: Removed SMF (Single-Minded Fury, Cata 4.0.1) setting — doesn't exist in WotLK. Was orphaned.
        // [Setting] [DefaultValue(false)] [Browsable(false)] public bool UseWarriorSMF { get; set; }

    }
}