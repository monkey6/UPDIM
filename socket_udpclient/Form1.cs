using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Configuration;

namespace socket_udpclient
{
    public partial class Form1 : Form
    {

        private UdpClient receiveClient;
        private UdpClient sendClient;

        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        [DllImport("user32.dll")]
        public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public enum falshType : uint
        {
            FLASHW_STOP = 0,    //停止闪烁
            FALSHW_CAPTION = 1,  //只闪烁标题
            FLASHW_TRAY = 2,   //只闪烁任务栏
            FLASHW_ALL = 3,     //标题和任务栏同时闪烁
            FLASHW_PARAM1 = 4,
            FLASHW_PARAM2 = 12,
            FLASHW_TIMER = FLASHW_TRAY | FLASHW_PARAM1,   //无条件闪烁任务栏直到发送停止标志或者窗口被激活，如果未激活，停止时高亮
            FLASHW_TIMERNOFG = FLASHW_TRAY | FLASHW_PARAM2  //未激活时闪烁任务栏直到发送停止标志或者窗体被激活，停止后高亮
        }

        public static bool flashTaskBar(IntPtr hWnd, falshType type)
        {
            FLASHWINFO fInfo = new FLASHWINFO();
            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;//要闪烁的窗口的句柄，该窗口可以是打开的或最小化的
            fInfo.dwFlags = (uint)type;//闪烁的类型
            fInfo.uCount = UInt32.MaxValue;//闪烁窗口的次数
            fInfo.dwTimeout = 0; //窗口闪烁的频度，毫秒为单位；若该值为0，则为默认图标的闪烁频度
            return FlashWindowEx(ref fInfo);
        }

        public class Flash
        {
            public IntPtr p;
            public falshType ft;
            public Flash(IntPtr p, falshType ft)
            {
                this.p = p;
                this.ft = ft;
            }
        }
        public Form1()
        {
            InitializeComponent();
            if(!(ConfigurationManager.AppSettings["LastIP"]=="Default"))
            {
                tbxRemoteIP.Text = ConfigurationManager.AppSettings["LastIP"];
            }
            IPEndPoint localIpPoint = new IPEndPoint(IPAddress.Any, 51883);
            receiveClient = new UdpClient(localIpPoint);
            Thread receiveThread = new Thread(ReceiveMessage);
            receiveThread.Start(new Flash(this.Handle,falshType.FLASHW_TIMERNOFG));
        }

        //接受消息的方法
        public void ReceiveMessage(object obj1)
        {
            Flash f = (Flash)obj1;
            IPEndPoint remoteIpPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    byte[] data = receiveClient.Receive(ref remoteIpPoint);
                    string message = Encoding.Unicode.GetString(data);
                    ShowMessageforView(rtbxMessage, string.Format("{0}[{1}]\r\n{2}", remoteIpPoint,DateTime.Now.ToLongTimeString(), message));
                    flashTaskBar(f.p, f.ft);
                }
                catch
                {
                    break;
                }
            }
        }

        // 利用委托回调机制实现界面上消息内容显示
        delegate void ShowMessageforViewCallBack(RichTextBox rtbx, string text);
        private void ShowMessageforView(RichTextBox rtbx, string text)
        {
            if (rtbx.InvokeRequired)
            {
                ShowMessageforViewCallBack showMessageforViewCallback = ShowMessageforView;
                rtbx.Invoke(showMessageforViewCallback, new object[] { rtbx, text });
            }
            else
            {
                rtbxMessage.AppendText(text);
                rtbxMessage.AppendText("\r\n\r\n");
                rtbxMessage.ScrollToCaret();
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if(!Regex.IsMatch(tbxRemoteIP.Text, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$"))
            {
                MessageBox.Show("请输入正确的IP地址", "提示");
                return;
            }
            else
            {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                conf.AppSettings.Settings["LastIP"].Value = tbxRemoteIP.Text;
                conf.Save();
            }

            if (tbxMessage.Text == string.Empty)
            {
                MessageBox.Show("发送内容不能为空", "提示");
                return;
            }
            //获取本机的IP
            string hostName = Dns.GetHostName();   //獲取本機名     
            IPHostEntry heserver = Dns.GetHostEntry(hostName);
            foreach (IPAddress curAdd in heserver.AddressList)
            {
                if (curAdd.AddressFamily == AddressFamily.InterNetwork)
                {
                    sendClient = new UdpClient(new IPEndPoint(curAdd, 0));
                }
            }
            Thread sendThread = new Thread(SendMessage);
            sendThread.Start(tbxMessage.Text);
        }

        // 发送消息方法
        private void SendMessage(object obj)
        {
            string message = (string)obj;
            byte[] sendbytes = Encoding.Unicode.GetBytes(message);
            IPAddress remoteIp = IPAddress.Parse(tbxRemoteIP.Text);
            IPEndPoint remoteIpEndPoint = new IPEndPoint(remoteIp, 51883);
            sendClient.Send(sendbytes, sendbytes.Length, remoteIpEndPoint);
            sendClient.Close();
            ShowMessageforView(rtbxMessage, "我["+DateTime.Now.ToLongTimeString()+"]\r\n"+message);
            // 清空发送消息框
            ResetMessageText(tbxMessage);
        }

        // 采用了回调机制
        // 使用委托实现跨线程界面的操作方式
        delegate void ResetMessageCallback(TextBox textbox);
        private void ResetMessageText(TextBox textbox)
        {
            // Control.InvokeRequired属性代表
            // 如果控件的处理与调用线程在不同线程上创建的，则为true,否则为false
            if (textbox.InvokeRequired)
            {
                ResetMessageCallback resetMessagecallback = ResetMessageText;
                textbox.Invoke(resetMessagecallback, new object[] { textbox });
            }
            else
            {
                textbox.Clear();
                textbox.Focus();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            receiveClient.Close();
        }
    }
}
