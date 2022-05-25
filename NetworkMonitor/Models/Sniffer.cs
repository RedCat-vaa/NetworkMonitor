using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Collections.ObjectModel;

namespace NetworkMonitor.Models
{
    public class DeviceNet
    {
        public string InfoDevice { get; set; }
        public bool IsChecked { get; set; }

        public ICaptureDevice Device { get; set; }
    }

    public class PacketNet
    {
        public ICaptureDevice captureDevice;
        public string IpSource { get; set; }
        public string IpDest { get; set; }

        public string PortSource { get; set; }
        public string PortDest { get; set; }
        public string Data { get; set; }

        public PacketNet(string IpSource = "", string IpDest = "", string PortSource = "", string PortDest = "", string Data = "")
        {
            this.IpSource = IpSource;
            this.IpDest = IpDest;
            this.PortSource = PortSource;
            this.PortDest = PortDest;
            this.Data = Data;
        }

    }
    internal class Sniffer
    {
        public static ICaptureDevice captureDevice;
        public delegate void InfoPacket(PacketNet packet);
        public static event InfoPacket EventPacket;
        public static ObservableCollection<DeviceNet> GetDevices()
        {
            ObservableCollection<DeviceNet> devices = new ObservableCollection<DeviceNet>();
            try
            {
                CaptureDeviceList deviceList = CaptureDeviceList.Instance;
                foreach (ICaptureDevice device in deviceList)
                {
                    devices.Add(new DeviceNet { InfoDevice = device.Description, IsChecked = false, Device = device });
                }
            }
            catch(Exception ex)
            {

            }
           
            return devices;


        }

        public static async void AsyncStartSniffer(ObservableCollection<DeviceNet> ListDevice)
        {
            await Task.Run(()=>StartSniffer(ListDevice));
        }
        public static  void StartSniffer(ObservableCollection<DeviceNet> ListDevice)
        {
            bool FirstStart = false;
            if (captureDevice==null)
            {
                FirstStart = true;
            }
            foreach (DeviceNet device in ListDevice)
            {
                if (device.IsChecked)
                {
    
                    captureDevice = device.Device;
                    break;
                }
            }
            if (FirstStart&&captureDevice!=null)
            {
                captureDevice.OnPacketArrival += new PacketArrivalEventHandler(Program_OnPacketArrival);
            }
            DeviceConfiguration DeviceConfig = new DeviceConfiguration();
            DeviceConfig.Mode = DeviceModes.Promiscuous;
            captureDevice?.Open(DeviceConfig);
            try
            {
                 captureDevice?.StartCapture();
            }
            catch { captureDevice?.Close(); captureDevice?.Dispose(); }
           
        }

        public static void Program_OnPacketArrival(object sender, PacketCapture e)
        {

            Packet packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
            TcpPacket tcpPacket = new TcpPacket(new PacketDotNet.Utils.ByteArraySegment(packet.Bytes));
            IPv4Packet ipPacket = new IPv4Packet(new PacketDotNet.Utils.ByteArraySegment(packet.Bytes));
            if (tcpPacket != null && ipPacket != null)
            {

                DateTime time = e.GetPacket().Timeval.Date;
                int len = e.GetPacket().Data.Length;
                // IP адрес отправителя
                var srcIp = ipPacket.SourceAddress.ToString();
                // IP адрес получателя
                var dstIp = ipPacket.DestinationAddress.ToString();

                // порт отправителя
                var srcPort = tcpPacket.SourcePort.ToString();
                // порт получателя
                var dstPort = tcpPacket.DestinationPort.ToString();
                // данные пакета
                var data = Encoding.UTF8.GetString(tcpPacket.Bytes);

                EventPacket?.Invoke(new PacketNet(srcIp, dstIp, srcPort, dstPort, data));
            }
        }
    }
}
