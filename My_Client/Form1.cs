using System;
using System.Windows.Forms;
using System.Net;
using Communication;
using Communication.TCPIP;
namespace My_Client
{
    public partial class Form1 : Form
    {
        ClientManager client;
        public Form1()
        {
            InitializeComponent();
            client = new ClientManager(2048);
        }
        private void button1_Click(object sender, EventArgs e) 
        {
            if (!this.client.Connected)
            {
                client.Connect(IPAddress.Parse(textBox1.Text), int.Parse(textBox2.Text),2000);
            }
            else
            {
                client.Disconnect();
            }
        }
        private void writeRichTextbox(string data)  
        {
            if (this.InvokeRequired)
            {

                this.Invoke((MethodInvoker)delegate 
                {
                    richTextBox1.AppendText(data + "\r\n");
                    richTextBox1.ScrollToCaret();
                });
            }
            else
            {
                richTextBox1.AppendText(data + "\r\n");
                richTextBox1.ScrollToCaret();
            }
        }
        private void button2_Click(object sender, EventArgs e) 
        {
            string sendData1 = textBox3.Text;
            client.DataSend(DataConverter.StringToByteArray(sendData1));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            client.ConnectedEvent += connectEventFunc;
            client.DataReceiveEvent += DataReceiveEventFunc;
            client.DataSendSuccessEvent += SendData;
            client.DisconnectedEvent += DisconnectEventFunc;
            client.ConnectFailEvent += connectFailEventFunc;
        }

        private void connectEventFunc(IPAddress IP, int Port)
        {
            writeRichTextbox(IP.ToString() + ":"+ Port.ToString() + " - 서버 연결됨...");
        }
        private void connectFailEventFunc(IPAddress IP, int Port)
        {
            writeRichTextbox(IP.ToString() + ":" + Port.ToString() + " - 서버 연결 실패.");
        }
        private void DataReceiveEventFunc(byte[] data)
        {
            writeRichTextbox("Receive : " + DataConverter.ByteArrayToString(data));
        }
        private void DisconnectEventFunc(IPAddress IP, int Port)
        {
            writeRichTextbox(IP.ToString() + ":" + Port.ToString() + " - 서버 연결 끊김...");
        }

        public void SendData()
        {
            writeRichTextbox("Send : " + textBox3.Text);
        }
    }
}
