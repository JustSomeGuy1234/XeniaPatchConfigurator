﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.CompilerServices;

namespace XeniaPatchUI
{
    // MVVM is for those that know what they're doing, not me c:

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            PatchFolder.patchFolder = (PatchFolder)FindResource("patchFolder");
            if (PatchFolder.patchFolder == null)
            {
                throw new ArgumentException("Patch folder instance not found!");
            }
            UpdatePatchFiles();
            PatchFolder.patchFolder.PortableFolder = @"C:\path\to\portableXeniaCanary\patches\"; // TODO: Create a config file to save portable path, and add game favouriting

            patchfileListBox.DataContext = PatchFolder.patchFiles;
            portableCheckbox.DataContext = PatchFolder.patchFolder.IsPortable;
            
            patchfileListBox.Items.SortDescriptions.Add(new SortDescription("GameName", ListSortDirection.Ascending));
        }

        

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (dropped == null || dropped.Length < 1 || dropped.Length == 0)
            {
                return;
            }
            string folder = dropped[0];
            bool isFolder = File.GetAttributes(folder).HasFlag(FileAttributes.Directory);
            if (!isFolder)
            {
                return;
            }
            if (folder[^1] != '\\') {
                folder += '\\';
            }
            PatchFolder.patchFolder.PortableFolder = folder;
            UpdatePatchFiles();
        }

        // Happens when choosing a game/patch file
        private void patchfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                PatchFile chosenPatch = (PatchFile)((ListBox)sender).SelectedItem;
                
                if (chosenPatch.Patches.Count <= 0)
                {
                    chosenPatch.PopulatePatches();
                }
                PatchFile.currentPatchFile = chosenPatch;
                patchOptionsItemsControl.DataContext = PatchFile.currentPatchFile;
                descriptionTextbox.DataContext = PatchFile.currentPatchFile;
            }
        }

        private void selectPatchFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Sigh. We have to use WinForms to just... select a folder. Why is such simple functionality missing from WPF?
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.Cancel || dialog.SelectedPath == "") { return; }
                PatchFolder.patchFolder.PortableFolder = dialog.SelectedPath;
                UpdatePatchFiles();
            }
        }

        private void portablePathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)
            {
                UpdatePatchFiles();
            }
        }

        public void UpdatePatchFiles(object sender = null, RoutedEventArgs e = null)
        {
            PatchFolder.patchFolder.PopulatePatchFolder();
            PatchFolder.patchFolder.PatchesEnabled = PatchFolder.patchFolder.GetIfPatchesEnabledInConfig();

        }

        private void ClearPatchFilesList(object sender = null, RoutedEventArgs e = null)
        {
            PatchFolder.patchFiles.Clear();
            if (PatchFile.currentPatchFile != null && PatchFile.currentPatchFile.Patches != null)
                PatchFile.currentPatchFile.Patches.Clear();
        }
    }

    public class PatchFolder : INotifyPropertyChanged
    {
        public static PatchFolder patchFolder;
        public event PropertyChangedEventHandler PropertyChanged;
        const string applyPatchesString = "apply_patches = ";

        public static ObservableCollection<PatchFile> patchFiles = new ObservableCollection<PatchFile>();
        const string NonPortableFolder = @"%homepath%\Documents\Xenia\patches\";
        const string RelativeXeniaConfigPath = @"\..\xenia-canary.config.toml";

        bool patchesEnabled;
        public bool PatchesEnabled { 
            get { return patchesEnabled; } 
            set
            {
                if (UpdateXeniaConfig(value))
                    patchesEnabled = value;
                else
                {
                    patchesEnabled = false;
                }
                OnPropertyChanged();
            }
        }

        string portableFolder;
        public string PortableFolder
        {
            get { return portableFolder; }
            set
            {
                portableFolder = value;
                OnPropertyChanged();
            }
        }

        bool foundPatchFolder;
        public bool FoundPatchFolder { 
            get { return foundPatchFolder; }
            set
            {
                foundPatchFolder = value;
                OnPropertyChanged();
            }
        }
        bool isPortable;
        public bool IsPortable
        {
            get { return isPortable; } 
            set 
            {
                isPortable = value;
                OnPropertyChanged();
            } 
        }

        public void PopulatePatchFolder()
        {
            string patchfolderpath = Environment.ExpandEnvironmentVariables(IsPortable ? PortableFolder : NonPortableFolder);
            if (!Directory.Exists(patchfolderpath))
            {
                FoundPatchFolder = false;
                return;
            }
            patchFiles.Clear();
            foreach (string filepath in Directory.GetFiles(patchfolderpath, "*.toml"))
            {
                patchFiles.Add(new PatchFile(filepath));
            }
            FoundPatchFolder = true;
            return;
        }

        // Called when ticking and unticking the Patches Enabled checkbox
        public bool UpdateXeniaConfig(bool enablePatches)
        {
            if (!FoundPatchFolder)
            {
                return false;
            }
            string finalConfigPath = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(IsPortable ? PortableFolder + RelativeXeniaConfigPath : NonPortableFolder + RelativeXeniaConfigPath));
            if (!File.Exists(finalConfigPath))
            {
                MessageBox.Show("Couldn't find xenia-canary.config.toml outside of patch folder!" + 
                    (IsPortable ? 
                        "\nMake sure your \"patches\" Folder is within the portable Xenia folder and you have run Xenia once." 
                        :
                        "\nThis shouldn't happen since the non-portable Xenia config is always just one level above the patch folder."),
                    "Error");
                return false;
            }

            string[] configfile = File.ReadAllLines(finalConfigPath);
            string finalConfigFile = "";
            bool foundApplyPatches = false;

            foreach (string thisLine in configfile)
            {
                int applyPatchesIndex = thisLine.IndexOf(applyPatchesString);
                string newPatchLine = "";
                if (applyPatchesIndex != -1)
                {
                    foundApplyPatches = true;
                    if (enablePatches)
                    {
                        newPatchLine = thisLine.Replace("false", "true");
                    }
                    else
                    {
                        newPatchLine = thisLine.Replace("true", "false");
                    }
                }
                else
                {
                    newPatchLine = thisLine;
                }
                finalConfigFile += newPatchLine + '\n';
            }
            if (foundApplyPatches)
            {
                File.WriteAllText(finalConfigPath, finalConfigFile);
            }
            else
            {
                MessageBox.Show("Failed to find apply_patches in the Canary config file!", "Error");
            }
            return foundApplyPatches;
        }

        // Called when updating the patch folder
        public bool GetIfPatchesEnabledInConfig()
        {
            string finalConfigPath = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(IsPortable ? PortableFolder + RelativeXeniaConfigPath : NonPortableFolder + RelativeXeniaConfigPath));
            if (!File.Exists(finalConfigPath))
            {
                return false;
            }
            string[] configfile = File.ReadAllLines(finalConfigPath);
            foreach (string line in configfile)
            {
                if (line.IndexOf(applyPatchesString) != -1)
                {
                    return line.IndexOf("true") != -1;
                }
            }
            return false;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public PatchFolder()
        {
            patchesEnabled = false;
        }
    }

    public class PatchFile
    {
        public static PatchFile currentPatchFile;
        public ObservableCollection<Patch> Patches { get; set; }
        public string filepath;
        public string GameName { get; }
        public const char commentChar = '#';
        public PatchFile(string path)
        {
            filepath = path;
            path = System.IO.Path.GetFileNameWithoutExtension(path);
            string tmpGameName = path;
            int indexOfPatchToml = path.IndexOf(".patch");
            if (indexOfPatchToml != -1)
            {
                tmpGameName = tmpGameName.Substring(0, indexOfPatchToml);
            }
            int titleidpos = path.IndexOf('-');
            if (titleidpos > 0)
                tmpGameName = tmpGameName.Substring(titleidpos + 2);
            GameName = tmpGameName;
            Patches = new ObservableCollection<Patch>();
        }

        public void PopulatePatches()
        {
            Patches.Clear();
            // Read the patchfile and parse it for each patch
            string patchfileText = File.ReadAllText(filepath);
            string[] allPatchSegments = patchfileText.Split("[[patch]]");
            if (allPatchSegments.Length < 2)
                return;
            string[] patchSegments = allPatchSegments[1..];
            foreach (string segment in patchSegments)
            {
                string name = "", desc = "", author = "";
                bool foundname = false, founddesc = false, foundenabled = false;
                bool is_enabled = false;
                string[] patchlines = segment.Split('\n');
                foreach (string line in patchlines)
                {
                    string finalLine = line;
                    int commentIndex = finalLine.IndexOf(commentChar);
                    if (commentIndex != -1)
                        finalLine = finalLine.Substring(0, commentIndex);

                    if (finalLine.Contains("name"))
                    {
                        foundname = true;
                        name = GetStringBetweenFirstQuotePair(finalLine);
                    }
                    else if (finalLine.Contains("is_enabled"))
                    {
                        foundenabled = true;
                        is_enabled = finalLine.Contains("true");
                    }
                    else if (finalLine.Contains("author"))
                    {
                        author = GetStringBetweenFirstQuotePair(finalLine);
                    }
                    else if (finalLine.Contains("desc"))
                    {
                        founddesc = true;
                        desc = GetStringBetweenFirstQuotePair(finalLine);
                    }
                }
                if (!foundname || !foundenabled || name == null)
                {
                    MessageBox.Show("Failed to parse a patch.\n" + (foundname ? "Patch's name:\n" + name : "") + (founddesc ? "Patch's description:\n" + desc : ""));
                    continue;
                }
                Patches.Add(new Patch(name, is_enabled, desc, author));
            }
        }

        public static string GetStringBetweenFirstQuotePair(string stringToSearch)
        {
            int firstQuoteIndex = stringToSearch.IndexOf('\"');
            if (firstQuoteIndex != -1)
            {
                int secondQuoteIndex = stringToSearch.IndexOf("\"", firstQuoteIndex+1);
                if (secondQuoteIndex != -1)
                {
                    return stringToSearch[(firstQuoteIndex+1)..secondQuoteIndex];
                }
            }
            return null;
        }
    }

    public class Patch : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Patch(string name, bool enabled, string description = "", string author = "")
        {
            PatchName = name;
            PatchDescription = description + (author != "" ? "\n\nAuthor: " + author : "");
            Author = author;
            isEnabled = enabled;
        }

        public string Author { get; }
        public string PatchName { get; }
        public string PatchDescription { get; }
        bool isEnabled;
        public bool IsEnabled { 
            get { return isEnabled; }
            set {
                // When the user ticks/unticks a patch's checkbox, the following function finds the patch's name in the patch file and changes it
                if (!SetEnabled(value))
                    value = false;
                isEnabled = value; 
                OnPropertyChanged(); 
            } 
        }

        public bool SetEnabled(bool enable)
        {
            string patchFilePath = PatchFile.currentPatchFile.filepath;
            string patchfileText = File.ReadAllText(patchFilePath);
            string[] patchSegments = patchfileText.Split("[[patch]]");
            string finalPatchText = patchSegments[0];
            if (patchSegments.Length < 2)
                return false;
            string[] mainPatchSegments = patchSegments[1..];
            bool success = false;
            
            foreach (string segment in mainPatchSegments)
            {
                bool foundname = false;
                bool foundenabled = false;
                string[] patchlines = segment.Split('\n');
                int is_enabledIndex = 1;
                List<string> finalSegmentList = new List<string>();
                finalSegmentList.Add("[[patch]]");
                foreach (string line in patchlines)
                {
                    string finalLine = line;
                    int commentIndex = finalLine.IndexOf(PatchFile.commentChar);
                    if (commentIndex != -1)
                        finalLine = finalLine.Substring(0, commentIndex);
                    string trimmedLine = finalLine.Trim();
                    if (trimmedLine.StartsWith("name"))
                    {
                        if (PatchFile.GetStringBetweenFirstQuotePair(finalLine) == PatchName)
                        {
                            foundname = true;
                            success = true;
                        }
                    }
                    // We need to track the is_enabled line, otherwise if we find is_enabled before the name we won't do anything. Though I don't think this is likely unless someone breaks the convention
                    if (trimmedLine.StartsWith("is_enabled"))
                    {
                        foundenabled = true;
                    }
                    if (!foundenabled)
                        is_enabledIndex++;
                    finalSegmentList.Add(finalLine + "\n");
                }

                if (foundname && foundenabled)
                {
                    if (enable)
                        finalSegmentList[is_enabledIndex] = finalSegmentList[is_enabledIndex].Replace("false", "true");
                    else
                        finalSegmentList[is_enabledIndex] = finalSegmentList[is_enabledIndex].Replace("true", "false");
                }
                // The last line in a patch segment is an empty line and the above loop adds a new line to it so we end up with \n\n, so remove one.
                if (finalSegmentList[^1] == "\n")
                    finalSegmentList[^1] = "";
                finalPatchText += String.Join("", finalSegmentList);
            }
            File.WriteAllText(patchFilePath, finalPatchText);
            return success;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}