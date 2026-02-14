using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals.WoWObjects;

namespace Singular.GUI
{
    /// <summary>
    /// Pure WinForms configuration window (Roslyn compatible)
    /// EXACTLY like original ConfigurationForm but code-behind only
    /// </summary>
    public class ConfigurationWindow : Form
    {
        private TabControl tabControl;
        private PropertyGrid pgGeneral;
        private PropertyGrid pgClass;
        private Label lblVersion;
        private Button btnSaveAndClose;

        public ConfigurationWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Form properties
            this.Text = "Singular Settings";
            this.Width = 650;
            this.Height = 600;
            this.MinimumSize = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            // TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Top,
                Height = 480
            };
            this.Controls.Add(tabControl);

            // General Tab
            var tabGeneral = new TabPage("General");
            pgGeneral = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                PropertySort = PropertySort.Categorized
            };
            tabGeneral.Controls.Add(pgGeneral);
            tabControl.TabPages.Add(tabGeneral);

            // Class Specific Tab
            var tabClass = new TabPage("Class Specific");
            pgClass = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                PropertySort = PropertySort.Categorized
            };
            tabClass.Controls.Add(pgClass);
            tabControl.TabPages.Add(tabClass);

            // Debugging Tab
            var tabDebug = new TabPage("Debugging");
            var debugPanel = new Panel { Dock = DockStyle.Fill };
            
            var grpDebugInfo = new GroupBox
            {
                Text = "Character Information",
                Dock = DockStyle.Fill
            };
            
            var lblDebugInfo = new Label
            {
                Name = "lblDebugInfo",
                Text = "Character information will be displayed here when in-game.",
                Dock = DockStyle.Fill,
                AutoSize = false
            };
            grpDebugInfo.Controls.Add(lblDebugInfo);
            debugPanel.Controls.Add(grpDebugInfo);
            
            var btnRefresh = new Button
            {
                Text = "Refresh Info",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            btnRefresh.Click += BtnRefreshDebug_Click;
            debugPanel.Controls.Add(btnRefresh);
            
            tabDebug.Controls.Add(debugPanel);
            tabControl.TabPages.Add(tabDebug);

            // Bottom panel - Branding
            var lblSingular = new Label
            {
                Text = "SINGULAR",
                AutoSize = true,
                Left = 10,
                Top = 490
            };
            this.Controls.Add(lblSingular);

            var lblCommunity = new Label
            {
                Text = "Community Driven",
                AutoSize = true,
                Left = 10,
                Top = 515
            };
            this.Controls.Add(lblCommunity);

            lblVersion = new Label
            {
                Text = "v0.0.0.0",
                AutoSize = true,
                Left = 10,
                Top = 535
            };
            this.Controls.Add(lblVersion);

            // Save button
            btnSaveAndClose = new Button
            {
                Text = "Save && Close",
                Width = 100,
                Height = 30,
                Left = 520,
                Top = 510
            };
            btnSaveAndClose.Click += BtnSaveAndClose_Click;
            this.Controls.Add(btnSaveAndClose);

            // Set version
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                lblVersion.Text = $"v{version}";
            }
            catch { }

            // Load event
            this.Load += ConfigurationWindow_Load;
        }

        private void ConfigurationWindow_Load(object sender, EventArgs e)
        {
            try
            {
                var settings = SingularSettings.Instance;
                
                // Load general settings
                pgGeneral.SelectedObject = settings;

                // Load class-specific settings
                Styx.Helpers.Settings classSettings = null;
                switch (StyxWoW.Me.Class)
                {
                    case WoWClass.Warrior:
                        classSettings = settings.Warrior;
                        break;
                    case WoWClass.Paladin:
                        classSettings = settings.Paladin;
                        break;
                    case WoWClass.Hunter:
                        classSettings = settings.Hunter;
                        break;
                    case WoWClass.Rogue:
                        classSettings = settings.Rogue;
                        break;
                    case WoWClass.Priest:
                        classSettings = settings.Priest;
                        break;
                    case WoWClass.DeathKnight:
                        classSettings = settings.DeathKnight;
                        break;
                    case WoWClass.Shaman:
                        classSettings = settings.Shaman;
                        break;
                    case WoWClass.Mage:
                        classSettings = settings.Mage;
                        break;
                    case WoWClass.Warlock:
                        classSettings = settings.Warlock;
                        break;
                    case WoWClass.Druid:
                        classSettings = settings.Druid;
                        break;
                }

                if (classSettings != null)
                {
                    pgClass.SelectedObject = classSettings;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveAndClose_Click(object sender, EventArgs e)
        {
            try
            {
                // PropertyGrid modifies the object directly
                if (pgGeneral.SelectedObject != null)
                {
                    ((Styx.Helpers.Settings)pgGeneral.SelectedObject).Save();
                }

                if (pgClass.SelectedObject != null)
                {
                    ((Styx.Helpers.Settings)pgClass.SelectedObject).Save();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRefreshDebug_Click(object sender, EventArgs e)
        {
            try
            {
                var lblDebugInfo = this.Controls.Find("lblDebugInfo", true).FirstOrDefault() as Label;
                if (lblDebugInfo == null) return;

                var info = new System.Text.StringBuilder();
                info.AppendLine("=== Character Information ===");
                
                if (StyxWoW.Me != null)
                {
                    info.AppendLine($"Name: {StyxWoW.Me.Name}");
                    info.AppendLine($"Class: {StyxWoW.Me.Class}");
                    info.AppendLine($"Level: {StyxWoW.Me.Level}");
                    info.AppendLine($"Health: {StyxWoW.Me.HealthPercent:F1}%");
                    info.AppendLine($"Power: {StyxWoW.Me.ManaPercent:F1}%");
                    info.AppendLine($"Location: {StyxWoW.Me.Location}");
                }
                else
                {
                    info.AppendLine("Character not available (not in-game)");
                }

                info.AppendLine("\n=== Current Target ===");
                if (StyxWoW.Me?.CurrentTarget != null)
                {
                    info.AppendLine($"Name: {StyxWoW.Me.CurrentTarget.Name}");
                    info.AppendLine($"Level: {StyxWoW.Me.CurrentTarget.Level}");
                    info.AppendLine($"Health: {StyxWoW.Me.CurrentTarget.HealthPercent:F1}%");
                    info.AppendLine($"Distance: {StyxWoW.Me.CurrentTarget.Distance:F1} yards");
                }
                else
                {
                    info.AppendLine("No target selected");
                }

                info.AppendLine("\n=== Settings Status ===");
                info.AppendLine($"General Settings: {(pgGeneral.SelectedObject != null ? "Loaded" : "Not Loaded")}");
                info.AppendLine($"Class Settings: {(pgClass.SelectedObject != null ? "Loaded (" + StyxWoW.Me.Class + ")" : "Not Loaded")}");

                lblDebugInfo.Text = info.ToString();
            }
            catch (Exception ex)
            {
                var lblDebugInfo = this.Controls.Find("lblDebugInfo", true).FirstOrDefault() as Label;
                if (lblDebugInfo != null)
                {
                    lblDebugInfo.Text = $"Error refreshing info: {ex.Message}";
                }
            }
        }
    }
}
