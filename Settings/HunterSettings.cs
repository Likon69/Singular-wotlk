using System;
using System.ComponentModel;

using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class HunterSettings : Styx.Helpers.Settings
    {
        public HunterSettings()
            : base(SingularSettings.SettingsPath + "_Hunter.xml")
        {
        }

        #region Category: Pet

        [Setting]
        [DefaultValue("1")]
        [Category("Pet")]
        [DisplayName("Pet Slot")]
        public string PetSlot { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Pet")]
        [DisplayName("Mend Pet Percent")]
        public double MendPetPercent { get; set; }

        #endregion

        #region Category: Common

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Disengage")]
        [Description("Will be used in battlegrounds no matter what this is set")]
        public bool UseDisengage { get; set; }

        [Setting]
        [DefaultValue(5)]
        [Category("Common")]
        [DisplayName("Viper Mana %")]
        [Description("Switch to Aspect of the Viper when mana drops below this percent")]
        public int ViperManaPercent { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Common")]
        [DisplayName("Viper Resume Mana %")]
        [Description("Switch back to Aspect of the Dragonhawk when mana reaches this percent")]
        public int ViperResumeManaPercent { get; set; }

        #endregion
    }
}