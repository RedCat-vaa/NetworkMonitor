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
        int timeout;
        string routerIP;
        int delay;
        object locker = new object();

        public ObservableCollection<IpLAN> ListIP;
        public ObservableCollection<DeviceNet> ListDevice;
        public ObservableCollection<PacketNet> ListPacket;

        public string Host
        {
            get
            {
                return host;
            }
            set 
            {
                host = value;
                NetworkClass.HOST = value;
                OnPropertyChanged("Host");
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
            Sniffer.EventPacket += HandlerSniffer;

            ListIP = new ObservableCollection<IpLAN>();
            ListPacket = new ObservableCollection<PacketNet>();
            ListDevice = Sniffer.GetDevices();
            MainWin.IpList.ItemsSource = ListIP;
            MainWin.DeviceList.ItemsSource = ListDevice;
            MainWin.SnifferList.ItemsSource = ListPacket;

            var config = JsonConvert.DeserializeObject<Dictionary<String, Object>>(File.ReadAllText(@"Settings\DefaultSettings.json"));
            NetworkClass.HOST = (String)config["host"];
            NetworkClass.ROUTER_IP = (String)config["router_ip"];
            NetworkClass.TIMEOUT = int.Parse(Convert.ToString(config["timeout"]));
            NetworkClass.PING_COUNT = int.Parse((String)config["ping_count"]);
            NetworkClass.PING_DELAY = int.Parse((String)config["ping_packet_delay"]);
            NetworkClass.MEASURE_DELAY = int.Parse(Convert.ToString(config["measure_delay"]));
            NetworkClass.MAX_PKT_LOSS = double.Parse((String)config["nq_max_loss"]);

            Host = NetworkClass.HOST;
            Timeout = NetworkClass.TIMEOUT;
            Delay = NetworkClass.MEASURE_DELAY;
        }


        ///////// Команды
        private RelayCommand _MonitorCommandStart;
        public RelayCommand MonitorCommandStart
        {
            get
            {
                return _MonitorCommandStart = _MonitorCommandStart ??
                  new RelayCommand(MonitorCommand, () => true);
            }
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
                  new RelayCommand(StopMonitorCommand, () => true);
            }
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
                  new RelayCommand(ScanCommandExecute, () => true);
            }
        }
        private void ScanCommandExecute()
        {
            Scaner.AsyncStartScaner();           
        }

        private RelayCommand _ScanCommandStop;
        public RelayCommand ScanCommandStop
        {
            get
            {
                return _ScanCommandStop = _ScanCommandStop ??
                  new RelayCommand(ScanCommandStopExecute, () => true);
            }
        }
        private void ScanCommandStopExecute()
        {
            Scaner.AliveScaner = false;
            MainWin.ProgressScaner.Value = 255;
        }
        private RelayCommand _SnifferCommandStart;
        public RelayCommand SnifferCommandStart
        {
            get
            {
                return _SnifferCommandStart = _SnifferCommandStart ??
                  new RelayCommand(SnifferCommand, () => true);
            }
        }
        private void SnifferCommand()
        {
            lock (locker)
            {
                MainWin.Dispatcher.Invoke(() => ListPacket.Clear());
            }
            Sniffer.AsyncStartSniffer(ListDevice);
        }

        private RelayCommand _SnifferCommandStop;
        public RelayCommand SnifferCommandStop
        {
            get
            {
                return _SnifferCommandStop = _SnifferCommandStop ??
                  new RelayCommand(SnifferStopCommand, () => true);
            }
        }
        private void SnifferStopCommand()
        {
            Sniffer.captureDevice.StopCapture();
        }
        /////////

        /////////Обработчики
        private void HandlerIP(IpLAN ip)
        {
            lock (locker)
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
        private void HandlerLog(StringBuilder message)
        {
            MainWin.Dispatcher?.Invoke(new Action(() => MainWin.LogMonitor.Text += message + Environment.NewLine));
        }

        private void HandlerSniffer(PacketNet packet)
        {
            lock (locker)
            {
                MainWin.Dispatcher.Invoke(() => ListPacket.Add(packet));
                var LastItem = MainWin.SnifferList.Items[MainWin.SnifferList.Items.Count - 1];
                MainWin.Dispatcher.Invoke(() => MainWin.SnifferList.ScrollIntoView(LastItem));
                MainWin.Dispatcher.Invoke(() => MainWin.SnifferList.UpdateLayout());

            }
        }
        public  void SaveOptions()
        {
            try
            {
                var config = JsonConvert.DeserializeObject<Dictionary<String, Object>>(File.ReadAllText(@"Settings\DefaultSettings.json"));
                config["host"] = Host;
                config["timeout"] = Timeout;
                config["measure_delay"] = Delay;
                string JsonFile = JsonConvert.SerializeObject(config);
                using (StreamWriter sw = new StreamWriter(@"Settings\DefaultSettings.json", false))
                {
                    sw.Write(JsonFile);
                }
            }
            catch { }
           
        }
        /////////

    }


}
