using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Runtime.InteropServices;



namespace NetworkMonitor.Models
{
    public class IpLAN
    {
        public string IP { get; set; }
        public string Device { get; set; }

        public string MAC { get; set; }

        public IpLAN(string ip = "", string device = "", string mac = "")
        {
            IP = ip;
            Device = device;
            MAC = mac;
        }

    }
    public class Scaner
    {
        public delegate void NewIP(IpLAN ip);
        public delegate void ScanerDelegate();
        public delegate void ScanerDelegateProgress(int i);
        public static event NewIP eventIP;
        public static event ScanerDelegate eventClear;
        public static event ScanerDelegateProgress eventProgress;
        internal static bool AliveScaner = false;
        static Mutex mutex1 = new Mutex();
        public static async void AsyncStartScaner()
        {
            await Task.Run(StartScaner);
        }

        
        public static void StartScaner()
        {

            AliveScaner = true;
            mutex1.WaitOne();
            eventClear?.Invoke();
            bool SuccesPing = false;
            string mac = ""; string host = "";
            string GatewayIP = "";
            string Host = System.Net.Dns.GetHostName();
            string IP = System.Net.Dns.GetHostByName(Host).AddressList[0].ToString();
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                return;

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                if (addresses.Count > 0)
                {
                    foreach (GatewayIPAddressInformation address in addresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            GatewayIP = address.Address.ToString();
                            eventIP?.Invoke(new IpLAN(address.Address.ToString(), "Основной шлюз", PingCheck.ARP(GatewayIP)));
                            break;
                        }
                        
                    }
                }
                if (GatewayIP!="") break;
            }

            if (GatewayIP!="")
            {
                string[] IpSplit = GatewayIP.Split(".");
                if (IpSplit.Length != 4) return;
                
                for (int i = 1; i <= 255; i++)
                {
                    if (!AliveScaner) return;
                    eventProgress?.Invoke(i);
                    string ipnum = IpSplit[0] + "." + IpSplit[1] + "." + IpSplit[2] + "." + i;
                    if (GatewayIP == ipnum) continue;
                    SuccesPing = false;
                    mac = "";
                    host = "";
                    PingCheck pch = new PingCheck(ipnum, ref SuccesPing, ref mac, ref host);
                    if (SuccesPing)
                    {
                        var ipCheck = System.Net.Dns.GetHostByName(Host).AddressList.Where(adr => adr.ToString() == ipnum);
                        if (ipCheck.Count() > 0)
                        {
                            eventIP?.Invoke(new IpLAN(ipnum, Host, mac));
                        }
                        else
                        {
                            eventIP?.Invoke(new IpLAN(ipnum, host, mac));
                        }
                    }
                }
            }
            mutex1.ReleaseMutex();
        }
    }

    class PingCheck
    {
        public PingCheck(string ip, ref bool succesPing, ref string mac, ref string host)
        {
            Ping ping = new Ping();
            PingReply pingReply = null;
            pingReply = ping.Send(ip, 300);
            if (pingReply.Status == IPStatus.Success)
            {
                succesPing = true;
                mac = ARP(ip);
            
                try
                {
                    IPAddress addr = IPAddress.Parse(ip);
                    IPHostEntry entry = Dns.GetHostEntry(addr);
                    host = entry.HostName;
                }
                catch (Exception ex)
                { }
                
            }
        }

        public static string ARP(string IP)
        {
            string mac = "";
            IPAddress dst = IPAddress.Parse(IP);

            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)macAddr.Length;

            try
            {
                if (SendARP(BitConverter.ToInt32(dst.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen) != 0)
                    throw new InvalidOperationException("SendARP failed.");
            }
            catch
            { }
           

            string[] str = new string[(int)macAddrLen];
            for (int i = 0; i < macAddrLen; i++)
            {
                str[i] = macAddr[i].ToString("x2");
                if (mac != "")
                {
                    mac += "-" + str[i];
                }
                else
                {
                    mac += str[i];
                }

            }
            return mac;
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int destIp, int srcIP, byte[] macAddr, ref uint physicalAddrLen);

    }
}
