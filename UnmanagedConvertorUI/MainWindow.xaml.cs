using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace UnmanagedConvertorUI
{
    public partial class MainWindow : Window
    {
        private string _dllpath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            var data = (string[])e.Data.GetData(DataFormats.FileDrop);

            var dllPath = data.FirstOrDefault();

            if (dllPath.EndsWith(".dll"))
            {
                _dllpath = dllPath;
                DllPathTextbox.Text = dllPath;
            }
        }

        private void SelectDllPath(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Открыть dll",
                Filter = "dll |*.dll"
            };
            if (openFileDialog.ShowDialog() == false) return;

            DllPathTextbox.Text = openFileDialog.FileName;
        }

        private void Build(object sender, RoutedEventArgs e)
        {
            if (_dllpath == null) return;

            UnmanagedBuilder.UnmanagedBuilder.Build(_dllpath);
        }
    }
}