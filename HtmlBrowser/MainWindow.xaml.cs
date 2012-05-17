using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;
using Awesomium.Core;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;


namespace HtmlBrowser
{
    public partial class MainWindow : Window
    {
        public string folder;
        public int fileIndex;
        public List<string> files = new List<string>();
        private NavigateToFileCommnand navigateToFileCommnand;
        private DeleteFileCommand deleteFileCommand;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            var setting = config.AppSettings.Settings["lastPath"];

            if (setting != null)
            {
                if (Directory.Exists(setting.Value))
                {
                    folder = setting.Value;
                    loadFiles(folder);
                }
            }

            browser.LoadCompleted += browser_LoadCompleted;
            browser.SetURLFilteringMode(URLFilteringMode.Whitelist);
            browser.AddURLFilter("file://*");

            deleteFileCommand = new DeleteFileCommand(this);

            navigateToFileCommnand = new NavigateToFileCommnand(this);
            navigateToPreviousBtn.CommandParameter = NavigationDirection.Previous;
            navigateToNextBtn.CommandParameter = NavigationDirection.Next;
            navigateToPreviousBtn.Command = navigateToFileCommnand;
            navigateToNextBtn.Command = navigateToFileCommnand;
            jumpToBtn.Command = navigateToFileCommnand;
            deleteFileBtn.CommandParameter = -1;
            deleteFileBtn.Command = deleteFileCommand;

            RegisterShortcut(this, new KeyGesture(Key.Right), navigateToFileCommnand, NavigationDirection.Next);
            RegisterShortcut(this, new KeyGesture(Key.Left), navigateToFileCommnand, NavigationDirection.Previous);
            RegisterShortcut(this, new KeyGesture(Key.Delete), deleteFileCommand, -1);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                files.Clear();
                fileIndex = 0;
                folder = dialog.SelectedPath;
                loadFiles(folder);
            }
        }

        private void loadFiles(string path)
        {
            try
            {
                foreach (var filename in Directory.GetFiles(path, "*.html"))
                {
                    files.Add(filename);
                }

                navigationStatusLbl.Content = files.Count + " documents loaded.";
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        public int getNavigatedDocumentIndex(NavigationDirection direction)
        {
            if (files.Count == 0) return -1;

            if (direction == NavigationDirection.Next)
            {
                fileIndex++;
                if (fileIndex >= files.Count)
                    fileIndex = 0;
            }
            else
            {
                fileIndex--;
                if (fileIndex < 0)
                    fileIndex = files.Count - 1;

            }
            return fileIndex;
        }

        public void navigateToIndexDocument(int index)
        {
            if (index > -1 && index < files.Count)
            {
                ShowDisabledOverlay();
                var filename = files[index];// Path.Combine(folder, files[index]);

                if (browser.IsLoadingPage)
                {
                    HideDisabledOverlay();
                    browser.Stop();
                }

                browser.LoadURL(filename);
            }
        }

        private void browser_LoadCompleted(object sender, EventArgs e)
        {
            HideDisabledOverlay();
        }


        private LoadingAdorner loadingAdorner;

        private void HideDisabledOverlay()
        {
            if (loadingAdorner != null)
            {
                browser.Dispatcher.BeginInvoke((Action)(() =>
                {
                    AdornerLayer parentAdorner = AdornerLayer.GetAdornerLayer(browser);

                    parentAdorner.Remove(loadingAdorner);
                }), DispatcherPriority.Input);
            }
        }

        private void ShowDisabledOverlay()
        {
            loadingAdorner = new LoadingAdorner(browser);
            browser.Dispatcher.BeginInvoke((Action)(() =>
            {
                AdornerLayer parentAdorner = AdornerLayer.GetAdornerLayer(browser);

                parentAdorner.Add(loadingAdorner);
            }), DispatcherPriority.Input);
        }
        public static void RegisterShortcut(Control control, KeyGesture keyGesture, ICommand command, object commandParameter)
        {
            KeyBinding binding = new KeyBinding(command, keyGesture);
            binding.CommandParameter = commandParameter;

            KeyBinding bind = control.InputBindings.OfType<KeyBinding>().FirstOrDefault(
                 kb =>
                 kb.Command == command && kb.CommandParameter == commandParameter && kb.Key == keyGesture.Key &&
                 kb.Modifiers == keyGesture.Modifiers);

            if (bind != null)
                control.InputBindings.Remove(bind);

            control.InputBindings.Add(binding);
        }
        public void setIndexTextBox(int index)
        {
            jumpToLbl.Text = index.ToString();
        }

        private void jumpToBtn_Click(object sender, RoutedEventArgs e)
        {
            int index = 0;
            if (int.TryParse(jumpToLbl.Text, out index))
            {
                navigateToFileCommnand.Execute(index);
            }
        }

        private void jumpToLbl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                int index = 0;
                if (int.TryParse(jumpToLbl.Text, out index))
                {
                    navigateToFileCommnand.Execute(index);
                }
            }
        }



        private void Window_Closed(object sender, EventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove("lastPath");
            config.AppSettings.Settings.Add("lastPath", folder);
            config.Save();
        }
    }

    public class DeleteFileCommand : ICommand
    {
        private MainWindow window;
        public DeleteFileCommand(MainWindow mainWindow)
        {
            window = mainWindow;
        }

        public void Execute(object parameter)
        {
            if (window.fileIndex < 0) return;
            try
            {
                string filename = window.files[window.fileIndex];
                window.files.RemoveAt(window.fileIndex);
                File.Delete(filename);

                window.navigateToIndexDocument(window.getNavigatedDocumentIndex((NavigationDirection) parameter));
                window.setIndexTextBox(window.fileIndex);
                window.navigationStatusLbl.Content = window.fileIndex + "/" + window.files.Count;
                if (window.files.Count > 0)
                    window.currentFileLbl.Content = String.Format("({0})", window.files[window.fileIndex]);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;
    }

    public class NavigateToFileCommnand : ICommand
    {
        private MainWindow window;
        public NavigateToFileCommnand(MainWindow mainWindow)
        {
            window = mainWindow;
        }

        public void Execute(object parameter)
        {
            if (parameter is NavigationDirection)
            {
                window.navigateToIndexDocument(window.getNavigatedDocumentIndex((NavigationDirection)parameter));
            }
            else if (parameter is int)
            {
                window.navigateToIndexDocument((int)parameter);
            }

            window.setIndexTextBox(window.fileIndex);
            window.navigationStatusLbl.Content = window.fileIndex + "/" + window.files.Count;
            if (window.files.Count > 0)
                window.currentFileLbl.Content = String.Format("({0})", window.files[window.fileIndex]);
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;
    }

    public class LoadingAdorner : Adorner
    {
        public LoadingAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }
        protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.LightGray, new Pen(Brushes.Transparent, 0),

                        new Rect(new Point(0, 0), DesiredSize));

            base.OnRender(drawingContext);
        }
    }

    public enum NavigationDirection
    {
        Previous,
        Next
    }
}
