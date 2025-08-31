using System.Net.NetworkInformation;
using SharpPcap;

namespace LogCheck.Models
{
    public class NetworkInterfaceItem
    {
        public ICaptureDevice? Device { get; set; }
        public string Name { get; set; } = string.Empty;
    public NetworkInterface? InterfaceInfo { get; set; }
        public bool IsActive { get; set; }
        public bool HasIPAddress { get; set; }
        public long Speed { get; set; }
        public NetworkInterfaceType InterfaceType { get; set; }
        public int Priority { get; set; } // 우선순위 속성 추가

        public NetworkInterfaceItem() { }

        public NetworkInterfaceItem(NetworkInterface ni)
        {
            InterfaceInfo = ni;
            Name = ni.Name;
            IsActive = ni.OperationalStatus == OperationalStatus.Up;
            Speed = ni.Speed;
            InterfaceType = ni.NetworkInterfaceType;

            // IP 주소 설정
            var ipProps = ni.GetIPProperties();
            var ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address;

            HasIPAddress = ipv4 != null;
        }
    }
}
