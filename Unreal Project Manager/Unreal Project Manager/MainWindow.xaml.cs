using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO.Compression;
using System.IO;
using System.Windows.Threading;

namespace Unreal_Project_Manager
{
    public partial class MainWindow : System.Windows.Window
    {
        private bool _isMouseDown = false;
        private bool _CanAutoSave = false;
        private bool _isAutoSaving = false;
        private bool _isAnyUnzip = false;
        private bool _isManualSaving = false;
        private IniFile iniFile;
        private DispatcherTimer autoSaveTimer;
        private List<string> projectBackup = new List<string>();
        private string backupPth = null;

        public MainWindow()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnrealProjectManager.ini");
            iniFile = new IniFile(path);

            InitializeComponent();
            LoadProjSettings(iniFile);
            LoadBackupSettings(iniFile);
            LoadIntervalSettings(iniFile);
            gatherProjectItems();

            // Időzítő létrehozása és eseménykezelők beállítása
            autoSaveTimer = new DispatcherTimer();
            autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }

        private void LoadProjSettings(IniFile iniFile)
        {
            string projectLocation = iniFile.Read("Settings", "ProjectLocation");
            projLocInp.Text = projectLocation;
        }

        private void LoadBackupSettings(IniFile iniFile)
        {

            string backupLocation = iniFile.Read("Settings", "BackupLocation");
            backUpLocInp.Text = backupLocation;
            backupPth = backupLocation;
        }

        private void LoadIntervalSettings(IniFile iniFile)
        {
            string interval = iniFile.Read("Settings", "AutoSaveInterval");

            switch (interval)
            {
                case "1hr":
                    saveInter.SelectedIndex = 0;
                    break;
                case "2hr":
                    saveInter.SelectedIndex = 1;
                    break;
                case "4hr":
                    saveInter.SelectedIndex = 2;
                    break;
                case "6hr":
                    saveInter.SelectedIndex = 3;
                    break;
                case "8hr":
                    saveInter.SelectedIndex = 4;
                    break;
                case "12hr":
                    saveInter.SelectedIndex = 5;
                    break;
                case "24hr":
                    saveInter.SelectedIndex = 6;
                    break;
                default:
                    saveInter.SelectedIndex = 1;
                    break;
            }
        }

        private void SaveProjSettings()
        {
            iniFile.Write("Settings", "ProjectLocation", projLocInp.Text);
        }

        private void SaveBackupSettings()
        {
            iniFile.Write("Settings", "BackupLocation", backUpLocInp.Text);
            backupPth = backUpLocInp.Text;
        }

        private void SaveInterval()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnrealProjectManager.ini");
            iniFile = new IniFile(path);

            int selectedIndex = saveInter.SelectedIndex;
            string interval = "";

            switch (selectedIndex)
            {
                case 0:
                    interval = "1hr";
                    break;
                case 1:
                    interval = "2hr";
                    break;
                case 2:
                    interval = "4hr";
                    break;
                case 3:
                    interval = "6hr";
                    break;
                case 4:
                    interval = "8hr";
                    break;
                case 5:
                    interval = "12hr";
                    break;
                case 6:
                    interval = "24hr";
                    break;
                default:
                    interval = "2hr";
                    break;
            }

            iniFile.Write("Settings", "AutoSaveInterval", interval);
        }

        private void title_bar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isMouseDown && this.WindowState == System.Windows.WindowState.Maximized)
            {
                _isMouseDown = false;
                this.WindowState = System.Windows.WindowState.Normal;
            }
        }

        private void title_bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            this.DragMove();
        }

        private void title_bar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
        }

        private void minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            if (_isAutoSaving == false && _isManualSaving == false && _isAnyUnzip == false)
            {
                SaveProjSettings();
                SaveBackupSettings();
                SaveInterval();
                System.Windows.Application.Current.Shutdown();
            }
        }


        private void minimize_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void minimize_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
        }

        private void maximize_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void maximize_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
        }

        private void close_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
        }

        private void close_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void BrowsProj_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Unreal Project Files (*.uproject)|*.uproject";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                projLocInp.Text = openFileDialog.FileName;
                SaveProjSettings();
                CleanBackupProjects();
                gatherProjectItems();
            }
        }


        private void BackupProj_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                {
                    backUpLocInp.Text = folderDialog.SelectedPath;
                }
            }
        }

        private async void manualSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isAutoSaving == false)
            {
                _isManualSaving = true;
                BackupIndCh(0x00, 0x7A, 0xE0, 1.0);
                string backupFolderPath = backUpLocInp.Text;
                string projectFilePath = projLocInp.Text;
                Task<bool> backupTask = Task.Run(() => BackupProject(backupFolderPath, projectFilePath));
                bool backupResult = await backupTask;

                if (backupResult == true)
                {
                    BackupIndCh(0x7D, 0xC3, 0x43, 1.0);
                    CleanBackupProjects();
                    gatherProjectItems();
                }
                else
                {
                    BackupIndCh(0xFF, 0xC3, 0x43, 1.0);
                }
                // Várakozás 1 másodperc
                await Task.Delay(2000);

                // Második BackupIndCh hívása
                BackupIndCh(0x24, 0x24, 0x24, 0.0);
                _isManualSaving = false;
            }
        }

        private async void RefreshProj_Click(object sender, RoutedEventArgs e)
        {
            CleanBackupProjects();
            gatherProjectItems();
        }

        // A projekt mentését végző függvény
        private async Task<bool> BackupProject(string backupFolderPath, string projectFilePath)
        {
            // A backup mappa elérési útvonala

            // Ellenőrizze, hogy a backup mappa elérési útvonala üres-e
            if (string.IsNullOrEmpty(backupFolderPath))
            {
                System.Windows.MessageBox.Show("Please specify a backup folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Az alkalmazás fő mappája
            string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;

            // Ellenőrizze, hogy a projekt fájl elérési útvonala üres-e
            if (string.IsNullOrEmpty(projectFilePath))
            {
                System.Windows.MessageBox.Show("Please specify a project file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // A projekt fájl neve
            string projectFileName = System.IO.Path.GetFileNameWithoutExtension(projectFilePath);

            // A projekt mappája (ahol a .uproject fájl található)
            string projectFolderPath = System.IO.Path.GetDirectoryName(projectFilePath);

            // A projekt mappa neve
            string projectFolderName = System.IO.Path.GetFileName(projectFolderPath);

            // A mentés ideje
            string saveTime = DateTime.Now.ToString("yyyyMMdd_HHmm");

            // A ZIP fájl neve
            string zipFileName = $"UPM_{projectFileName}_{saveTime}.zip";

            // A mentéshez szükséges fájl teljes elérési útvonala
            string projectFolderFullPath = System.IO.Path.Combine(appFolderPath, projectFolderPath);
            string zipFilePath = System.IO.Path.Combine(backupFolderPath, zipFileName);

            try
            {
                await Task.Run(() => ZipFile.CreateFromDirectory(projectFolderFullPath, zipFilePath));
                //System.Windows.MessageBox.Show($"Backup created successfully: {zipFileName} at {}", "Backup Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                // Hibás üzenet megjelenítése
                //System.Windows.MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BackupIndCh(byte r, byte g, byte b, double op)
        {
            Color color = Color.FromRgb(r, g, b);
            backupInd.Fill = new SolidColorBrush(color);
            backupIndShadow.Opacity = op;
        }

        private void autoSave_Click(object sender, RoutedEventArgs e)
        {
            // Ellenőrzi, hogy az automatikus mentés aktív-e vagy sem
            if (_CanAutoSave)
            {
                // Ha aktív, akkor kapcsolja ki
                _CanAutoSave = false;
                autoSave.Content = "Start automatic save";
                autoSaveTimer.Stop();
            }
            else
            {
                // Ha inaktív, akkor kapcsolja be
                _CanAutoSave = true;
                autoSave.Content = "Stop automatic save";
                SetAutoSaveInterval();
                autoSaveTimer.Start();
            }
        }

        private void SetAutoSaveInterval()
        {
            // Az időköz kiválasztása a ComboBox-ból
            ComboBoxItem selectedItem = (ComboBoxItem)saveInter.SelectedItem;
            string interval = selectedItem.Content.ToString();

            // Az időköz alapján állítsa be az időzítőt
            switch (interval)
            {
                case "1 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(1);
                    break;
                case "2 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(2);
                    break;
                case "4 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(4);
                    break;
                case "6 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(6);
                    break;
                case "8 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(8);
                    break;
                case "12 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(12);
                    break;
                case "24 hr":
                    autoSaveTimer.Interval = TimeSpan.FromHours(24);
                    break;
                default:
                    break;
            }
        }

        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            // Csak akkor végezze el az automatikus mentést, ha engedélyezve van
            if (_CanAutoSave)
            {
                // Végrehajtja az automatikus mentés folyamatát
                await PerformAutoSave();
            }
        }

        private async Task PerformAutoSave()
        {
            if (_isManualSaving == false)
            {
                _isAutoSaving = true;
                BackupIndCh(0x00, 0x7A, 0xE0, 1.0);
                string backupFolderPath = backUpLocInp.Text;
                string projectFilePath = projLocInp.Text;
                Task<bool> backupTask = Task.Run(() => BackupProject(backupFolderPath, projectFilePath));
                bool backupResult = await backupTask;

                if (backupResult == true)
                {
                    BackupIndCh(0x7D, 0xC3, 0x43, 1.0);
                    CleanBackupProjects();
                    gatherProjectItems();
                }
                else
                {
                    BackupIndCh(0xFF, 0xC3, 0x43, 1.0);
                }
                // Várakozás 1 másodperc
                await Task.Delay(2000);

                // Második BackupIndCh hívása
                BackupIndCh(0x24, 0x24, 0x24, 0.0);
                _isAutoSaving = false;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveInterval();
        }

        async private void gatherProjectItems()
        {
            int projectC = 0;
            string backupPath = backUpLocInp.Text;
            projectBackup.Clear();

            // Ellenőrizze, hogy a backup mappa elérési útvonala üres-e
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
            {
                return;
            }

            try
            {
                // Végigiterálás a mappán és hozzáadás a zip fájlok neveinek listájához
                foreach (string file in Directory.GetFiles(backupPath, "*.zip"))
                {
                    string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(file);
                    string fileName = System.IO.Path.GetFileName(file);
                    // Ellenőrizzük, hogy a fájl nevében legalább három '_' karakter van-e
                    if (fileName.StartsWith("UPM") && fileName.Count(c => c == '_') >= 3 && fileName.EndsWith(".zip"))
                    {
                        projectBackup.Add(fileNameWithoutExtension);
                        projectC++;
                    }
                }

                // Itt megteheti, amit akar a zip fájlokkal
                int i = 0;
                foreach (string zipFileName in projectBackup)
                {
                    generateProjectItem(backupPath, zipFileName, i);
                    i++;
                }
            }
            catch (Exception ex)
            {
                // Hiba esetén kezelés
                System.Windows.MessageBox.Show($"An error occurred while processing zip file names: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            projectCount.Text = $"{projectC} backup projects";
        }




        async private void generateProjectItem(string backupPath, string currFileName, int iteration)
        {
            string[] projectArr = currFileName.Split('_');
            string projectName = projectArr[1];
            string rawDate = projectArr[2];
            string rawTime = projectArr[3];

            Console.WriteLine(projectArr[3]);

            // Ellenőrzés, hogy rawDate megfelelő hosszúságú
            if (projectArr[2].Length != 8 || !int.TryParse(projectArr[2], out int result0))
            {
                return;
            }
            // Ellenőrzés, hogy rawTime megfelelő hosszúságú
            if (projectArr[3].Length != 4 || !int.TryParse(projectArr[3], out int result1))
            {
                Console.WriteLine("Returned at rawTime");
                return;
            }

            // Dátum formázása
            DateTime date = DateTime.ParseExact(rawDate.ToString(), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            string formattedDate = date.ToString("yyyy.MM.dd");
            string formattedTime = rawTime.ToString().Insert(2, ":");
            string formattedDateTime = $"{formattedDate} {formattedTime}";

            Console.WriteLine(formattedDateTime);

            // Border létrehozása
            Border border = new Border();
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(5);
            border.Margin = new Thickness(20, 10, 20, 0);
            border.Height = 120;
            border.Padding = new Thickness(5);
            border.Background = new SolidColorBrush(Color.FromRgb(26, 26, 26));

            // Grid létrehozása
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 0, 10, 0); // Margin változott

            // Egy automatikus és egy csillag méretű oszlop hozzáadása a Gridhez
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            border.Child = grid; // A Grid-et a Border Child-jává kell tenni

            // Image létrehozása
            Image image = new Image();
            image.Source = new BitmapImage(new Uri("Assets/backup_projects.png", UriKind.Relative));
            image.Width = 100;
            image.MaxHeight = 100;
            image.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            image.VerticalAlignment = VerticalAlignment.Center;
            image.Margin = new Thickness(0, 0, 0, 0); // Margin változott

            // Border létrehozása a kép köré
            Border imageBorder = new Border();
            imageBorder.BorderThickness = new Thickness(1);
            imageBorder.CornerRadius = new CornerRadius(3);
            imageBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 112, 224)); // Választható: szürke keret a kép körül
            imageBorder.Height = 100;
            imageBorder.Width = 100;
            imageBorder.Margin = new Thickness(5, 0, 5, 0); // Margin változott
            imageBorder.Child = image; // A képet adjuk hozzá a Border Child-jává

            // A kép Border hozzáadása a Grid-hez
            grid.Children.Add(imageBorder);

            // TextBlock létrehozása és hozzáadása a Grid-hez
            TextBlock projectNameTextBlock = new TextBlock();
            projectNameTextBlock.Text = projectName;
            projectNameTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(177, 177, 177));
            projectNameTextBlock.FontSize = 16;
            projectNameTextBlock.FontWeight = FontWeights.Bold;
            projectNameTextBlock.VerticalAlignment = VerticalAlignment.Center;
            projectNameTextBlock.Margin = new Thickness(20, 0, 0, 0); // Margin változott
            Grid.SetColumn(projectNameTextBlock, 1); // Column beállítása
            grid.Children.Add(projectNameTextBlock);

            // StackPanel létrehozása és hozzáadása a Grid-hez
            StackPanel buttonStackPanel = new StackPanel();
            buttonStackPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
            buttonStackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            buttonStackPanel.VerticalAlignment = VerticalAlignment.Top;
            buttonStackPanel.Margin = new Thickness(0, 5, 0, 0); // Margin változott
            Grid.SetColumn(buttonStackPanel, 2); // Column beállítása
            grid.Children.Add(buttonStackPanel);

            // Unzip Button létrehozása és hozzáadása a StackPanel-hez
            System.Windows.Controls.Button unzipButton = new System.Windows.Controls.Button();
            unzipButton.Style = (Style)FindResource("RoundedButtonStyle");
            unzipButton.Content = "Unzip than Launch";
            unzipButton.Click += UnzipLaunchButton_Click;
            unzipButton.Tag = iteration.ToString();
            unzipButton.Background = new SolidColorBrush(Color.FromRgb(0, 112, 224));
            unzipButton.Foreground = Brushes.White;
            unzipButton.FontWeight = FontWeights.Bold;
            unzipButton.Padding = new Thickness(5, 3, 5, 3);
            unzipButton.Margin = new Thickness(0, 5, 0, 5);
            buttonStackPanel.Children.Add(unzipButton);

            // Open Location Button létrehozása és hozzáadása a StackPanel-hez
            System.Windows.Controls.Button openLocationButton = new System.Windows.Controls.Button();
            openLocationButton.Style = (Style)FindResource("RoundedButtonStyle");
            openLocationButton.Content = "Open Location";
            openLocationButton.Background = new SolidColorBrush(Color.FromRgb(0, 112, 224));
            openLocationButton.Foreground = Brushes.White;
            openLocationButton.Click += OpenLocationButton_Click;
            openLocationButton.Tag = iteration.ToString();
            openLocationButton.FontWeight = FontWeights.Bold;
            openLocationButton.Padding = new Thickness(5, 3, 5, 3);
            openLocationButton.Margin = new Thickness(0, 5, 0, 0);
            buttonStackPanel.Children.Add(openLocationButton);

            // Date TextBlock létrehozása és hozzáadása a Grid-hez
            TextBlock dateTextBlock = new TextBlock();
            dateTextBlock.Text = formattedDateTime;
            dateTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(177, 177, 177));
            dateTextBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            dateTextBlock.VerticalAlignment = VerticalAlignment.Bottom;
            dateTextBlock.Margin = new Thickness(0, 0, 0, 5); // Margin változott
            Grid.SetColumn(dateTextBlock, 1); // Column beállítása
            grid.Children.Add(dateTextBlock);

            // BackupStackPanel-hez hozzáadás
            backupStackPanel.Children.Add(border);
        }
        private void OpenLocationButton_Click(object sender, EventArgs e)
        {
            System.Windows.Controls.Button OpLobutton = sender as System.Windows.Controls.Button;

            if (OpLobutton != null)
            {
                string index = OpLobutton.Tag as string;

                string projectName = projectBackup[int.Parse(index)];
                string projectExt = $"{projectName}.zip";
                string fullPth = $"{backupPth}\\{projectExt}";

                if (System.IO.File.Exists(fullPth))
                {
                    Process.Start("explorer.exe", $"/select,\"{fullPth}\"");
                }
            }
        }

        private async void UnzipLaunchButton_Click(object sender, EventArgs e)
        {
            System.Windows.Controls.Button OpLobutton = sender as System.Windows.Controls.Button;
            performUnzip(OpLobutton);
        }

        private async void performUnzip(System.Windows.Controls.Button opLoBt)
        {


            if (opLoBt != null)
            {
                string index = opLoBt.Tag as string;

                string projectName = projectBackup[int.Parse(index)];
                string projectExt = $"{projectName}.zip";
                string backUpPth = $"{backupPth}\\{projectName}";
                string fullPth = $"{backupPth}\\{projectExt}";
                BackupIndCh(0x00, 0x7A, 0xE0, 1.0);
                Task<bool> backupTask = Task.Run(() => PerformUnzipProject(fullPth, backUpPth));
                bool backupResult = await backupTask;

                if (backupResult == true)
                {
                    BackupIndCh(0x7D, 0xC3, 0x43, 1.0);
                }
                else
                {
                    BackupIndCh(0xFF, 0xC3, 0x43, 1.0);
                }
                // Várakozás 1 másodperc
                await Task.Delay(2000);
                // Második BackupIndCh hívása
                BackupIndCh(0x24, 0x24, 0x24, 0.0);
            }
        }

        private async Task<bool> PerformUnzipProject(string Pth, string BackUpPth)
        {
            Console.WriteLine(BackUpPth);
            if (_isAnyUnzip == false)
            {
                _isAnyUnzip = true;
                try
                {
                    // Ellenőrizzük, hogy a BackUpPth létezik-e
                    if (!Directory.Exists(BackUpPth))
                    {
                        Directory.CreateDirectory(BackUpPth);
                        ZipFile.ExtractToDirectory(Pth, BackUpPth);
                        string[] projectFiles = Directory.GetFiles(BackUpPth, "*.uproject");

                        if (projectFiles.Length > 0)
                        {
                            // Ha van .uproject fájl, elindítjuk azt
                            Process.Start(projectFiles.First());
                        }
                        else
                        {
                            // Ha nincs .uproject fájl, megnyitjuk a fájlkezelőt a BackUpPth mappában
                            Process.Start("explorer.exe", $"/select,\"{BackUpPth}\"");
                        }
                    }
                    else
                    {
                        string[] projectFiles = Directory.GetFiles(BackUpPth, "*.uproject");
                        if (projectFiles.Length > 0)
                        {
                            // Ha van .uproject fájl, elindítjuk azt
                            Process.Start(projectFiles.First());
                        }
                        else
                        {
                            // Ha nincs .uproject fájl, megnyitjuk a fájlkezelőt a BackUpPth mappában
                            Process.Start("explorer.exe", $"/select,\"{BackUpPth}\"");
                        }
                    }
                    _isAnyUnzip = false;
                    return true;
                }
                catch (Exception ex)
                {
                    _isAnyUnzip = false;
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void CleanBackupProjects()
        {
            backupStackPanel.Children.Clear();
        }
    }

    public class IniFile
    {
        private string _path;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string path)
        {
            _path = path;
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _path);
        }

        public string Read(string section, string key)
        {
            StringBuilder sb = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", sb, 255, _path);
            return sb.ToString();
        }
    }
}
