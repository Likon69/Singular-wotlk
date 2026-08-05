using System;
using System.Reflection;
using CopilotBuddy.UI;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using SettingsBase = Styx.Helpers.Settings;

namespace Singular.GUI
{
    public static class ConfigurationWindow
    {
        public static void Show()
        {
            try
            {
                var settings = SingularSettings.Instance;
                var version = Assembly.GetExecutingAssembly().GetName().Version;

                var window = new RoutineSettingsWindow(
                    "Singular Settings",
                    "SINGULAR",
                    "Community Driven · v" + (version != null ? version.ToString() : "0.0.0.0"));

                window.AddPage("General", settings);

                SettingsBase classSettings = GetClassSettings(settings);
                if (classSettings != null)
                    window.AddPage(StyxWoW.Me.Class.ToString(), classSettings);

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        private static SettingsBase GetClassSettings(SingularSettings settings)
        {
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warrior:     return settings.Warrior;
                case WoWClass.Paladin:     return settings.Paladin;
                case WoWClass.Hunter:      return settings.Hunter;
                case WoWClass.Rogue:       return settings.Rogue;
                case WoWClass.Priest:      return settings.Priest;
                case WoWClass.DeathKnight: return settings.DeathKnight;
                case WoWClass.Shaman:      return settings.Shaman;
                case WoWClass.Mage:        return settings.Mage;
                case WoWClass.Warlock:     return settings.Warlock;
                case WoWClass.Druid:       return settings.Druid;
                default:                   return null;
            }
        }
    }
}
