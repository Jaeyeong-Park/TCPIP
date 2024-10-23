using System;
using System.Windows.Forms;
using System.Net;
using Communication;
using Communication.TCPIP;
namespace My_Server
{
    public partial class Form1 : Form
    {
        ServerManager server;
        public Form1()
        {
            InitializeComponent();
        } 

        private void button1_Click(object sender, EventArgs e)
        {
            if (server == null || !server.ServerOpened)
            {
                server = new ServerManager(IPAddress.Parse(textBox1.Text), int.Parse(textBox2.Text),2048);
                server.ServerOpenedEvent += ServerOpenEvent;
                server.ClientConnectedEvent += ClientAccessEvent;
                server.DataReceivedEvent += ReceiveEvent;
                server.ClientDisconnectedEvent += DisConnectClient;
                server.DataSendSuccessEvent += SendData;
                server.ServerOpenFailEvent += ServerOpenFailEvent;
                server.ServerCloseedEvent += ServerClose;
                server.ServerOpen();
            }
            else
            {
                server.ServerClose();
                server = null;
            }   
        }

        private void writeRichTextbox(string str)  
        {
            if (this.richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke((MethodInvoker)delegate { richTextBox1.AppendText(str + "\r\n"); }); 
                richTextBox1.Invoke((MethodInvoker)delegate { richTextBox1.ScrollToCaret(); }); 
            }
            else
            {
                richTextBox1.AppendText(str + "\r\n");
                richTextBox1.ScrollToCaret();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string sendData1 = textBox3.Text;
            server.DataSend(IPAddress.Parse(textBox1.Text), DataConverter.StringToByteArray(sendData1));
        }
        private void ServerOpenEvent()
        {
            writeRichTextbox("서버 준비 - 클라이언트 기다리는 중...");
        }

        private void ServerOpenFailEvent()
        {
            writeRichTextbox("서버 오픈 실패.");
        }
        private void ClientAccessEvent(IPAddress IP)
        {
            writeRichTextbox(IP.ToString() + " - 클라이언트 접속 완료.");
        }
        private void ReceiveEvent(IPAddress ClientIP, byte[] data)
        {
            string data_s = DataConverter.ByteArrayToString(data);
            writeRichTextbox($"Receive from {ClientIP} : " + data_s);
        }

        private void DisConnectClient(IPAddress IP)
        {
            writeRichTextbox(IP.ToString() + " - 클라이언트 접속 종료.");
        }
        private void SendData(IPAddress IP)
        {
            writeRichTextbox($"Send To {IP} : {textBox3.Text}");
        }
        private void ServerClose()
        {
            writeRichTextbox("서버 닫힘.");
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(server != null && server.ServerOpened)
            {
                server.ServerClose();
            }
        }
        
    }
}
