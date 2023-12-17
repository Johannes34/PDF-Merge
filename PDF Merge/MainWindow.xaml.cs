using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PDF_Merge
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        #region Startup Directory

        private void Initialize()
        {
            try
            {
                // load last directory from registry:
                LastDirectory = Registry.CurrentUser.OpenSubKey("Software\\PDF Merge")?.GetValue("LastDirectory", String.Empty) as string;

                // fallback: use app startup directory:
                if (String.IsNullOrEmpty(LastDirectory))
                    LastDirectory = Environment.CurrentDirectory;

                // auto-select the two most recent pdfs from last directory:
                if (Directory.Exists(LastDirectory))
                {
                    var pdfs = Directory.GetFiles(LastDirectory, "*.pdf");
                    if (pdfs.Any())
                    {
                        var newestTwoPdfs = pdfs.OrderByDescending(p => File.GetCreationTime(p)).Take(2).ToArray();
                        if (newestTwoPdfs.Length == 2)
                        {
                            OddPagesPdfFile = newestTwoPdfs.ElementAt(0);
                            EvenPagesPdfFile = newestTwoPdfs.ElementAt(1);
                        }
                    }
                }

            }
            catch(Exception e)
            {
                LogError(e.Message);
            }
        }

        private void SaveLastDirectory()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("Software\\PDF Merge", true);
                if (key == null)
                    key = Registry.CurrentUser.CreateSubKey("Software\\PDF Merge", true);
                key.SetValue("LastDirectory", LastDirectory);
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private string m_LastDirectory;
        public string LastDirectory
        {
            get { return m_LastDirectory; }
            set
            {
                if (value != m_LastDirectory)
                {
                    m_LastDirectory = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastDirectory)));
                }
            }
        }

        #endregion

        #region Input Files & Options

        private string m_OddPagesPdfFile;
        public string OddPagesPdfFile
        {
            get { return m_OddPagesPdfFile; }
            set
            {
                if (value != m_OddPagesPdfFile)
                {
                    m_OddPagesPdfFile = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OddPagesPdfFile)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanMerge)));
                }
            }
        }

        private string m_EvenPagesPdfFile;
        public string EvenPagesPdfFile
        {
            get { return m_EvenPagesPdfFile; }
            set
            {
                if (value != m_EvenPagesPdfFile)
                {
                    m_EvenPagesPdfFile = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EvenPagesPdfFile)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanMerge)));
                }
            }
        }

        public bool CanMerge =>
            !String.IsNullOrEmpty(OddPagesPdfFile) && File.Exists(OddPagesPdfFile)
            && String.Equals(Path.GetExtension(OddPagesPdfFile), ".pdf", StringComparison.OrdinalIgnoreCase)
            && !String.IsNullOrEmpty(EvenPagesPdfFile) && File.Exists(EvenPagesPdfFile)
            && String.Equals(Path.GetExtension(EvenPagesPdfFile), ".pdf", StringComparison.OrdinalIgnoreCase);


        private bool m_ReverseEvenPdfPages = true;
        public bool ReverseEvenPdfPages
        {
            get { return m_ReverseEvenPdfPages; }
            set
            {
                if (value != m_ReverseEvenPdfPages)
                {
                    m_ReverseEvenPdfPages = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReverseEvenPdfPages)));
                }
            }
        }

        private bool m_DeleteSourceFiles;
        public bool DeleteSourceFiles
        {
            get { return m_DeleteSourceFiles; }
            set
            {
                if (value != m_DeleteSourceFiles)
                {
                    m_DeleteSourceFiles = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteSourceFiles)));
                }
            }
        }


        private void BrowseOddPagesPdf_Click(object sender, RoutedEventArgs e)
        {
            var ofd = GetOpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                LastDirectory = Path.GetDirectoryName(ofd.FileNames[0]);
                SaveLastDirectory();

                if (ofd.FileNames.Length == 1)
                {
                    OddPagesPdfFile = ofd.FileNames[0];
                }
                else
                {
                    SetAndOrderFiles(ofd.FileNames[0], ofd.FileNames[1]);
                }
            }
        }

        private void BrowseEvenPagesPdf_Click(object sender, RoutedEventArgs e)
        {
            var ofd = GetOpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                LastDirectory = Path.GetDirectoryName(ofd.FileNames[0]);
                SaveLastDirectory();

                if (ofd.FileNames.Length == 1)
                {
                    EvenPagesPdfFile = ofd.FileNames[0];
                }
                else
                {
                    SetAndOrderFiles(ofd.FileNames[0], ofd.FileNames[1]);
                }
            }
        }

        private OpenFileDialog GetOpenFileDialog()
        {
            return new OpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "PDF files (*.pdf)|*.pdf",
                InitialDirectory = LastDirectory,
                Multiselect = true,
                Title = "Select one or two pdf files to merge"
            };
        }

        private void SetAndOrderFiles(string file1, string file2)
        {
            // assuming usually one would begin the scanning with the first page, the
            // older file should always be the odd-pages one:
            var created1 = File.GetCreationTime(file1);
            var created2 = File.GetCreationTime(file2);

            if (created1 < created2)
            {
                OddPagesPdfFile = file1;
                EvenPagesPdfFile = file2;
            }
            else
            {
                OddPagesPdfFile = file2;
                EvenPagesPdfFile = file1;
            }
        }

        private void root_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length == 1)
                {
                    if (String.IsNullOrEmpty(OddPagesPdfFile))
                        OddPagesPdfFile = files[0];
                    else
                        EvenPagesPdfFile = files[0];
                }
                else if (files.Length == 2)
                    SetAndOrderFiles(files[0], files[1]);
            }
        }

        #endregion

        #region Log

        private ObservableCollection<ListViewItem> m_Log = new ObservableCollection<ListViewItem>();
        public ObservableCollection<ListViewItem> Log
        {
            get { return m_Log; }
            set
            {
                if (value != m_Log)
                {
                    m_Log = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Log)));
                }
            }
        }

        private void LogMessage(string text)
            => Log.Add(new ListViewItem() { Content = text, Foreground = Brushes.Black });

        private void LogError(string text)
            => Log.Add(new ListViewItem() { Content = text, Foreground = Brushes.Red });

        private Visibility m_ShowResultArea = Visibility.Collapsed;
        public Visibility ShowResultArea
        {
            get { return m_ShowResultArea; }
            set
            {
                if (value != m_ShowResultArea)
                {
                    m_ShowResultArea = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowResultArea)));
                }
            }
        }

        #endregion

        #region PDF Merge & Result

        private void Merge_Click(object sender, RoutedEventArgs e)
        {
            Log = new ObservableCollection<ListViewItem>();
            ShowResultArea =  Visibility.Visible;
            LastOutputFile = null;
            MergeNonDuplexBothSidedScans();
        }

        private void MergeNonDuplexBothSidedScans()
        {
            try
            {
                LogMessage("Merging files...");

                // contains all odd pages, e.g. 1, 3, 5, 7
                var master = PdfReader.Open(OddPagesPdfFile, PdfDocumentOpenMode.Modify);

                // contains all even pages in reverse order, e.g. 6, 4, 2
                var second = PdfReader.Open(EvenPagesPdfFile, PdfDocumentOpenMode.Import);

                // read pages:
                var secondPages = new List<PdfPage>();
                foreach (var page in second.Pages)
                    secondPages.Add(page);

                // invert optionally (default: yes):
                if (ReverseEvenPdfPages)
                    secondPages.Reverse();

                // insert pages into master:
                int insertIndex = 1;
                foreach (var page in secondPages)
                {
                    master.InsertPage(insertIndex, page);
                    insertIndex += 2;
                }

                LogMessage("Merge finished, saving pdf...");

                // save master as new file:
                var originalFile = new FileInfo(master.FullPath);
                var targetFile = Path.Combine(originalFile.Directory.FullName, Path.GetFileNameWithoutExtension(originalFile.Name) + "_merge.pdf");
                master.Save(targetFile);
                LastOutputFile = targetFile;
                LogMessage("File created: " + targetFile);

                if (DeleteSourceFiles)
                {
                    LogMessage("Moving original files to recycle bin...");
                    FileSystem.DeleteFile(OddPagesPdfFile, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    FileSystem.DeleteFile(EvenPagesPdfFile, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }

                LogMessage("Successfully finished.");
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        private string m_LastOutputFile;
        public string LastOutputFile
        {
            get { return m_LastOutputFile; }
            set
            {
                if (value != m_LastOutputFile)
                {
                    m_LastOutputFile = value;
                    HasLastOutputFile = !String.IsNullOrEmpty(value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastOutputFile)));
                }
            }
        }

        private bool m_HasLastOutputFile = false;
        public bool HasLastOutputFile
        {
            get { return m_HasLastOutputFile; }
            set
            {
                if (value != m_HasLastOutputFile)
                {
                    m_HasLastOutputFile = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLastOutputFile)));
                }
            }
        }


        private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = Path.GetDirectoryName(LastOutputFile);
            RunProcess(folder);
        }

        private void OpenOutputFile_Click(object sender, RoutedEventArgs e)
        {

            RunProcess(LastOutputFile);
        }

        private void RunProcess(string fileOrFolder)
        {
            try
            {
                var p = new Process();
                p.StartInfo = new ProcessStartInfo(fileOrFolder)
                {
                    UseShellExecute = true
                };
                p.Start();
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        #endregion


        #region Others

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            RunProcess(e.Uri.ToString());
        }

        #endregion
    }
}