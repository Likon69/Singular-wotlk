using System;
using System.ComponentModel;

using Singular.ClassSpecific.Paladin;

using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{

    internal class PaladinSettings : Styx.Helpers.Settings
    {
        public PaladinSettings()
            : base(SingularSettings.SettingsPath + "_Paladin.xml")
        {
        }
        
        #region Common
        [Setting]
        [DefaultValue(PaladinAura.Auto)]
        [Category("Common")]
        [DisplayName("Aura")]
        [Description("The aura to be used while not mounted. Set this to Auto to allow the CC to automatically pick the aura depending on spec.")]
        public PaladinAura Aura { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Common")]
        [DisplayName("Holy Light Health")]
        [Description("Holy Light will be used at this value")]
        public int HolyLightHealth { get; set; }

        [Setting]
        [DefaultValue(PaladinBlessings.Auto)]
        [Category("Common")]
        [DisplayName("Blessings")]
        [Description("Which Blessing to cast")]
        public PaladinBlessings Blessings { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Lay on Hand Health")]
        [Description("Lay on Hands will be used at this value")]
        public int LayOnHandsHealth { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Common")]
        [DisplayName("Flash of Light Health")]
        [Description("Flash of Light will be used at this value")]
        public int FlashOfLightHealth { get; set; }

        #endregion

        #region Holy

        [Setting]
        [DefaultValue(90)]
        [Category("Holy")]
        [DisplayName("Holy Shock Health")]
        [Description("Holy Shock will be used at this value")]
        public int HolyShockHealth { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Holy")]
        [DisplayName("Divine Plea Mana")]
        [Description("Divine Plea will be used at this value")]
        public double DivinePleaMana { get; set; } 
        #endregion

        #region Protection

        [Setting]
        [DefaultValue(80)]
        [Category("Protection")]
        [DisplayName("Divine Protection Health")]
        [Description("Divine Protection will be used at this value")]
        public int DivineProtectionHealthProt { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Protection")]
        [DisplayName("Avengers On Pull Only")]
        [Description("Only use Avenger's Shield to pull")]
        public bool AvengersPullOnly { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Protection")]
        [DisplayName("Consecration Count")]
        [Description("Consecration will be used when you have more then that many mobs attacking you")]
        public int ProtConsecrationCount { get; set; }
        #endregion

        #region Retribution
        [Setting]
        [DefaultValue(70)]
        [Category("Retribution")]
        [DisplayName("Divine Protection Health")]
        [Description("Divine Protection will be used at this value")]
        public int DivineProtectionHealthRet { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Retribution")]
        [DisplayName("Consecration Count")]
        [Description("Consecration will be used when you have more then that many mobs attacking you")]
        public int ConsecrationCount { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Retribution")]
        [DisplayName("Heal Health")]
        [Description("Healing will be done at this percentage")]
        public int RetributionHealHealth { get; set; }

        #endregion
    }
}