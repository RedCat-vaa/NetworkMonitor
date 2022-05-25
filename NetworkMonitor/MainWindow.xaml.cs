using System;
using System.Collections.Generic;
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
using NetworkMonitor.ViewModels;

namespace NetworkMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        MainViewModel MVM;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += SaveOptions;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MVM = new MainViewModel(this);
            this.DataContext = MVM;
        }
        
        private void SaveOptions(object sender, EventArgs e)
        {
            MVM.SaveOptions();
        }

        
    }
}
