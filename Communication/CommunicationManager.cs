using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
namespace Communication
{
    public static class DataConverter
    {
        public static string ByteArrayToString(byte[] data)
        {
            return Encoding.Default.GetString(data);
        }

        public static char[] ByteArrayToCharArray(byte[] data)
        {
            return Encoding.Default.GetChars(data);
        }

        public static byte[] StringToByteArray(string data)
        {
            return Encoding.Default.GetBytes(data);
        }
        public static byte[] CharArrayToByteArray(char[] data)
        {
            return Encoding.Default.GetBytes(data);
        }
    }
    namespace TCPIP
    {
        public class ServerManager
        {
            #region Variable
            private Socket _server;
            private IPAddress IP;
            private int port;
            private List<IPAddress> ClientsIPList;
            private Dictionary<IPAddress, ClientHandler> Clients;
            private bool ServerOpenState = false;
            private int datasize;
            #endregion

            #region Property
            public IPAddress ServerIP
            {
                get { return this.IP; }
            }
            public int ServerPort
            {
                get { return this.port; }
            }

            public List<IPAddress> ClientsIP
            {
                get { return this.ClientsIPList; }
            }
            public bool ServerOpened
            {
                get { return this.ServerOpenState; }
            }
            #endregion

            #region Constructor
            public ServerManager(IPAddress IP_Addr, int Port, int datasize)
            {
                this.IP = IP_Addr;
                this.port = Port;
                this.datasize = datasize;
                this.Clients = new Dictionary<IPAddress, ClientHandler>();
                this.ClientsIPList = new List<IPAddress>();
            }
            #endregion


            #region Event
            public delegate void ClientAccessEventHandler(IPAddress ClientIP);
            public event ClientAccessEventHandler ClientConnectedEvent;


            public delegate void DataReceiveEventHandler(IPAddress ClientIP, byte[] ReceiveData);
            public event DataReceiveEventHandler DataReceivedEvent;


            public delegate void ServerOpenEventHandler();
            public event ServerOpenEventHandler ServerOpenedEvent;

            public delegate void ServerOpenFailEventHandler();
            public event ServerOpenFailEventHandler ServerOpenFailEvent;

            public delegate void ServerCloseEventHandler();
            public event ServerCloseEventHandler ServerCloseedEvent;

            public delegate void DataSendEventHandler(IPAddress ClientIP);
            public event DataSendEventHandler DataSendSuccessEvent;

            public delegate void DataSendFailEventHandler(IPAddress ClientIP);
            public event DataSendFailEventHandler DataSendFailEvent;

            public delegate void ClientDisconnectEventHandler(IPAddress DisconnectIP);
            public event ClientDisconnectEventHandler ClientDisconnectedEvent;
            #endregion

            #region Method
            public void ServerOpen()
            {
                try
                {
                    if (ServerOpenState == true)
                    {
                        MessageBox.Show("Server is already opened.");
                        return;
                    }
                    IPEndPoint server_ep = new IPEndPoint(this.IP, this.port);
                    this._server = new Socket(server_ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    this._server.Blocking = true;
                    this._server.Bind(server_ep);
                    this._server.Listen((int)SocketOptionName.MaxConnections);
                    this._server.BeginAccept(new AsyncCallback(BeginAcceptTcpAsync), this._server);
                    this.ServerOpenState = true;
                    ServerOpenedEvent?.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    this.stop();
                    ServerOpenFailEvent?.Invoke();
                }
            }

            public void ServerClose()
            {
                if (ServerOpenState == false) { return; }
                foreach (var client in Clients.Values)
                {
                    client.RemoveClient();
                }
                this.Clients.Clear();
                this.ClientsIPList.Clear();
                this.stop();
                ServerCloseedEvent?.Invoke();
            }
            public void DataSend(IPAddress IP, byte[] data)
            {
                try
                {
                    if (!Clients.ContainsKey(IP)) { return; }
                    if (!Clients[IP].client.Connected) { return; }
                    Clients[IP].client.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(DataSendAsync), Clients[IP]);
                }
                catch (Exception e)
                {
                    DataSendFailEvent?.Invoke(IP);
                    Console.WriteLine(e.ToString());
                }
            }
            private void stop()
            {
                if (_server != null)
                {
                    _server.Close();
                    _server.Dispose();
                    _server = null;
                }
                this.ServerOpenState = false;
            }

            public void ClientRemove(IPAddress IP)
            {
                if (Clients.Count < 1 || ClientsIPList.Count < 1) { return; }
                ClientDisconnectedEvent?.Invoke(IP);
                Clients[IP].RemoveClient();
                Clients.Remove(IP);
                ClientsIPList.Remove(IP);
            }

            private void BeginAcceptTcpAsync(IAsyncResult ar)
            {
                try
                {
                    Socket sv = ar.AsyncState as Socket;
                    if (sv == null)
                    {
                        return;
                    }
                    Socket acceptClient = sv.EndAccept(ar);
                    this._server.BeginAccept(new AsyncCallback(BeginAcceptTcpAsync), sv);
                    int size = sizeof(UInt32);
                    UInt32 on = 1;
                    UInt32 keepAliveInterval = 1000;   // Send a packet once every 1 seconds.
                    UInt32 retryInterval = 200;        // If no response, resend every second.
                    byte[] inArray = new byte[size * 3];
                    Array.Copy(BitConverter.GetBytes(on), 0, inArray, 0, size);
                    Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, inArray, size, size);
                    Array.Copy(BitConverter.GetBytes(retryInterval), 0, inArray, size * 2, size);
                    acceptClient.IOControl(IOControlCode.KeepAliveValues, inArray, null);
                    ClientHandler clientHandle = new ClientHandler(acceptClient, this.datasize);
                    clientHandle.client.BeginReceive(clientHandle.ReceivedData, 0, clientHandle.ReceivedData.Length, SocketFlags.None, new AsyncCallback(DataReceiveAsync), clientHandle);
                    Clients.Add(clientHandle.IP_Ad, clientHandle);
                    ClientsIPList.Add(clientHandle.IP_Ad);
                    ClientConnectedEvent?.Invoke(clientHandle.IP_Ad);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            private void DataReceiveAsync(IAsyncResult ar)
            {

                ClientHandler callbackClient = ar.AsyncState as ClientHandler;
                if (callbackClient.client == null || !callbackClient.client.Connected)
                {
                    ClientRemove(callbackClient.IP_Ad);
                    return;
                }
                try
                {
                    int byteRead = callbackClient.client.EndReceive(ar);
                    if (byteRead == 0)
                    {
                        ClientRemove(callbackClient.IP_Ad);
                        return;
                    }
                    byte[] OnlyData = new byte[byteRead];
                    Array.Copy(callbackClient.ReceivedData, OnlyData, byteRead);
                    Array.Clear(callbackClient.ReceivedData, 0, callbackClient.ReceivedData.Length);
                    callbackClient.client.BeginReceive(callbackClient.ReceivedData, 0, callbackClient.ReceivedData.Length, SocketFlags.None, new AsyncCallback(DataReceiveAsync), callbackClient);
                    DataReceivedEvent?.Invoke(callbackClient.IP_Ad, OnlyData);
                }
                catch (Exception ex)
                {
                    ClientRemove(callbackClient.IP_Ad);
                    Console.WriteLine(ex.ToString());
                }

            }
            private void DataSendAsync(IAsyncResult ar)
            {
                ClientHandler callbackClient = ar.AsyncState as ClientHandler;
                try
                {
                    callbackClient.client.EndSend(ar);
                    DataSendSuccessEvent?.Invoke(callbackClient.IP_Ad);
                }
                catch (Exception ex)
                {
                    DataSendFailEvent?.Invoke(callbackClient.IP_Ad);
                    Console.WriteLine(ex.ToString());
                }
            }
            #endregion


            #region Nested Class - ClientHandler
            private class ClientHandler
            {
                #region Variable
                #endregion

                #region Constructor
                internal ClientHandler(Socket client, int dataSize)
                {
                    this.client = client;
                    this.ReceivedData = new byte[dataSize];

                    string clientEndPoint = client.LocalEndPoint.ToString();
                    char[] point = { '.', ':' };
                    IP_Ad = IPAddress.Parse(clientEndPoint.Split(point[1])[0]);
                }
                #endregion

                #region Property
                internal Socket client { get; set; }
                internal byte[] ReceivedData { get; set; }
                internal IPAddress IP_Ad { get; set; }
                #endregion

                #region Event
                #endregion

                #region Method
                internal void RemoveClient()
                {
                    if (client == null)
                    {
                        return;
                    }
                    if (this.client.Connected)
                    {
                        this.client.Shutdown(SocketShutdown.Both);
                    }
                    this.client.Close();
                    this.client.Dispose();
                    this.client = null;
                }
                #endregion
            }
            #endregion
        }

        public class ClientManager
        {
            #region Variable
            private Socket client;
            private bool connecting;
            private bool connected;
            #endregion

            #region Property
            public IPAddress IP_ad { get; set; }
            public int Port { get; set; }

            public bool Connected
            {
                get
                {
                    if(this.client == null)
                    {
                        return false;
                    }
                    return this.client.Connected;
                }
            }
            private byte[] ReceivedData { get; set; }
            #endregion

            #region Constructor
            public ClientManager(int datasize)
            {
                ReceivedData = new byte[datasize];
                connecting = false;
                connected = false;
            }
            #endregion


            #region Event
            public delegate void ConnectEventHandler(IPAddress IP, int Port);
            public event ConnectEventHandler ConnectedEvent;

            public delegate void ConnectFailEventHandler(IPAddress IP, int Port);
            public event ConnectFailEventHandler ConnectFailEvent;

            public delegate void DataReceiveEventHandler(byte[] ReceiveData);
            public event DataReceiveEventHandler DataReceiveEvent;


            public delegate void DataSendEventHandler();
            public event DataSendEventHandler DataSendSuccessEvent;

            public delegate void DataSendFailEventHandler();
            public event DataSendFailEventHandler DataSendFailEvent;

            public delegate void DisconnectEventHandler(IPAddress IP, int port);
            public event DisconnectEventHandler DisconnectedEvent;
            #endregion

            #region Method
            /// <summary>
            /// Connect Function
            /// </summary>
            /// <param name="IP">IP</param>
            /// <param name="Port">Port</param>
            public void Connect(IPAddress IP, int Port)
            {

                if (this.connected) { return; }
                if (this.client != null && (this.client.Connected || connecting)) { return; }
                try
                {
                    this.connecting = true;
                    this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this.client.BeginConnect(IP, Port, new AsyncCallback(OnConnected), this.client);
                    this.IP_ad = IP;
                    this.Port = Port;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    _Disconnect();
                    ConnectFailEventFunc();
                    connecting = false;
                }
            }

            /// <summary>
            /// Connect Function
            /// </summary>
            /// <param name="IP">IP</param>
            /// <param name="Port">Port</param>
            /// <param name="TimeOut">TimeOut(ms)</param>
            public void Connect(IPAddress IP, int Port,int TimeOut)
            {

                if (this.connected) { return; }
                if (this.client != null && (this.client.Connected || connecting)) { return; }
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        this.connecting = true;
                        this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        this.IP_ad = IP;
                        this.Port = Port;
                        if (!this.client.BeginConnect(IP, Port, new AsyncCallback(OnConnected), this.client).AsyncWaitHandle.WaitOne(TimeOut, true))
                        {
                            this.client.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        _Disconnect();
                        ConnectFailEventFunc();
                        connecting = false;
                    }
                });
            }
            public void Disconnect()
            {
                _Disconnect();
                DisconnectEventFunc();
            }

            public void DataSend(byte[] data)
            {
                try
                {
                    if (client == null || !client.Connected) { return; }
                    this.client.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(DataSendAsync), this.client);
                }
                catch (Exception ex)
                {
                    DataSendFailEvent?.Invoke();
                    Console.WriteLine(ex.ToString());
                }
            }
            private void _Disconnect()
            {
                this.connecting = false;
                if (this.client == null) { goto initializeclient; }
                if (this.client.Connected)
                {
                    this.client.Shutdown(SocketShutdown.Both);
                }
                this.client.Close();
                this.client.Dispose();
                initializeclient:
                this.connected = false;
            }
            private void DisconnectEventFunc()
            {
                var ip = this.IP_ad;
                var port = this.Port;
                this.client = null;
                this.IP_ad = null;
                this.Port = -1;
                Array.Clear(ReceivedData, 0, ReceivedData.Length);
                this.DisconnectedEvent?.Invoke(ip, port);
            }
            private void ConnectFailEventFunc()
            {
                var ip = this.IP_ad;
                var port = this.Port;
                this.client = null;
                this.IP_ad = null;
                this.Port = -1;
                Array.Clear(ReceivedData, 0, ReceivedData.Length);
                ConnectFailEvent?.Invoke(ip, port);
            }
            private void OnConnected(IAsyncResult ar)
            {
                try
                {
                    Socket client = ar.AsyncState as Socket;
                    if (client != null && client.Connected)
                    {
                        client.EndConnect(ar);
                        int size = sizeof(UInt32);
                        UInt32 on = 1;
                        UInt32 keepAliveInterval = 1000;   // Send a packet once every 1 seconds.
                        UInt32 retryInterval = 200;        // If no response, resend every second.
                        byte[] inArray = new byte[size * 3];
                        Array.Copy(BitConverter.GetBytes(on), 0, inArray, 0, size);
                        Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, inArray, size, size);
                        Array.Copy(BitConverter.GetBytes(retryInterval), 0, inArray, size * 2, size);
                        client.IOControl(IOControlCode.KeepAliveValues, inArray, null);
                        client.BeginReceive(ReceivedData, 0, ReceivedData.Length, SocketFlags.None, new AsyncCallback(DataReceiveAsync), this.client);
                        this.connected = true;
                        ConnectedEvent?.Invoke(this.IP_ad, this.Port);
                    }
                    else
                    {
                        _Disconnect();
                        ConnectFailEventFunc();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    _Disconnect();
                    ConnectFailEventFunc();
                }
                finally
                {
                    connecting = false;
                }
            }

            private void DataReceiveAsync(IAsyncResult ar)
            {
                try
                {
                    Socket cl = ar.AsyncState as Socket;
                    if (cl == null || !cl.Connected) { return; }
                    int byteRead = cl.EndReceive(ar);
                    if (byteRead == 0)
                    {
                        _Disconnect();
                        DisconnectEventFunc();
                        return;
                    }
                    byte[] OnlyData = new byte[byteRead];
                    Array.Copy(ReceivedData, OnlyData, byteRead);
                    Array.Clear(ReceivedData, 0, ReceivedData.Length);
                    cl.BeginReceive(ReceivedData, 0, ReceivedData.Length, SocketFlags.None, new AsyncCallback(DataReceiveAsync), cl);
                    DataReceiveEvent?.Invoke(OnlyData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    _Disconnect();
                    DisconnectEventFunc();
                }
            }

            private void DataSendAsync(IAsyncResult ar)
            {
                try
                {
                    Socket client = ar.AsyncState as Socket;
                    client.EndSend(ar);
                    DataSendSuccessEvent?.Invoke();
                }
                catch (Exception ex)
                {
                    DataSendFailEvent?.Invoke();
                    Console.WriteLine(ex.ToString());
                }
            }
            #endregion
        }
    }
}
