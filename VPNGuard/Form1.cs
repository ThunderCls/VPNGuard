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
using System.IO;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Management;
using System.Net.Cache;
using System.Runtime.InteropServices;

namespace VPNGuard
{
    public partial class Form1 : Form
    {
        enum ACTION { RECONNECT, KILL_INTERNET };

        string realPublicIp = "";
        string currentPublicIp = "";
        string HMALocation = "";
        int reconnectFailed = -1;
        bool inetDown = false;

        public Form1()
        {
            InitializeComponent();
            combAction.SelectedIndex = 0;
            tabControl1.SelectedIndex = 1;
            HideCaret();
        }

        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);
        public void HideCaret()
        {
            HideCaret(txtLog.Handle);
        }

        private void GetPublicIP()
        {
            // using:
            // http://ipinfo.io/ip
            // http://checkip.dyndns.org/
            // http://icanhazip.com/
            // https://api.ipify.org/
            // https://wtfismyip.com/text
            //WebRequest request = HttpWebRequest.Create("https://api.ipify.org/");
            //HttpRequestCachePolicy noCachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            //request.Method = "GET";            
            //request.CachePolicy = noCachePolicy;
            //using (WebResponse response = request.GetResponse())
            //{
            //    if (response.IsFromCache) MessageBox.Show("CACHED!");
            //    using (var reader = new StreamReader(response.GetResponseStream()))
            //    {
            //        return reader.ReadToEnd().Replace("\n", "");
            //    }
            //}

            try
            {
	            string externalip = new WebClient().DownloadString("https://ipv4.icanhazip.com/");
	            currentPublicIp = externalip.TrimEnd();
            }
            catch (System.Exception ex)
            {
                //MessageBox.Show("Check your internet connection and try again", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentPublicIp = "ERROR";
            }
        }

        private void ReconnectVPN()
        {
            do
            {
                reconnectFailed++;
                if (reconnectFailed == Convert.ToInt32(txtFailedReconAttempts.Text))
                {
                    KillInternet();
                    break;
                }

                // Send connect cmd to vpn
                Process proc = new Process();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.FileName = "cmd.exe";
                proc.StartInfo.Arguments = String.Format("/c \"{0}\" -connect", HMALocation);
                proc.StartInfo.RedirectStandardError = false;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.Start();
                //proc.WaitForExit();
                Thread.Sleep(Convert.ToInt32(txtReconnectInterval.Text));

                GetPublicIP();
            } while (currentPublicIp == realPublicIp);
            reconnectFailed = -1;
        }

        private async void KillInternet()
        {
            inetDown = true;
            await Task.Run(() => InternetConnection("Disable"));
            btnStop_Click(null, null);
        }

        private async void RestoreInternet()
        {
            inetDown = false;
            await Task.Run(() => InternetConnection("Enable"));
        }

        private void InternetConnection(string action)
        {
            Process proc = new Process();

            switch (action.ToUpper())
            {
                case "DISABLE":
                    // disconnect every network adapter                    
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    proc.StartInfo.FileName = "cmd.exe";
                    proc.StartInfo.Arguments = "/c ipconfig /release";
                    proc.StartInfo.RedirectStandardError = false;
                    proc.StartInfo.RedirectStandardOutput = false;
                    proc.Start();
                    //proc.WaitForExit();
                    break;

                case "ENABLE":
                    // reconnect every network adapter
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    proc.StartInfo.FileName = "cmd.exe";
                    proc.StartInfo.Arguments = "/c ipconfig /renew";
                    proc.StartInfo.RedirectStandardError = false;
                    proc.StartInfo.RedirectStandardOutput = false;
                    proc.Start();
                    //proc.WaitForExit();
                    break;

                default:
                    break;
            }

            //try
            //{
            // string strWQuery = "SELECT DeviceID, ProductName FROM Win32_NetworkAdapter WHERE Manufacturer <> 'Microsoft' ";
            // ObjectQuery oQuery = new System.Management.ObjectQuery(strWQuery);
            // ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(oQuery);
            //    ManagementObjectCollection oReturnCollection = oSearcher.Get();

            //    foreach (ManagementObject mo in oReturnCollection)
            //        mo.InvokeMethod(action, null);
            //}
            //catch (System.Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }

        private async void btnGetPublicIP_Click(object sender, EventArgs e)
        {
            txtPublicIP.Text = realPublicIp = "";

            await Task.Run(() => GetPublicIP());
            if (currentPublicIp == "ERROR")
            {
                MessageBox.Show("Check your internet connection and try again", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            realPublicIp = currentPublicIp;
            if (realPublicIp.Length == 0)
                realPublicIp = txtPublicIP.Text;
            else
                txtPublicIP.Text = realPublicIp;

            //IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            //string localIP = "?";
            //foreach (IPAddress ip in host.AddressList)
            //{
            //    if (ip.AddressFamily.ToString() == "InterNetwork")
            //    {
            //        localIP = ip.ToString();
            //        if (ip.ToString() == textBox1.Text)  //Returns true 
            //                                             //if the VPN Address typed matches the IP from the system
            //        {
            //            bool vpnON = true;
            //            //timer1.Start(); //Restart the timer since the check was good
            //        }
            //    }
            //}
        }

        private void combAction_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if(String.IsNullOrEmpty(txtPublicIP.Text))
            {
                MessageBox.Show("Before starting first enter the real public IP address", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            if(String.IsNullOrEmpty(txtHMALocation.Text))
            {
                MessageBox.Show("Before starting first enter the location of your HMA executable", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            timerConnection.Interval = Convert.ToInt32(txtTimer.Text);
            statProcess.Text = "Active";
            timerConnection.Enabled = true;
            tabControl1.SelectedIndex = 0;

            // set the log info
            txtLog.AppendText(String.Format("\n[{0}] VPN Guard Protection Active\n", DateTime.Now.ToString("HH:mm:ss")));
            txtLog.AppendText(String.Format("Real Public IP: {0}\n", realPublicIp));
            txtLog.AppendText(String.Format("IP check interval: {0}\n", timerConnection.Interval));
            txtLog.AppendText(String.Format("Action on VPN disconnection: {0}\n", combAction.Text));
            txtLog.AppendText(String.Format("HMA executable location: {0}\n\n", txtHMALocation.Text));
            HighlightLine(txtLog, "VPN Guard Protection Active", Color.White, Color.DarkSlateBlue);

            // show tray info if visible
            if(trayIcon.Visible)
            {
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.BalloonTipText = "VPN Guard Protection is now Active";
                trayIcon.BalloonTipTitle = "VPN Guard";
                trayIcon.ShowBalloonTip(3000);
            }

            trayEnableGuard.Enabled = false;
            trayDisableGuard.Enabled = true;
            btnStart.Enabled = false;
            btnStop.Enabled = true;
        }

        private async void timerConnection_Tick(object sender, EventArgs e)
        {
            timerConnection.Enabled = false;

            await Task.Run(() => GetPublicIP());
            if (currentPublicIp == "ERROR")
            {
                // set log info
                txtLog.AppendText(String.Format("[{0}] Connection Error\n", DateTime.Now.ToString("HH:mm:ss")));
                txtLog.AppendText(String.Format("Check your internet connection...\n"));
                HighlightLine(txtLog, "Connection Error", Color.White, Color.DarkOrange);

                // show tray info if visible
                if (trayIcon.Visible)
                {
                    trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                    trayIcon.BalloonTipText = "Connection Error. Check your internet connection...";
                    trayIcon.BalloonTipTitle = "VPN Guard";
                    trayIcon.ShowBalloonTip(3000);
                }

                timerConnection.Enabled = true;
                return;
            }

            if (currentPublicIp == realPublicIp)
            {
                switch (combAction.SelectedIndex)
                {
                    case (int)ACTION.RECONNECT:
                        // set the log info
                        txtLog.AppendText(String.Format("[{0}] VPN Disconnected\n", DateTime.Now.ToString("HH:mm:ss")));
                        txtLog.AppendText(String.Format("Attempting to reconnect...\n"));
                        HighlightLine(txtLog, "VPN Disconnected", Color.White, Color.Brown);

                        // show tray info if visible
                        if (trayIcon.Visible)
                        {
                            trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                            trayIcon.BalloonTipText = "VPN Disconnected. Attempting to reconnect...";
                            trayIcon.BalloonTipTitle = "VPN Guard";
                            trayIcon.ShowBalloonTip(3000);
                        }

                        await Task.Run(() => ReconnectVPN());

                        if(!inetDown)
                        {
                            // set the log info
                            txtLog.AppendText(String.Format("[{0}] VPN Connected\n", DateTime.Now.ToString("HH:mm:ss")));
                            txtLog.AppendText(String.Format("Successful Reconnection...\n"));
                            HighlightLine(txtLog, "VPN Connected", Color.White, Color.DarkSlateGray);
                            // show tray info if visible
                            if (trayIcon.Visible)
                            {
                                trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                                trayIcon.BalloonTipText = "VPN Connected. Successful Reconnection...";
                                trayIcon.BalloonTipTitle = "VPN Guard";
                                trayIcon.ShowBalloonTip(3000);
                            }
                        }
                        else
                        {
                            // set the log info
                            txtLog.AppendText(String.Format("[{0}] VPN Disconnected\n", DateTime.Now.ToString("HH:mm:ss")));
                            txtLog.AppendText(String.Format("Unsuccessful Reconnection...\n"));
                            HighlightLine(txtLog, "VPN Disconnected", Color.White, Color.DarkOrange);

                            // show tray info if visible
                            if (trayIcon.Visible)
                            {
                                trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                                trayIcon.BalloonTipText = "VPN Disconnected. Unsuccessful Reconnection...";
                                trayIcon.BalloonTipTitle = "VPN Guard";
                                trayIcon.ShowBalloonTip(3000);
                            }
                        }
                        break;

                    case (int)ACTION.KILL_INTERNET:
                        // set the log info
                        txtLog.AppendText(String.Format("[{0}] VPN Disconnected\n", DateTime.Now.ToString("HH:mm:ss")));
                        txtLog.AppendText(String.Format("Attempting to disable all the adapters...\n"));
                        HighlightLine(txtLog, "VPN Disconnected", Color.White, Color.Brown);

                        // show tray info if visible
                        if (trayIcon.Visible)
                        {
                            trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                            trayIcon.BalloonTipText = "VPN Disconnected. Attempting to disable all the adapters...";
                            trayIcon.BalloonTipTitle = "VPN Guard";
                            trayIcon.ShowBalloonTip(3000);
                        }

                        await Task.Run(() => KillInternet());
                        break;
                    default:
                        break;
                }
            }

            if (!inetDown)
                timerConnection.Enabled = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            statProcess.Text = "Disabled";
            timerConnection.Enabled = false;

            // set log info
            txtLog.AppendText(String.Format("[{0}] VPN Guard Protection Disabled\n", DateTime.Now.ToString("HH:mm:ss")));
            HighlightLine(txtLog, "VPN Guard Protection Disabled", Color.White, Color.Brown);

            // show tray info if visible
            if (trayIcon.Visible)
            {
                trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                trayIcon.BalloonTipText = "VPN Guard Protection is now Disabled";
                trayIcon.BalloonTipTitle = "VPN Guard";
                trayIcon.ShowBalloonTip(3000);
            }

            trayEnableGuard.Enabled = true;
            trayDisableGuard.Enabled = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
        }

        private void txtHMALocation_Click(object sender, EventArgs e)
        {
            if (dlgOpenFiles.ShowDialog() == DialogResult.OK)
            {
                txtHMALocation.Text = dlgOpenFiles.FileName;
                HMALocation = dlgOpenFiles.FileName;
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://hidemyass.avcdn.net/tools/privax/latest/HMA-Pro-VPN-2.8.24.0-installer.exe", null);
        }

        public static void HighlightLine(RichTextBox rich, string text, Color fontColor, Color bgColor)
        {
            for (int line = 0; line < rich.Lines.Length; line++)
            {
                int c0 = rich.GetFirstCharIndexFromLine(line);
                int c1 = rich.GetFirstCharIndexFromLine(line + 1);
                if (c1 < 0)
                    c1 = rich.Text.Length;

                rich.SelectionStart = c0;
                rich.SelectionLength = c1 - c0;
                if (rich.SelectedText.Contains(text))
                {
                    rich.SelectionBackColor = bgColor;
                    rich.SelectionColor = fontColor;
                    rich.SelectionFont = new Font(rich.Font, FontStyle.Bold);
                }
            }
        }

        private void btnReactivateInet_Click(object sender, EventArgs e)
        {
            // set the log info
            txtLog.AppendText(String.Format("[{0}] VPN Reactivation\n", DateTime.Now.ToString("HH:mm:ss")));
            txtLog.AppendText(String.Format("Attempting to re-enable all the adapters...\n"));
            HighlightLine(txtLog, "VPN Reactivation", Color.White, Color.DarkSlateGray);

            // show tray info if visible
            if (trayIcon.Visible)
            {
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.BalloonTipText = "VPN Reactivation. Attempting to re-enable all the adapters...";
                trayIcon.BalloonTipTitle = "VPN Guard";
                trayIcon.ShowBalloonTip(3000);
            }

            RestoreInternet();
        }

        private void txtLog_MouseDown(object sender, MouseEventArgs e)
        {
            HideCaret();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                trayIcon.Visible = true;
            }
        }

        private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
        }

        private void trayShowWindow_Click(object sender, EventArgs e)
        {
            trayIcon_MouseDoubleClick(sender, null);
        }

        private void trayEnableGuard_Click(object sender, EventArgs e)
        {
            btnStart_Click(sender, null);
        }

        private void trayDisableGuard_Click(object sender, EventArgs e)
        {
            btnStop_Click(sender, null);
        }

        private void trayExit_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("Are you sure you want to exit VPN Guard?", "Close VPN Guard", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                Application.Exit();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit VPN Guard?", "Close VPN Guard", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }
}
