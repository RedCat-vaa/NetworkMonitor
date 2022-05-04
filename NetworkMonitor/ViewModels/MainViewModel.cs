using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading.Tasks;
using NetworkMonitor.Models;
using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace NetworkMonitor.ViewModels
{
  
    public class MainViewModel : INotifyPropertyChanged
    {
        MainWindow MainWin;

        string host;
        int port;
        int timeout;
        string routerIP;
        int delay;
        object locker = new object();

        public ObservableCollection<IpLAN> ListIP;

        public string Host
        {
            get
            {
                return host;
            }
            set 
            {
                host = value;
                NetworkClass.HTTP_TEST_HOST = value;
                OnPropertyChanged("Host");
            }
        }

        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;
                NetworkClass.HTTP_TEST_PORT = value;
                OnPropertyChanged("Port");
            }
        }

        public int Timeout
        {
            get
            {
                return timeout;
            }
            set
            {
                timeout = value;
                NetworkClass.TIMEOUT = value;
                OnPropertyChanged("Timeout");
            }
        }

        public string RouterIP
        {
            get
            {
                return routerIP;
            }
            set
            {
                routerIP = value;
                NetworkClass.ROUTER_IP = value;
                OnPropertyChanged("RouterIP");
            }
        }

        public int Delay
        {
            get
            {
                return delay;
            }
            set
            {
                delay = value;
                NetworkClass.MEASURE_DELAY = value;
                OnPropertyChanged("Delay");
            }
        }

        
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        internal MainViewModel(MainWindow mainWin)
        {
            MainWin = mainWin;
            NetworkClass.LogEvent += HandlerLog;
            Scaner.eventIP += HandlerIP;
            Scaner.eventClear += HandlerClearList;
            Scaner.eventProgress += HandlerProgress;

            ListIP = new ObservableCollection<IpLAN>();
            MainWin.IpList.ItemsSource = ListIP;

            var config = JsonConvert.DeserializeObject<Dictionary<String, Object>>(File.ReadAllText(@"Settings\DefaultSettings.json"));
            NetworkClass.HTTP_TEST_HOST = (String)config["http_test_host"];
            NetworkClass.ROUTER_IP = (String)config["router_ip"];
            NetworkClass.HTTP_TEST_PORT = int.Parse((String)config["http_test_port"]);
            NetworkClass.TIMEOUT = int.Parse((String)config["timeout"]);
            NetworkClass.PING_COUNT = int.Parse((String)config["ping_count"]);
            NetworkClass.PING_DELAY = int.Parse((String)config["ping_packet_delay"]);
            NetworkClass.MEASURE_DELAY = int.Parse((String)config["measure_delay"]);
            NetworkClass.MAX_PKT_LOSS = double.Parse((String)config["nq_max_loss"]);

            Host = NetworkClass.HTTP_TEST_HOST;
            Port = NetworkClass.HTTP_TEST_PORT;
            Timeout = NetworkClass.TIMEOUT;
            Delay = NetworkClass.MEASURE_DELAY;
            RouterIP = NetworkClass.ROUTER_IP;
        }

        private void HandlerLog(StringBuilder message)
        {
            MainWin.Dispatcher?.Invoke(new Action(() => MainWin.LogMonitor.Text += message + Environment.NewLine));
        }

        private void HandlerIP(IpLAN ip)
        {
            lock(locker)
            {
                MainWin.Dispatcher.Invoke(() => ListIP.Add(ip));
            } 
        }

        private void HandlerClearList()
        {
            lock (locker)
            {
                MainWin.Dispatcher.Invoke(() => ListIP.Clear());
            }
        }

        private void HandlerProgress(int i)
        {
            MainWin.Dispatcher.Invoke(() => MainWin.ProgressScaner.Value = i);
           
        }

        private RelayCommand _MonitorCommandStart;
        public RelayCommand MonitorCommandStart
        {
            get
            {
                return _MonitorCommandStart = _MonitorCommandStart ??
                  new RelayCommand(MonitorCommand, CanMonitorCommand);
            }
        }
        private bool CanMonitorCommand()
        {
            return true;
        }
        private void MonitorCommand()
        {
            NetworkClass.AsyncStartMonitor();
        }

        private RelayCommand _MonitorCommandStop;
        public RelayCommand MonitorCommandStop
        {
            get
            {
                return _MonitorCommandStop = _MonitorCommandStop ??
                  new RelayCommand(StopMonitorCommand, CanStopMonitorCommand);
            }
        }
        private bool CanStopMonitorCommand()
        {
            return true;
        }
        private void StopMonitorCommand()
        {
            NetworkClass.AliveMonitor = false;
        }


        private RelayCommand _ScanCommand;
        public RelayCommand ScanCommand
        {
            get
            {
                return _ScanCommand = _ScanCommand ??
                  new RelayCommand(ScanCommandExecute, CanScanCommand);
            }
        }
        private bool CanScanCommand()
        {
            return true;
        }
        private void ScanCommandExecute()
        {
            Scaner.AsyncStartScaner();           
        }
    }


}
