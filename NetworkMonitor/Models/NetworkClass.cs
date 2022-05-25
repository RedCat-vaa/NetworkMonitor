using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;


namespace NetworkMonitor.Models
{
    struct net_state
    {
        public bool inet_ok; // Флаг доступности сети
        public Dictionary<String, int> avg_rtts; // Словарь пинга до хостов
        public double packet_loss; // Потеря пакетов
        public DateTime measure_time; // Дата, время
        public int router_rtt; // 
    }

    internal class NetworkClass
    {
        private static StringBuilder strLog = new StringBuilder("");
        internal delegate void LogDelegate(StringBuilder message);
        internal static event LogDelegate LogEvent;

        internal static String HOST; // HTTP сервер, соединение до которого будем тестировать
        internal static int TIMEOUT; // Таймаут подключения
        internal static int PING_COUNT; // Количество пакетов пинга
        internal static int PING_DELAY; // Ожидание перед отправкой следующего пакета пинга
        internal static int MEASURE_DELAY; // Время между проверками
        internal static String ROUTER_IP; // IP роутера
        internal static double MAX_PKT_LOSS; // Максимально допустимый Packet loss
        
        // Промежуточные переменные
        internal static bool prev_inet_ok = true;
        internal static DateTime first_fail_time;
        internal static long total_time = 0;
        internal static int pkt_sent = 0;
        internal static int success_pkts = 0;
        internal static int exited_threads = 0;
        internal static Dictionary<string, int> measure_results = new Dictionary<string, int>();
        internal static bool AliveMonitor = false;

        public static async void AsyncStartMonitor()
        {
            await Task.Run(StartMonitor);
        }
        public static void StartMonitor()
        {
            var config = JsonConvert.DeserializeObject<Dictionary<String, Object>>(File.ReadAllText(@"Settings\DefaultSettings.json"));
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                if (addresses.Count > 0)
                {
                    foreach (GatewayIPAddressInformation address in addresses)
                    {
                        ROUTER_IP = address.Address.ToString();
                    }
                }
            }
            PING_COUNT = int.Parse((String)config["ping_count"]);
            PING_DELAY = int.Parse((String)config["ping_packet_delay"]);
            MAX_PKT_LOSS = double.Parse((String)config["nq_max_loss"]);
            AliveMonitor = true;

            while (AliveMonitor)
            {
                Monit();
                Thread.Sleep(MEASURE_DELAY);
            }

        }
        static void Save_log(net_state snapshot)
        {
            String rtts = "";
            int avg_rtt = 0;
            try
            {
                avg_rtt = snapshot.avg_rtts[HOST];
            }
            catch
            {
            }
            strLog.Append("Хост: " + HOST + "; ");
            strLog.Append("Доступ в интернет: " + snapshot.inet_ok.ToString() + "; ");
            strLog.Append("Пинг: " + avg_rtt.ToString() + ";");
            strLog.Append("Пинг до роутера: " + snapshot.router_rtt.ToString() + "; ");
            strLog.Append("Потери: " + snapshot.packet_loss.ToString());
            LogEvent?.Invoke(strLog);

            strLog.Clear();
        }

        static void Monit()
        {
            // Создаем экземпляр измерений.
            net_state snapshot = new net_state();
            snapshot.inet_ok = true;
            snapshot.measure_time = DateTime.Now;
            // Проверяем доступность роутера
            Ping ping = new Ping();
            var prr = ping.Send(ROUTER_IP, TIMEOUT);
            snapshot.router_rtt = prr.Status == IPStatus.Success ? (int)prr.RoundtripTime : TIMEOUT;
            if (prr.Status != IPStatus.Success)
            {
                snapshot.avg_rtts = new Dictionary<string, int>();
                snapshot.inet_ok = false;
                snapshot.packet_loss = 1;
                Save_log(snapshot);
                return;
            }
            snapshot.inet_ok = true;
            
            //Пингуем хост
            exited_threads = 0;
            pkt_sent = 0;
            success_pkts = 0;
            total_time = 0;
            measure_results = new Dictionary<string, int>();


            Thread thread = new Thread(new ParameterizedThreadStart(PingTest));
            thread.Start(HOST);
          

            while (exited_threads < 1) continue;
            //Анализируем результаты пинга
            snapshot.avg_rtts = measure_results;
            snapshot.packet_loss = (double)(pkt_sent - success_pkts) / pkt_sent;
            snapshot.inet_ok = !(
                 ((double)total_time / success_pkts >= 0.75 * TIMEOUT) ||
                snapshot.packet_loss >= MAX_PKT_LOSS ||
                snapshot.router_rtt == TIMEOUT);
            Save_log(snapshot);
            if (prev_inet_ok && !snapshot.inet_ok)
            {
                //Интернет был , но теперь неудачу
                prev_inet_ok = false;
                first_fail_time = DateTime.Now;
            }
            else if (!prev_inet_ok && snapshot.inet_ok)
            {
                String t_s = new TimeSpan(DateTime.Now.Ticks - first_fail_time.Ticks).ToString(@"hh\:mm\:ss");
                prev_inet_ok = true;

            }

        }

        static void PingTest(Object arg)
        {
            String host = (String)arg;
            int pkts_lost_row = 0;
            int local_success = 0;
            long local_time = 0;
            Ping ping = new Ping();
            
            // Запускаем пинг заданное количество раз.
            for (int i = 0; i < NetworkClass.PING_COUNT; i++)
            {
                // Если потеряно 3 пакеты, записываем результаты и выходим из цикла
                if (pkts_lost_row == 3)
                {
                    NetworkClass.measure_results.Add(host, (int)(local_time / (local_success == 0 ? 1 : local_success)));
                    NetworkClass.exited_threads++;
                    return;
                }
                try
                {
                    var result = ping.Send(host, NetworkClass.TIMEOUT);
                    // Если пинг прошел
                    if (result.Status == IPStatus.Success)
                    {
                        pkts_lost_row = 0;
                        local_success++;
                        // RoundtripTime Возвращает количество миллисекунд, затраченных на отправку Эхо-запроса
                        local_time += result.RoundtripTime;
                        NetworkClass.total_time += result.RoundtripTime;
                        NetworkClass.pkt_sent++;
                        NetworkClass.success_pkts++;
                    }
                    switch (result.Status)
                    {
                        case IPStatus.Success: break; //Already handled 
                        case IPStatus.BadDestination:

                            NetworkClass.measure_results.Add(host, -1);
                            NetworkClass.exited_threads++;
                            return;
                        case IPStatus.DestinationHostUnreachable:
                        case IPStatus.DestinationNetworkUnreachable:
                        case IPStatus.DestinationUnreachable:

                            NetworkClass.measure_results.Add(host, -1);
                            NetworkClass.exited_threads++;
                            return;
                        case IPStatus.TimedOut:
                            pkts_lost_row++;
                            NetworkClass.pkt_sent++;
                            break;
                        default:

                            NetworkClass.measure_results.Add(host, -1);
                            NetworkClass.exited_threads++;
                            return;
                    }
                }
                catch (Exception xc)
                {

                    NetworkClass.exited_threads++;
                    NetworkClass.measure_results.Add(host, -1);
                    return;
                }
            }
            NetworkClass.measure_results.Add(host, (int)(local_time / (local_success == 0 ? 1 : local_success)));
            NetworkClass.exited_threads++;
            return;
        }
    }

}
