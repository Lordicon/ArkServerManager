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

        String[,] rssData = null;

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

            TitlesBox.Items.Clear();
            rssData = getRssData(ChannelTextBox.Text);
            for (int i = 0; i < rssData.GetLength(0); i++)
            {
                if (rssData[i, 0] != null)
                {
                    TitlesBox.Items.Add(rssData[i, 0]);
                }
                TitlesBox.SelectedIndex = 0;
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

        private String[,] getRssData(String channel)
        {
            System.Net.WebRequest myRequest = System.Net.WebRequest.Create(channel);
            System.Net.WebResponse myResponse = myRequest.GetResponse();

            System.IO.Stream rssStream = myResponse.GetResponseStream();
            System.Xml.XmlDocument rssDoc = new System.Xml.XmlDocument();

            rssDoc.Load(rssStream);

            System.Xml.XmlNodeList rssItems = rssDoc.SelectNodes("rss/channel/item");

            String[,] tempRssData = new String[100, 3];

            for (int i = 0; i < rssItems.Count; i++)
            {
                System.Xml.XmlNode rssNode;

                rssNode = rssItems.Item(i).SelectSingleNode("title");
                if (rssNode != null)
                {
                    tempRssData[i, 0] = rssNode.InnerText;
                }
                else 
                {
                    tempRssData[i, 0] = "";
                }

                rssNode = rssItems.Item(i).SelectSingleNode("description");
                if (rssNode != null)
                {
                    tempRssData[i, 1] = rssNode.InnerText;
                }
                else
                {
                    tempRssData[i, 1] = "";
                }

                rssNode = rssItems.Item(i).SelectSingleNode("link");
                if (rssNode != null)
                {
                    tempRssData[i, 2] = rssNode.InnerText;
                }
                else
                {
                    tempRssData[i, 2] = "";
                }
            }
            return tempRssData;
        }

        private void TitlesBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (rssData[TitlesBox.SelectedIndex, 1] != null)
                DescriptionBox.Text = rssData[TitlesBox.SelectedIndex, 1];
            if (rssData[TitlesBox.SelectedIndex, 2] != null)
                linkLabel.Text = "GoTo: " + rssData[TitlesBox.SelectedIndex, 0];
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (rssData[TitlesBox.SelectedIndex, 2] != null)
                System.Diagnostics.Process.Start(rssData[TitlesBox.SelectedIndex, 2]);
        }
    }
}