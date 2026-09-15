using ApexSenseBridgeTray.Common;
using ApexSenseBridgeTray.Models;
using ApexSenseBridgeTray.Services;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ApexSenseBridgeTray
{
    public partial class App : Application
    {
        private const string AppMutexName = @"Global\ApexSenseBridgeTrayMutex";
        private Mutex singleInstanceMutex;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem statusMenuItem;
        private ToolStripMenuItem autoDetectMenuItem;

        private TraySettings settings;
        private CloudGameListService gameListService;
        private EngineSessionManager sessionManager;
        private ExecutableLearningService learningService;
        private ProcessMonitorService monitorService;
        private UpdateCheckerService updateChecker;
        private MainWindow mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    var ex = args.ExceptionObject as Exception;
                    var msg = ex != null ? ex.ToString() : (args.ExceptionObject != null ? args.ExceptionObject.ToString() : "Unknown error");
                    AppLog.WriteLine("tray_crash.log", "[UNHANDLED] " + msg);
                }
                catch { }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    AppLog.WriteLine(
                        "tray_crash.log", "[DISPATCHER] " + args.Exception.ToString());
                }
                catch { }
                args.Handled = true;
            };

            try
            {
                settings = TraySettings.Load();
                LocalizationManager.Initialize(settings.Language);
                ThemeManager.Initialize();

                bool isNewInstance;
                singleInstanceMutex = new Mutex(true, AppMutexName, out isNewInstance);
                if (!isNewInstance)
                {
                    MessageBox.Show(LocalizationManager.Get("Loc_AlreadyRunning"),
                                    LocalizationManager.Get("Loc_AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                gameListService = new CloudGameListService();
                gameListService.Initialize();

                sessionManager = new EngineSessionManager();
                learningService = new ExecutableLearningService();
                monitorService = new ProcessMonitorService(
                    gameListService, sessionManager, learningService, settings);
                gameListService.GamesUpdated += monitorService.ForceCheck;
                updateChecker = new UpdateCheckerService();

                mainWindow = new MainWindow(
                    gameListService, sessionManager, learningService,
                    monitorService, updateChecker, settings);

                learningService.InitializeAsync();

                InitializeNotifyIcon();

                LocalizationManager.LanguageChanged += () =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            BuildContextMenu();
                            UpdateTrayStatus();
                        }
                        catch { }
                    }));
                };

                monitorService.GameDetected += (game, path) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (settings.EnableNotifications && notifyIcon != null)
                            {
                                string gameTitle = game != null ? game.Title : LocalizationManager.Get("Loc_NotificationGame");
                                string profileName = game != null ? game.Profile : LocalizationManager.Get("Loc_NotificationProfileStandard");
                                notifyIcon.ShowBalloonTip(
                                    3000,
                                    LocalizationManager.Get("Loc_NotificationActivated"),
                                    LocalizationManager.Format("Loc_NotificationGameProfile", gameTitle, profileName),
                                    ToolTipIcon.Info);
                            }
                            UpdateTrayStatus();
                        }
                        catch { }
                    }));
                };

                monitorService.GameExited += (path) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { UpdateTrayStatus(); } catch { }
                    }));
                };

                sessionManager.SessionStarted += (game, profile) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { UpdateTrayStatus(); } catch { }
                    }));
                };

                sessionManager.SessionStopped += (reason) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { UpdateTrayStatus(); } catch { }
                    }));
                };

                sessionManager.SessionError += (err) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdateTrayStatus();
                            if (settings.EnableNotifications && notifyIcon != null && !string.IsNullOrWhiteSpace(err))
                            {
                                notifyIcon.ShowBalloonTip(
                                    4000,
                                    LocalizationManager.Get("Loc_NotificationWarning"),
                                    err,
                                    ToolTipIcon.Warning);
                            }
                        }
                        catch { }
                    }));
                };

                sessionManager.LogMessage += (msg) =>
                {
                    try
                    {
                        AppLog.WriteLine("tray_bridge.log", msg);
                    }
                    catch { }
                };

                updateChecker.UpdateAvailable += (info) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (settings.EnableNotifications && notifyIcon != null && info != null && info.HasUpdate)
                            {
                                notifyIcon.ShowBalloonTip(
                                    5000,
                                    LocalizationManager.Get("Loc_UpdateAvailableNotification"),
                                    LocalizationManager.Format("Loc_UpdateAvailableBody", info.LatestVersion),
                                    ToolTipIcon.Info);
                            }
                        }
                        catch { }
                    }));
                };

                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    try { await gameListService.FetchLatestFromCloudAsync(); } catch { }
                });

                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    try { await updateChecker.CheckForUpdatesAsync(true); } catch { }
                });
            }
            catch (Exception ex)
            {
                try
                {
                    AppLog.WriteLine("tray_startup_error.log", ex.ToString());
                }
                catch { }
                MessageBox.Show(LocalizationManager.Get("Loc_StartupError") + ex.Message, "ApexSenseBridge Tray", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void InitializeNotifyIcon()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Renderer = new DarkTrayMenuRenderer();
            contextMenu.Font = new Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Regular);
            contextMenu.ShowImageMargin = false;
            contextMenu.ShowCheckMargin = false;
            contextMenu.BackColor = Color.FromArgb(14, 16, 22);
            contextMenu.ForeColor = Color.FromArgb(235, 238, 245);
            BuildContextMenu();

            Icon appIcon = SystemIcons.Application;
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                }
                if (File.Exists(iconPath))
                {
                    appIcon = new Icon(iconPath);
                }
                else
                {
                    var resourceUri = new Uri("pack://application:,,,/ApexSenseBridgeTray;component/Resources/app.ico");
                    var info = System.Windows.Application.GetResourceStream(resourceUri);
                    if (info != null && info.Stream != null)
                    {
                        appIcon = new Icon(info.Stream);
                    }
                }
            }
            catch
            {
            }

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = appIcon;
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Text = LocalizationManager.Get("Loc_TrayTooltipStandby");
            notifyIcon.Visible = true;

            notifyIcon.Click += (s, e) =>
            {
                var me = e as MouseEventArgs;
                if (me == null || me.Button == MouseButtons.Left)
                {
                    ShowMainWindow();
                }
            };
            notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void BuildContextMenu()
        {
            if (contextMenu == null) return;
            contextMenu.Items.Clear();

            string statusText = (sessionManager != null && sessionManager.IsSessionActive)
                ? LocalizationManager.Format("Loc_TrayStatusActive", sessionManager.ActiveGameTitle)
                : LocalizationManager.Get("Loc_TrayStatusStandby");

            statusMenuItem = new ToolStripMenuItem(statusText);
            statusMenuItem.Enabled = false;
            statusMenuItem.Font = new Font(contextMenu.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(statusMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            var openItem = new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayOpen"), null, (s, e) => ShowMainWindow());
            openItem.Font = new Font(contextMenu.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(openItem);

            autoDetectMenuItem = new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayAutoDetect"), null, (s, e) =>
            {
                settings.AutoDetectGames = !settings.AutoDetectGames;
                autoDetectMenuItem.Checked = settings.AutoDetectGames;
                settings.Save();
                if (settings.AutoDetectGames)
                {
                    monitorService.ForceCheck();
                }
                else if (sessionManager.IsSessionActive && settings.ForcedProfile == "none")
                {
                    sessionManager.StopSession("Auto-detect disabled from tray");
                }
                mainWindow.UpdateSessionStatus();
            });
            autoDetectMenuItem.Checked = settings != null && settings.AutoDetectGames;
            contextMenu.Items.Add(autoDetectMenuItem);

            contextMenu.Items.Add(new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayCheckUpdates"), null, async (s, e) =>
            {
                if (updateChecker != null)
                {
                    await updateChecker.CheckForUpdatesAsync(false);
                }
            }));

            contextMenu.Items.Add(new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayControlPanel"), null, (s, e) =>
            {
                var controlPath = InstallLocator.ResolveControlPanel();
                if (!string.IsNullOrWhiteSpace(controlPath) && File.Exists(controlPath))
                {
                    try { Process.Start(new ProcessStartInfo(controlPath) { UseShellExecute = true }); } catch { }
                }
            }));

            var langMenu = new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayLanguage"));
            langMenu.DropDown.Renderer = new DarkTrayMenuRenderer();
            var dropDownMenu = langMenu.DropDown as ToolStripDropDownMenu;
            if (dropDownMenu != null)
            {
                dropDownMenu.ShowImageMargin = false;
                dropDownMenu.ShowCheckMargin = false;
            }
            langMenu.DropDown.BackColor = Color.FromArgb(14, 16, 22);
            langMenu.DropDown.ForeColor = Color.FromArgb(235, 238, 245);
            var langEnglish = new ToolStripMenuItem("English", null, (s, e) => SwitchLanguage(LocalizationManager.LangEnglish));
            var langFrench = new ToolStripMenuItem("Français", null, (s, e) => SwitchLanguage(LocalizationManager.LangFrench));
            langEnglish.Checked = LocalizationManager.CurrentLanguage == LocalizationManager.LangEnglish;
            langFrench.Checked = LocalizationManager.CurrentLanguage == LocalizationManager.LangFrench;
            langMenu.DropDownItems.Add(langEnglish);
            langMenu.DropDownItems.Add(langFrench);
            contextMenu.Items.Add(langMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            contextMenu.Items.Add(new ToolStripMenuItem(LocalizationManager.Get("Loc_TrayExit"), null, (s, e) => ExitApplication()));
        }

        private void SwitchLanguage(string lang)
        {
            if (settings != null)
            {
                settings.Language = lang;
                settings.Save();
            }
            LocalizationManager.SetLanguage(lang);
        }

        private void UpdateTrayStatus()
        {
            try
            {
                if (sessionManager != null && sessionManager.IsSessionActive)
                {
                    var text = LocalizationManager.Format("Loc_TrayStatusActive", sessionManager.ActiveGameTitle);
                    if (statusMenuItem != null)
                    {
                        statusMenuItem.Text = text;
                        statusMenuItem.ForeColor = Color.FromArgb(52, 211, 153);
                    }
                    if (notifyIcon != null) notifyIcon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
                }
                else
                {
                    if (statusMenuItem != null)
                    {
                        statusMenuItem.Text = LocalizationManager.Get("Loc_TrayStatusStandby");
                        statusMenuItem.ForeColor = Color.FromArgb(56, 189, 248);
                    }
                    if (notifyIcon != null) notifyIcon.Text = LocalizationManager.Get("Loc_TrayTooltipStandby");
                }
            }
            catch { }
        }

        private GameListWindow dashboardWindow;

        private void ShowMainWindow()
        {
            try
            {
                if (dashboardWindow == null || !dashboardWindow.IsLoaded)
                {
                    dashboardWindow = new GameListWindow(
                        gameListService, settings, learningService,
                        sessionManager, monitorService, updateChecker,
                        initialTab: "dashboard");
                    dashboardWindow.Closed += (s, e) => dashboardWindow = null;
                }
                dashboardWindow.Show();
                dashboardWindow.WindowState = WindowState.Normal;
                dashboardWindow.Activate();
            }
            catch { }
        }

        private void ExitApplication()
        {
            if (monitorService != null) monitorService.Dispose();
            if (learningService != null) learningService.Dispose();
            if (sessionManager != null) sessionManager.StopSession("Tray exiting");

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            if (singleInstanceMutex != null)
            {
                singleInstanceMutex.ReleaseMutex();
                singleInstanceMutex.Dispose();
            }
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (monitorService != null) monitorService.Dispose();
            if (learningService != null) learningService.Dispose();
            if (sessionManager != null) sessionManager.StopSession("Tray app closing");

            if (notifyIcon != null) notifyIcon.Dispose();
            if (singleInstanceMutex != null) singleInstanceMutex.Dispose();
            base.OnExit(e);
        }
    }

    internal sealed class DarkTrayColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(14, 16, 22);
        public override Color MenuBorder => Color.FromArgb(36, 40, 52);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(0, 112, 209);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0, 112, 209);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0, 112, 209);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(0, 91, 181);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(0, 91, 181);
        public override Color ImageMarginGradientBegin => Color.FromArgb(14, 16, 22);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(14, 16, 22);
        public override Color ImageMarginGradientEnd => Color.FromArgb(14, 16, 22);
        public override Color SeparatorDark => Color.FromArgb(36, 40, 52);
        public override Color SeparatorLight => Color.Transparent;
        public override Color CheckBackground => Color.FromArgb(0, 112, 209);
        public override Color CheckSelectedBackground => Color.FromArgb(0, 120, 220);
        public override Color CheckPressedBackground => Color.FromArgb(0, 91, 181);
    }

    internal sealed class DarkTrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkTrayMenuRenderer() : base(new DarkTrayColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                e.TextColor = Color.FromArgb(100, 108, 126);
            }
            else if (e.Item.Selected)
            {
                e.TextColor = Color.White;
            }
            else
            {
                e.TextColor = Color.FromArgb(235, 238, 245);
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                var rect = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
                using (var brush = new SolidBrush(Color.FromArgb(0, 112, 209)))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }
}
