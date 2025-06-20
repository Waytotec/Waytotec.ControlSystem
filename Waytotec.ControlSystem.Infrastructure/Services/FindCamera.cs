using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Waytotec.ControlSystem.Infrastructure.Services
{
    public class FindCamera
    {
        // 찾은 카메라 정보 메인 화면으로 던진다.
        public delegate void CallSendMessage(_waytocam wt, string ip);
        public static event CallSendMessage CallSendMsg;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct _waytocam
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
            public byte[] id1;

            public int version;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
            public byte[] mac;

            public uint ipAddress;                  /* IP Address */
            public uint ipMask;                     /* Subnet Mask */
            public uint ipGateway;

            //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
            //public byte[] r5;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] name;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] user;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] pass;

            public uint sig;                        /* Signature */

            public uint useWlan;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] ssid;
            public uint authtype;
            public uint keytype;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
            public byte[] key;
            public int http_port1;
            public int rtsp_port1;
            public int httpjpegport;
            public int ptzport;
        };

        public struct _waytocam2
        {
            public uint msg;
            public uint version;
            public _waytocam cfgNvRam;
        }

        byte[] wayto_data = {
                                0x47, 0x43, 0x46, 0x47, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x43, 0x46, 0x47, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                            };


        public const int PORT_NUMBER = 20011;
        UdpClient udp = null;
        IAsyncResult ar_ = null;

        public FindCamera()
        {
            //try
            //{
            //    udp = new UdpClient();
            //    IPEndPoint localEp = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
            //    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            //    udp.ExclusiveAddressUse = false;
            //    udp.Client.Bind(localEp);

            //    //  send_command();

            //    StartListening();
            //}
            //catch (Exception)
            //{
            //}
        }

        public void StartBind()
        {
            try
            {
                udp = new UdpClient();
                IPEndPoint localEp = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                udp.ExclusiveAddressUse = false;
                udp.Client.Bind(localEp);
                //  send_command();

                StartListening();
            }
            catch (Exception)
            {

            }
        }

        private void StartListening()
        {
            ar_ = udp.BeginReceive(Receive, new object());
        }

        private void check_camera(byte[] data, string ip)
        {
            if (data.Length < Marshal.SizeOf(typeof(_waytocam)))
                return;

            _waytocam wt = new _waytocam();
            wt = BytesToStructure<_waytocam>(data, 0);


            bool b_ok1 = false;

            for (int i = 0; i < 6; i++)
            {
                if (wt.mac[i] != 0xff)
                {
                    b_ok1 = true;
                    break;
                }
            }
            if (!b_ok1 && wt.http_port1 == 0)
                return;

            CallSendMsg(wt, ip);
        }

        private void Receive(IAsyncResult ar)
        {
            try
            {
                if (udp != null)
                {
                    IPEndPoint ip = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
                    byte[] bytes = udp.EndReceive(ar, ref ip);

                    check_camera(bytes, ip.ToString());
                    StartListening();

                }
            }
            catch (SocketException se)
            {
                Trace.WriteLine(string.Format("SocketException : {0}", se.Message));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("Exception : {0}", ex.Message));
            }
        }
        public void Stop()
        {
            try
            {
                if (udp != null)
                {
                    udp.Close();
                    udp = null;
                }
            }
            catch { }
        }

        public void send_command()
        {
            if (udp == null) return;
            IPEndPoint ip = new IPEndPoint(IPAddress.Parse("255.255.255.255"), PORT_NUMBER);
            udp.Send(wayto_data, wayto_data.Length, ip);
        }

        public static T BytesToStructure<T>(byte[] arr, int offset, int size = 0)
        {
            if (size == 0)
                size = Marshal.SizeOf(typeof(T));
            T str = (T)Activator.CreateInstance(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(arr, offset, ptr, size);
            str = (T)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);
            return str;
        }
    }
}
