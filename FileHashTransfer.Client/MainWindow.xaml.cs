using FileHashTransfer.Client.ViewModels;
using System.Windows;

namespace FileHashTransfer.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}