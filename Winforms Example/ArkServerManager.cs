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
using System.IO;

namespace Ark_Server_Manager
{
    public partial class ArkServerManager : Form
    {
        public ArkServerManager()
        {
            InitializeComponent();
            Load += Form1_Load;
        }

        void Form1_Load(object sender, EventArgs e)
        {
            Program.ArkRcon.Run();
            Program.ArkRcon.ServerConnectionStarting += ArkRcon_ServerConnectionStarting;
            Program.ArkRcon.ServerConnectionFailed += ArkRcon_ServerConnectionFailed;
            Program.ArkRcon.ServerAuthSucceeded += ArkRcon_ServerAuthSucceeded;
            Program.ArkRcon.ServerAuthFailed += ArkRcon_ServerAuthFailed;
            Program.ArkRcon.ConsoleLogUpdated += ArkRcon_ConsoleLogUpdated;

            IPAddress[] localIP = Dns.GetHostAddresses(Dns.GetHostName()); //Local IP Info
            foreach (IPAddress address in localIP)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    label7.Text = address.ToString();
                }
            }
        }

        void ArkRcon_ConsoleLogUpdated(object sender, Ark.ConsoleLogEventArgs e)
        {
            listBox1.Items.Add(string.Format("{0} - {1}", e.Timestamp, e.Message));
        }

        void ArkRcon_ServerAuthFailed(object sender, Ark.ServerAuthEventArgs e)
        {
            listBox1.Items.Add(string.Format("{0} - {1}", e.Timestamp, e.Message));
        }

        void ArkRcon_ServerAuthSucceeded(object sender, Ark.ServerAuthEventArgs e)
        {
            listBox1.Items.Add(string.Format("{0} - {1}", e.Timestamp, e.Message));
        }

        void ArkRcon_ServerConnectionFailed(object sender, Ark.ServerConnectionEventArgs e)
        {
            listBox1.Items.Add(string.Format("{0} - {1}", e.Timestamp, e.Message));
        }

        void ArkRcon_ServerConnectionStarting(object sender, Ark.ServerConnectionEventArgs e)
        {
            listBox1.Items.Add(string.Format("{0} - {1}", e.Timestamp, e.Message));
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var server = new Server
            {
                Hostname = textBox1.Text,
                Port = int.Parse(textBox2.Text),
                Password = textBox3.Text
            };
            await Program.ArkRcon.Connect(server);       
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            
            if (!Program.ArkRcon.IsConnected)
            {
                listBox1.Items.Add(string.Format("{0} - {1}", DateTime.Now, "You are not connected to a server!"));
                return;
            }
            Program.ArkRcon.ExecCommand(Ark.Opcode.Generic, textBox4.Text);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        private void CheckStatus_Tick(object sender, EventArgs e)
        {
            if (Program.ArkRcon.IsConnected)
            {
                this.serverstatus.Image = Properties.Resources.online;
            }
            else
            {
                this.serverstatus.Image = Properties.Resources.offline;
            }
        }
    }
}
