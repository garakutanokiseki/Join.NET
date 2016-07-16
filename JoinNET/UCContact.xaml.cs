using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Xml.Serialization;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using Codeplex.Data;

using System.Net;
using System.Net.Sockets;

using LumiSoft.Net.SIP.Stack;
using LumiSoft.Net.STUN.Client;
using LumiSoft.Net.SDP;

using WrapLibSIP;
using JoinNET.SIP;

namespace JoinNET
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        //STUNサーバー
        private const string m_StunServer = "stun.l.google.com";
        private const int m_StunServerPort = 19302;
        private STUN_Result m_StunResult;
        private string [] m_LocapIP;

        //通信用ソケット
        private UDPFileTransfer m_UDPTransfer;

        //クライアント情報受信スレッド
        DispatcherTimer m_TimerUpdateClient;

        //スレッド管理
        Thread m_ThreadDownloadData;
        bool m_isStopThreadDownloaddata;
        Thread m_ThreadUpdaeClient;
        Thread m_ThreadLoadSharedData;
        bool m_isTopThreadLoadSharedData;

        //SIP通信処理
        System.IntPtr m_SIPInstance = IntPtr.Zero;
        CWrapLibSIP.CallBack m_SIPCallback;
        CDataSIPAccount m_SIPAccount = new CDataSIPAccount();

        //アプリケーション全体の終了フラグ
        bool m_isTerminateApplication = false;

        //保存ディレクトリ名
        const string CLIENT_DATA_FOLDER = "\\clientdata";

        //実行ディレクトリ
        string m_ExcuteDirectory;

        //データタイプ
        enum DataType
        {
            //データ要求
            Request_FileList = 1,
            Request_File,

            //データ送信
            Data_FileList = 1000,
            Data_File,
        }

        public MainWindow()
        {
            InitializeComponent();

            //データコンテキストを設定する
            DataContext = Properties.Settings.Default;
            imageWidth.DataContext = Properties.Settings.Default;

            //タイマーの初期化を行う
            m_TimerUpdateClient = new DispatcherTimer();
            m_TimerUpdateClient.Tick += new EventHandler(TimerUpdateClient_Tick);
            m_TimerUpdateClient.Interval = new TimeSpan(0, 0, 5);

        }

        #region メンバ関数
        void AnimationClientLoading_Run()
        {
            imageLoading.Visibility = System.Windows.Visibility.Visible;
            imageLoading.BeginStoryboard((Storyboard)FindResource("Storyboard_LoadingAnimation"));
        }

        void AnimationClientLoading_Stop()
        {
            imageLoading.Visibility = System.Windows.Visibility.Collapsed;

            //ストーリーボードを停止する
            Storyboard story = (Storyboard)FindResource("Storyboard_LoadingAnimation");
            story.Stop();
        }

        /// <summary>
        /// 指定したItemCollectionにShareDataがあるかを確認する
        /// </summary>
        /// <param name="data"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        bool IsExistShareDataInList(ShareData data, ItemCollection list){
            //データがリストにあるかを確認する
            bool isExist = false;
            foreach (ShareData current in list)
            {
                if (data.guid == current.guid)
                {
                    isExist = true;
                    break;
                }
            }
            return isExist;
        }

        /// <summary>
        /// 実行ディレクトリを取得する(Windowロード時に実行する必要あり)
        /// </summary>
        /// <returns>実行ディレクトリパス</returns>
        private string GetStatupDirectory()
        {
            string exePath = Environment.GetCommandLineArgs()[0];
            string exeFullPath = System.IO.Path.GetFullPath(exePath);
            return System.IO.Path.GetDirectoryName(exeFullPath);
        }
        #endregion

        #region UDP通信
        /// <summary>
        /// ClientDatak構造体から通信先アドレスを取得する
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private ClientCommunicationData GetTargetAddress(ClientData data)
        {
            string ip = "";
            int port = 0;

            ClientCommunicationData returndata = new ClientCommunicationData();

            string public_ip = "";
            int public_port = 0;

            if (m_StunResult.PublicEndPoint != null)
            {
                public_ip = m_StunResult.PublicEndPoint.Address.ToString();
                public_port = m_StunResult.PublicEndPoint.Port;
            }

            if (data.global_ip != null && public_ip != data.global_ip && data.global_port != "")
            {
                ip = data.global_ip;
                port = int.Parse(data.global_port);

                returndata.source_ip = public_ip;
                returndata.source_port = m_UDPTransfer.GetPort();
            }
            else
            {
                for (int i = 0; i < m_LocapIP.Count(); ++i)
                {
                    if (m_LocapIP[i] == null) continue;
                    int pos = m_LocapIP[i].LastIndexOf('.');

                    string local_net_address;
                    string target_net_address;

                    local_net_address = m_LocapIP[i].Substring(0, pos);

                    //Debug.WriteLine("MainWindow::GetTargetAddress local_net_address=" + local_net_address);

                    //1つめのIPを確認する
                    if (data.local_ip != null && data.local_ip != "" && data.local_port != "")
                    {
                        pos = data.local_ip.LastIndexOf('.');
                        target_net_address = data.local_ip.Substring(0, pos);
                        //Debug.WriteLine("MainWindow::GetTargetAddress target_net_address=" + target_net_address);

                        if (local_net_address == target_net_address)
                        {
                            ip = data.local_ip;
                            port = int.Parse(data.local_port);

                            returndata.source_ip = m_LocapIP[i];
                            returndata.source_port = m_UDPTransfer.GetPort();
                        }
                    }
                    if (ip != "") break;

                    //2つめのIPを確認する
                    if (data.local_ip2 != null && data.local_ip2 != "" && data.local_port2 != "")
                    {
                        pos = data.local_ip2.LastIndexOf('.');
                        if (pos >= 0)
                        {
                            target_net_address = data.local_ip2.Substring(0, pos);
                            //Debug.WriteLine("MainWindow::GetTargetAddress target_net_address=" + target_net_address);

                            if (local_net_address == target_net_address)
                            {
                                ip = data.local_ip2;
                                port = int.Parse(data.local_port2);

                                returndata.source_ip = m_LocapIP[i];
                                returndata.source_port = m_UDPTransfer.GetPort();
                            }
                        }
                    }
                    if (ip != "") break;

                    //3つめのIPを確認する
                    if (data.local_ip3 != null && data.local_ip3 != "" && data.local_port3 != "")
                    {
                        pos = data.local_ip3.LastIndexOf('.');
                        if (pos > 0)
                        {
                            target_net_address = data.local_ip3.Substring(0, pos);
                            //Debug.WriteLine("MainWindow::GetTargetAddress target_net_address=" + target_net_address);

                            if (local_net_address == target_net_address)
                            {
                                ip = data.local_ip3;
                                port = int.Parse(data.local_port3);

                                returndata.source_ip = m_LocapIP[i];
                                returndata.source_port = m_UDPTransfer.GetPort();
                            }
                        }
                    }
                    if (ip != "") break;
                }
            }
            
            //接続済みの場合は取得済みのアドレス/ポートを使う
            if (data.node_status == ClientData.NodeStatus.Connected)
            {
                ip = data.commnication_address;
                port = int.Parse(data.comminication_port);
            }

            //送信先のデータを格納する
            returndata.target_ip = ip;
            returndata.target_port = port;

            return returndata;
        }

        private bool EventTransfer(UDPFileTransfer.EventID eventid, Object obj, Object obj2)
        {
            var thread = new Thread(() =>
            {
                Debug.WriteLine("EventTransfer >>");
                switch (eventid)
                {
                    case UDPFileTransfer.EventID.Recived_Ack_ConnectionRequest:
                        OnEventAckConnectionRequest((IPEndPoint)obj2);
                        break;
                    case UDPFileTransfer.EventID.Recived_ConnectionRequest:
                        OnEventRecivedConnectionRequest((string)obj, (IPEndPoint)obj2);
                        break;
#if false
                    case UDPFileTransfer.EventID.Recived_Message:
                        OnEventRecivedMessage((string)obj, (IPEndPoint)obj2);
                        break;
#endif
                    case UDPFileTransfer.EventID.Recived_Binary:
                        OnEventRecivedBinary((byte[])obj, (IPEndPoint)obj2);
                        break;
                    
                    //ファイル受信経過通知
                    case UDPFileTransfer.EventID.Recived_File:
                        OnEventRecivedFile((TransferProgress)obj);
                        break;
                    case UDPFileTransfer.EventID.ReciveFile_Progress:
                        OnEventRecivedFileProgress((TransferProgress)obj);
                        break;
 
                    //エラー通知
                    case UDPFileTransfer.EventID.Error_CantReciveAck:
                        OnEventErrorRecivedAck((int)obj, (uint)obj2);
                        break;
                    case UDPFileTransfer.EventID.Error_socket:
                        OnEventErrorSocket((SocketException)obj);
                        break;
                }
                Debug.WriteLine("EventTransfer <<");
            });
            thread.Start();  // 別スレッドでの処理開始           
            return true;
        }

        private void OnEventAckConnectionRequest(IPEndPoint endpoint){
            Debug.WriteLine("OnEventAckConnectionRequest >>");

            //返信元のアドレス/ポートを得る
            string ipaddress = endpoint.Address.ToString();
            string port = endpoint.Port.ToString();

            //ノード情報を更新する
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                foreach (ClientData data in listClient.Items)
                {
                    //ACKが帰ってきた場合はIPとポートが取得情報と一致している
                    if (data.global_ip == ipaddress && data.global_port == port)
                    {
                        data.comminication_port = port;
                        data.commnication_address = ipaddress;
                        data.connection_request_sequenceNumber = 0;
                        data.node_status = ClientData.NodeStatus.Connected;
                        break;
                    }
                    else if (data.local_ip == ipaddress && data.local_port == port)
                    {
                        data.comminication_port = port;
                        data.commnication_address = ipaddress;
                        data.connection_request_sequenceNumber = 0;
                        data.node_status = ClientData.NodeStatus.Connected;
                        break;
                    }
                    else if (data.local_ip2 == ipaddress && data.local_port2 == port)
                    {
                        data.comminication_port = port;
                        data.commnication_address = ipaddress;
                        data.connection_request_sequenceNumber = 0;
                        data.node_status = ClientData.NodeStatus.Connected;
                        break;
                    }
                    else if (data.local_ip3 == ipaddress && data.local_port3 == port)
                    {
                        data.comminication_port = port;
                        data.commnication_address = ipaddress;
                        data.connection_request_sequenceNumber = 0;
                        data.node_status = ClientData.NodeStatus.Connected;
                        break;
                    }
                }
            }));


            Debug.WriteLine("OnEventAckConnectionRequest <<");
        }

        private void OnEventErrorRecivedAck(int id, uint sequenceNumber)
        {
            Debug.WriteLine("OnEventErrorRecivedAck >>");
            Debug.WriteLine(string.Format("OnEventErrorRecivedAck id={0}, sequenceNumber={1}", id, sequenceNumber));
            if (id == (int)UDPFileTransfer.DataID.ConnectionRequest)
            {
                //ACKが届かなかったのが接続要求だった場合の処理
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    foreach (ClientData clitentdata in listClient.Items)
                    {
                        //Debug.WriteLine(string.Format("OnEventErrorRecivedAck client={0}, sequenceNumber={1}", clitentdata.user,clitentdata.connection_request_sequenceNumber));
                        if (clitentdata.connection_request_sequenceNumber == sequenceNumber)
                        {
                            clitentdata.node_status = ClientData.NodeStatus.None;
                            clitentdata.connection_request_sequenceNumber = 0;
                            Debug.WriteLine(string.Format("OnEventErrorRecivedAck {0} is not connected", clitentdata.user));
                            break;
                        }
                    }
                }));

            }
            Debug.WriteLine("OnEventErrorRecivedAck <<");
        }

        //ソケットエラーハンドラ
        private void OnEventErrorSocket(SocketException exception)
        {
            Debug.WriteLine("OnEventErrorSocket >>");
            Debug.WriteLine("OnEventErrorSocket <<");
        }

        private void OnEventRecivedConnectionRequest(string client_name, IPEndPoint endpoint)
        {
            Debug.WriteLine("OnEventRecivedConnectionRequest >>");

            //返信元のアドレス/ポートを得る
            string ipaddress = endpoint.Address.ToString();
            string port = endpoint.Port.ToString();

            Debug.WriteLine(string.Format("OnEventRecivedConnectionRequest client_name={0}, ipaddress={1}, port={2}", client_name, ipaddress, port));

            //ノード情報を更新する
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                foreach (ClientData data in listClient.Items)
                {
                    //ACKが帰ってきた場合はIPとポートが取得情報と一致している
                    if (data.user == client_name)
                    {
                        data.comminication_port = port;
                        data.commnication_address = ipaddress;
                        data.connection_request_sequenceNumber = 0;
                        data.node_status = ClientData.NodeStatus.Connected;
                        break;
                    }
                }
            }));

            Debug.WriteLine("OnEventRecivedConnectionRequest <<");
        }

        private void OnEventRecivedFileProgress(TransferProgress data){

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                ClientData client = GetClientDataFromEndpoint(data.endpoint);
                if (client == listClient.SelectedItem)
                {
                    Debug.WriteLine(string.Format("OnEventRecivedFileProgress [from {2}]{0} : {1}%", data.name, data.progress * 100, client.user));

                    //取得したファイルが選択されているクライアント場合、リストのデータに反映する
                    foreach (SharedData shareddata in listConnectedShare.Items)
                    {
                        //Debug.WriteLine(string.Format("OnEventRecivedFileProgress {0} => {1}", shareddata.name, data.original_name));
                        if (shareddata.name == data.original_name)
                        {
                            shareddata.downloadedrate = data.progress * 100;
                            break;
                        }
                    }
                }
            }));
        }

        private void OnEventRecivedFile(TransferProgress data)
        {
            Debug.WriteLine("OnEventRecivedFile >>");
            
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                ClientData client = GetClientDataFromEndpoint(data.endpoint);
                if (client == listClient.SelectedItem)
                {
                    Debug.WriteLine(string.Format("OnEventRecivedFile [from {2}]{0} : {1}%", data.name, data.progress * 100, client.user));

                    //取得したファイルが選択されているクライアント場合、リストのデータに反映する
                    foreach (SharedData shareddata in listConnectedShare.Items)
                    {
                        //Debug.WriteLine(string.Format("OnEventRecivedFileProgress {0} => {1}", shareddata.name, data.original_name));
                        if (shareddata.name == data.original_name)
                        {
                            string new_filename = "";
                            try
                            {
                                //ダウンロードしたファイルを保存フォルダに移動する
                                //new_filename = GetAppDataPath() + CLIENT_DATA_FOLDER + "\\" + client.user + "\\" + System.IO.Path.GetFileName(data.name);
                                new_filename = GetAppDataPath() + CLIENT_DATA_FOLDER + "\\" + client.user;
                                if(Directory.Exists(new_filename) == false)
                                {
                                    Directory.CreateDirectory(new_filename);
                                }
                                new_filename = new_filename + "\\" + System.IO.Path.GetFileName(data.name);
                                File.Move(data.name, new_filename);
                            }
                            catch(Exception ex){
                                Debug.WriteLine(string.Format("OnEventRecivedFile Error = {0}", ex.Message));
                                if (File.Exists(data.name) == true)
                                {
                                    File.Delete(data.name);
                                }
                            }

                            shareddata.filepath = new_filename;
                            shareddata.downloadedrate = 100;
                            
                            SaveCurrentSharedData();
                            break;
                        }
                    }
                }
            }));

            Debug.WriteLine("OnEventRecivedFile <<");
        }

        private void OnEventRecivedBinary(byte[] data, IPEndPoint endpoint)
        {
            Debug.WriteLine("OnEventRecivedBinary >>");

            DataType type = (DataType)BitConverter.ToInt32(data, 0);

            Debug.WriteLine("OnEventRecivedBinary data type = " + type.ToString());
            switch(type){
                case DataType.Data_File:
                    break;
                case DataType.Data_FileList:
                    OnEventRecivedBinary_DataFileList(data, endpoint);
                    break;
                case DataType.Request_File:
                    //要求元にファイルを送信する
                    OnEventRecivedBinary_RequestFile(data, endpoint);
                    break;
                case DataType.Request_FileList:
                    //要求元にファイルリストを送信する
                    OnEventRecivedBinary_RequestFileList(data, endpoint);
                    break;
            }

            Debug.WriteLine("OnEventRecivedBinary <<");
        }

        private void OnEventRecivedBinary_DataFileList(byte[] data, IPEndPoint endpoint)
        {
            Debug.WriteLine("OnEventRecivedBinary_DataFileList >>");
            Debug.WriteLine(string.Format("OnEventRecivedBinary_DataFileList from IP={0}, Port={1}", endpoint.Address.ToString(), endpoint.Port));

            string returnData = System.Text.Encoding.UTF8.GetString(data, 4, data.Length - 4);

            //JSONからArrayに戻す
            var arrayJson = DynamicJson.Parse(returnData);

            // (type) is shortcut of Deserialize<type>()
            var array1 = arrayJson.Deserialize<ShareData[]>();

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                //選択されているノードを得る
                ClientData client = (ClientData)listClient.SelectedItem;
                if (client == null) return;

                //取得したデータをリストに追加する
                foreach (ShareData sharedata in array1)
                {
                    //データがリストにあるかを確認する
                    if (IsExistShareDataInList(sharedata, listConnectedShare.Items) == true) continue;

                    SharedData shareddata = new SharedData();
                    
                    shareddata.guid = sharedata.guid;
                    shareddata.name = sharedata.name;
                    shareddata.length = sharedata.length;
                    shareddata.datatype = sharedata.datatype;

                    listConnectedShare.Items.Add(shareddata);
                }

                //削除されているデータを削除する
                ArrayList arTmp = new ArrayList();
                foreach(SharedData shareddata in listConnectedShare.Items){
                    bool bIsExist = false;
                    foreach (ShareData sharedata in array1)
                    {
                        if (shareddata.guid == sharedata.guid)
                        {
                            bIsExist = true;
                            break;
                        }
                    }
                    if (bIsExist == true) continue;
                    arTmp.Add(shareddata);
                }
                foreach (SharedData shareddata in arTmp)
                {
                    listConnectedShare.Items.Remove(shareddata);
                }

                //データを保存する
                /*
                arTmp.Clear();
                foreach (SharedData shareddata in listConnectedShare.Items)
                {
                    arTmp.Add(shareddata);
                }
                string file = GetAppDataPath() + "\\" + client.user + "_file.xml";
                SaveArrayData(file, arTmp, new Type[] { typeof(SharedData) });
                */
                SaveCurrentSharedData();

                //データ受信スレッドを開始する
                if (m_ThreadDownloadData != null)
                {
                    m_isStopThreadDownloaddata = true;
                    while (m_ThreadDownloadData.IsAlive)
                    {
                        Thread.Sleep(100);
                    }
                }
                m_ThreadDownloadData = new Thread(new ThreadStart(ThreadDownloadData));
                m_ThreadDownloadData.SetApartmentState(System.Threading.ApartmentState.STA);
                m_ThreadDownloadData.Start();
            }));

            Debug.WriteLine("OnEventRecivedBinary_DataFileList <<");
        }

        private void OnEventRecivedBinary_RequestFileList(byte[] data, IPEndPoint endpoint)
        {
            Debug.WriteLine("OnEventRecivedBinary_RequestFileList >>");
            Debug.WriteLine(string.Format("OnEventRecivedBinary_RequestFileList from IP={0}, Port={1}", endpoint.Address.ToString(), endpoint.Port));

            try
            {
                string str = DynamicJson.Serialize(listShareItem.Items);

                //送信文字列をバイナリに変換する
                Byte[] jsonbin = System.Text.Encoding.GetEncoding("utf-8").GetBytes(str);

                //送信データを作成する
                Byte[] send_data = new Byte[jsonbin.Length + 4];
                int data_type = (int)DataType.Data_FileList;
                Buffer.BlockCopy(BitConverter.GetBytes(data_type), 0, send_data, 0, 4);
                Buffer.BlockCopy(jsonbin, 0, send_data, 4, jsonbin.Length);

                //データを送信する
                m_UDPTransfer.Send(send_data, endpoint.Address, endpoint.Port);
            }

            catch (Exception ex)
            {
                Debug.WriteLine("OnEventRecivedBinary_RequestFileList Error = " + ex.Message);
            }

            Debug.WriteLine("OnEventRecivedBinary_RequestFileList <<");

        }

        //要求元にファイルを送信する
        private void OnEventRecivedBinary_RequestFile(byte[] data, IPEndPoint endpoint)
        {
            Debug.WriteLine("OnEventRecivedBinary_RequestFile >>");
            Debug.WriteLine(string.Format("OnEventRecivedBinary_RequestFile from IP={0}, Port={1}", endpoint.Address.ToString(), endpoint.Port));

            try
            {
                //送信されたjsonデータを得る
                string returnData = System.Text.Encoding.UTF8.GetString(data, 4, data.Length - 4);

                //JSONからShareDataに戻す
                var json = DynamicJson.Parse(returnData);
                var shareData = json.Deserialize<ShareData>();

                string filename = shareData.name;
                Debug.WriteLine("OnEventRecivedBinary_RequestFile Target = " + filename);

                //ファイルを送信する
                m_UDPTransfer.Send(filename, endpoint.Address, endpoint.Port);
            }

            catch (Exception ex)
            {
                Debug.WriteLine("OnEventRecivedBinary_RequestFile Error = " + ex.Message);
            }

            Debug.WriteLine("OnEventRecivedBinary_RequestFile <<");

        }

        #endregion

        #region STUN処理
        /// <summary>
        /// STUNサーバーからNATの状態を取得する
        /// </summary>
        /// <returns></returns>
        private bool GetStunInformation(string stun_server, int stun_port)
        {
            Debug.WriteLine("MainWindow::GetStunInformation >>");

            try
            {
                m_StunResult = STUN_Client.Query(stun_server, stun_port, m_UDPTransfer.GetSocket());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainWindow::GetStunInformation Error = " + ex.Message);
                Debug.WriteLine("MainWindow::GetStunInformation <<");
                return false;
            }

            Debug.WriteLine("MainWindow::GetStunInformation <<");

            return true;
        }

        private string [] GetIPAddress()
        {
            //string ipaddress = "";
            IPHostEntry ipentry = Dns.GetHostEntry(Dns.GetHostName());

            string [] ipaddress = new string[ipentry.AddressList.Count()];

            int count = 0;
            foreach (IPAddress ip in ipentry.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    ipaddress[count] = ip.ToString();
                    ++count;
                }
            }
            return ipaddress;
        }

        #endregion

        #region データ要求処理
        private void RequestFileList(ClientData client)
        {
            ClientCommunicationData target_address = GetTargetAddress(client);

            if (target_address.target_ip == "")
            {
                return;
            }

            //送信データを作成する
            Byte[] send_data = new Byte[4];
            int data_type = (int)DataType.Request_FileList;
            Buffer.BlockCopy(BitConverter.GetBytes(data_type), 0, send_data, 0, 4);

            //データを送信する
            IPAddress ipaddress = IPAddress.Parse(target_address.target_ip);
            m_UDPTransfer.Send(send_data, ipaddress, target_address.target_port);
        }

        private void RequestFile(SharedData sharedata, ClientData target)
        {

            ClientCommunicationData target_address = GetTargetAddress(target);

            if (target_address.target_ip == "")
            {
                return;
            }

            //送信データを作成する
            string json_string = DynamicJson.Serialize(sharedata);
            Byte[] json_data = System.Text.Encoding.GetEncoding("utf-8").GetBytes(json_string);

            Byte[] send_data = new Byte[json_data.Length + 4];
            int data_type = (int)DataType.Request_File;
            Buffer.BlockCopy(BitConverter.GetBytes(data_type), 0, send_data, 0, 4);
            Buffer.BlockCopy(json_data, 0, send_data, 4, json_data.Length);

            //データを送信する
            IPAddress ipaddress = IPAddress.Parse(target_address.target_ip);
            m_UDPTransfer.Send(send_data, ipaddress, target_address.target_port);
        }
        #endregion

        #region タイマー - クライアントリスト更新
        private void TimerUpdateClient_Tick(object sender, EventArgs e)
        {
            if (m_ThreadUpdaeClient != null && m_ThreadUpdaeClient.IsAlive == true)
            {
                return;
            }

            m_ThreadUpdaeClient = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < listClient.Items.Count; ++i)
                    {
                        ClientData client = (ClientData)this.Dispatcher.Invoke((Func<ClientData>)(() =>
                        {
                            if (m_isTerminateApplication == true) return null;
                            return (ClientData)listClient.Items[i];
                        }));

                        if (m_UDPTransfer.IsRecivedACK(client.connection_request_sequenceNumber) == false)
                        {
                            Debug.WriteLine("TimerUpdateClient_Tick : not recived ack => sequenceNumber = " + client.connection_request_sequenceNumber.ToString());
                            continue;
                        }

                        //通信ポートを取得する
                        ClientCommunicationData target_address = GetTargetAddress(client);
                        if (target_address.target_ip == "" || target_address.target_port == 0) continue;

                        //接続状態を設定する
                        if (client.node_status != ClientData.NodeStatus.Connected)
                            client.node_status = ClientData.NodeStatus.RequestConnect;

                        //接続要求を送る
                        Debug.WriteLine("TimerUpdateClient_Tick : send request ");
                        uint sequenceNumber = m_UDPTransfer.SendConnectionRequest(m_SIPAccount.Name, target_address.source_ip, target_address.source_port, target_address.target_ip, target_address.target_port);

                        //シーケンス番号がセットされていないときのみセットする
                        //再送信期間が長く受信確認ができない場合があるため
                        if (client.connection_request_sequenceNumber == 0)
                        {
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                if (m_isTerminateApplication == true) return;
                                ClientData tmp = (ClientData)listClient.Items[i];
                                tmp.connection_request_sequenceNumber = sequenceNumber;
                            }));
                        }
                    }
                }
                catch(Exception ex){
                    Debug.WriteLine("TimerUpdateClient_Tick : Error " + ex.Message);
                }
            });
            m_ThreadUpdaeClient.Start();
        }
        #endregion

        #region データ受信スレッド
        private void ThreadDownloadData()
        {
            Debug.WriteLine("ThreadDownloadData >>");
            try
            {
                for (int i = 0; i < listConnectedShare.Items.Count; ++i)
                {
                    Debug.WriteLine(string.Format("ThreadDownloadData checking index = {0}", i));

                    bool isNeedDownload = (bool)this.Dispatcher.Invoke((Func<bool>)(() =>
                    {
                        if (m_isTerminateApplication == true)
                        {
                            Debug.WriteLine(string.Format("ThreadDownloadData << terminate application"));
                            return true;
                        }

                        ClientData client = (ClientData)listClient.SelectedItem;
                        SharedData shareddata = (SharedData)listConnectedShare.Items[i];

                        //保存ファイル名を求める
                        string filename = GetAppDataPath() + CLIENT_DATA_FOLDER + "\\" + client.user + "\\" + System.IO.Path.GetFileName(shareddata.name);

                        //ノードの接続状態を確認する
                        if (client.node_status != ClientData.NodeStatus.Connected)
                        {
                            //接続されていない場合はダウンロード処理をお行わない
                            Debug.WriteLine(string.Format("ThreadDownloadData [{0}]checking client is not connected", i));
                            return true;
                        }

                        if (File.Exists(shareddata.filepath) == false)
                        {
                            //データが無い場合はダウンロードする
                            RequestFile(shareddata, client);
                            shareddata.downloadedrate = 0;
                            Debug.WriteLine(string.Format("ThreadDownloadData [{0}]start download => {1}", i, System.IO.Path.GetFileName(shareddata.name)));
                            return false;

                        }
                        //リストにデータを登録する
                        //shareddata.filepath = filename;
                        shareddata.downloadedrate = 100;

                        return true;
                    }));

                    if (isNeedDownload == true) continue;

                    double downladrate = 0;
                    double old_downladrate = 0;
                    DateTime old_increase_time = DateTime.Now;

                    while (downladrate != 100)
                    {
                        //停止フラグが立っている場合は停止する
                        if (m_isStopThreadDownloaddata == true)
                        {
                            break;
                        }

                        downladrate = (double)this.Dispatcher.Invoke((Func<double>)(() =>
                        {
                            if (m_isTerminateApplication == true) return 0;

                            SharedData shareddata = (SharedData)listConnectedShare.Items[i];
                            return shareddata.downloadedrate;
                        }));

                        if (old_downladrate != downladrate)
                        {
                            old_downladrate = downladrate;
                            old_increase_time = DateTime.Now;
                        }

                        //待機する
                        Thread.Sleep(100);

                        TimeSpan timespan = DateTime.Now.Subtract(old_increase_time);
                        if (timespan.Seconds > 10)
                        {
                            Debug.WriteLine("ThreadDownloadData Time out index = " + i.ToString());
                            break;
                        }
                    }
                    Debug.WriteLine(string.Format("ThreadDownloadData [{0}]finish download", i));
                }

            }
            catch(Exception ex){
                Debug.WriteLine("ThreadDownloadData Error => " + ex.Message);
            }
            Debug.WriteLine("ThreadDownloadData <<");
        }
        #endregion

        #region データ処理
        private string GetAppDataPath()
        {
#if false
            string szDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\JoinNET";
#else
            string szDir = m_ExcuteDirectory;
#endif
            if (Directory.Exists(szDir) == false)
            {
                Directory.CreateDirectory(szDir);
            }
            return szDir;
        }

        private ArrayList LoadArrayData(string szFile, Type[] type)
        {
            //XmlSerializerオブジェクトを作成
            var se = new XmlSerializer(typeof(ArrayList), type);
            ArrayList ar;

            try
            {
                using (var fs = new FileStream(szFile, FileMode.Open))
                {
                    ar = (ArrayList)se.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadShareData : Error : " + ex.Message);
                return null;
            }

            return ar;
        }

        private void LoadShareData()
        {
            var thread = new Thread(() =>
            {
                string file = GetAppDataPath() + "\\sharedata.xml";
                ArrayList ar = LoadArrayData(file, new Type[] { typeof(ShareData) });
                if (ar != null)
                {
                    foreach (ShareData data in ar)
                    {
                        if (m_isTerminateApplication == true) break;

                        this.Dispatcher.Invoke((Action)(() =>
                        {
                            listShareItem.Items.Add(data);
                        }));
                        Thread.Sleep(5);
                    }
                }
            });
            thread.Start();
        }

        private void LoadSharedData()
        {
            Debug.WriteLine("LoadSharedData >>");

            //前のスレッドが動作しているかを確認する
            if (m_ThreadLoadSharedData != null && m_ThreadLoadSharedData.IsAlive == true)
            {
                m_isTopThreadLoadSharedData = true;

                while (true)
                {
                    if (m_ThreadLoadSharedData.IsAlive == false) break;
                    Thread.Sleep(100);
                }

            }

            m_isTopThreadLoadSharedData = false;
            m_ThreadLoadSharedData = new Thread(() =>
            {
                Debug.WriteLine("LoadSharedData::Thread >>");

                //送信先のアドレスを得る
                ClientData target = (ClientData)this.Dispatcher.Invoke((Func<ClientData>)(() =>
                {
                    ClientData tmpTarget = (ClientData)listClient.SelectedItem;
                    if (tmpTarget == null)
                    {
                        Debug.WriteLine("LoadSharedData::Thread << client is not selected");
                        return null;
                    }
                    return tmpTarget;
                }));

                //共有リストをクリアする
                this.Dispatcher.Invoke((Action)(() =>
                {
                    listConnectedShare.Items.Clear();
                }));

                if (m_isTopThreadLoadSharedData == true)
                {
                    Debug.WriteLine("LoadSharedData::Thread << stop sinal is true");
                    return;
                }

                //保存データをロードする
                string file = GetAppDataPath() + "\\" + target.user + "_file.xml";
                ArrayList ar = LoadArrayData(file, new Type[] { typeof(SharedData) });
                if (ar != null)
                {
                    foreach (SharedData data in ar)
                    {
                        if (m_isTopThreadLoadSharedData == true) break;
                        this.Dispatcher.Invoke((Action)(() =>
                        {
                            listConnectedShare.Items.Add(data);
                        }));
                        Thread.Sleep(5);
                    }
                }

                //停止指示があるなら終了する
                if (m_isTopThreadLoadSharedData == true)
                {
                    Debug.WriteLine("LoadSharedData::Thread << stop sinal is true");
                    return;
                }
                
                //ファイルリスト送信要求を行う
                if (target.node_status == ClientData.NodeStatus.Connected)
                {
                    RequestFileList(target);
                }

                Debug.WriteLine("LoadSharedData::Thread <<");
            });
            m_ThreadLoadSharedData.Start();
            Debug.WriteLine("LoadSharedData <<");

        }

        private bool SaveArrayData(string szFile, ArrayList collection, Type[] type)
        {
            try
            {
                //XMLファイルを出力する
                var se = new XmlSerializer(typeof(ArrayList), type);
                using (var fs = new FileStream(szFile, FileMode.Create))
                {
                    se.Serialize(fs, collection);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveArrayData : Error : " + ex.Message);
                return false;
            }
            return true;
        }

        private bool SaveCurrentSharedData()
        {
            try
            {
                ClientData client = (ClientData)listClient.SelectedItem;

                //データを保存する
                ArrayList arTmp = new ArrayList();
                foreach (SharedData shareddata in listConnectedShare.Items)
                {
                    arTmp.Add(shareddata);
                }
                string file = GetAppDataPath() + "\\" + client.user + "_file.xml";
                SaveArrayData(file, arTmp, new Type[] { typeof(SharedData) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveCurrentSharedData : Error : " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// endpointからノードを取得する
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        private ClientData GetClientDataFromEndpoint(IPEndPoint endpoint)
        {
            string address = endpoint.Address.ToString();
            string port = endpoint.Port.ToString();

            foreach (ClientData data in listClient.Items)
            {
                if (address == data.commnication_address && port == data.comminication_port)
                    return data;

                if (address == data.global_ip && port == data.global_port)
                    return data;

                if (address == data.local_ip && port == data.local_port)
                    return data;

                if (address == data.local_ip2 && port == data.local_port2)
                    return data;

                if (address == data.local_ip3 && port == data.local_port3)
                    return data;
            }

            return null;
        }

        private bool LoadSIPAccount(string fileName)
        {
            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(CDataSIPAccount));
                //読み込むファイルを開く
                System.IO.StreamReader sr = new System.IO.StreamReader(
                    fileName, new System.Text.UTF8Encoding(false));
                //XMLファイルから読み込み、逆シリアル化する
                m_SIPAccount = (CDataSIPAccount)serializer.Deserialize(sr);
                //ファイルを閉じる
                sr.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadSIPAccount : Error : " + ex.Message);
                return false;
            }

            return true;
        }

        private bool SaveSIPAccount(string fileName)
        {
            try
            {
                //XmlSerializerオブジェクトを作成
                //オブジェクトの型を指定する
                System.Xml.Serialization.XmlSerializer serializer =
                    new System.Xml.Serialization.XmlSerializer(typeof(CDataSIPAccount));
                //書き込むファイルを開く（UTF-8 BOM無し）
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, m_SIPAccount);
                //ファイルを閉じる
                sw.Close();
            }

            catch (Exception ex)
            {
                Debug.WriteLine("SaveSIPAccount Error = " + ex.Message);
                return false;
            }

            return true;
        }

        private void LoadClientList()
        {
            var thread = new Thread(() =>
            {
                string file = GetAppDataPath() + "\\clientsdata.xml";
                ArrayList ar = LoadArrayData(file, new Type[] { typeof(ClientData) });
                if (ar != null)
                {
                    foreach (ClientData data in ar)
                    {
                        if (m_isTerminateApplication == true) break;

                        this.Dispatcher.Invoke((Action)(() =>
                        {
                            listClient.Items.Add(data);
                        }));
                        Thread.Sleep(5);
                    }
                }
            });
            thread.Start();
        }

        private bool SaveClientList()
        {
            try
            {
                //データを保存する
                ArrayList arTmp = new ArrayList();
                foreach (ClientData shareddata in listClient.Items)
                {
                    arTmp.Add(shareddata);
                }
                string file = GetAppDataPath() + "\\clientsdata.xml";
                SaveArrayData(file, arTmp, new Type[] { typeof(ClientData) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveClientList : Error : " + ex.Message);
                return false;
            }

            return true;
        }

        #endregion

        #region SIP
        private bool SIP_Login()
        {
            uint hr;

            if (m_SIPInstance == IntPtr.Zero) return false;

            //ユーザー情報を設定する
            CWrapLibSIP.SIP_SetUser(m_SIPInstance,
                m_SIPAccount.Uri,
                m_SIPAccount.Password,
                m_SIPAccount.Name,
                m_SIPAccount.Server);

            //コールバック関数を設定する
            CWrapLibSIP.SIP_SetCallbackFunc(m_SIPInstance, m_SIPCallback, IntPtr.Zero);

            //SIPを初期化する
            hr = CWrapLibSIP.SIP_Init(m_SIPInstance, CWrapLibSIP.AF_INET, "192.168.78.100", "JoinNET");
            Debug.WriteLine("SIP_Login : SIP_Init => " + hr.ToString());

            //SIPサーバーに情報を登録する
            hr = CWrapLibSIP.SIP_Regist(m_SIPInstance);
            Debug.WriteLine("SIP_Login : SIP_Regist => " + hr.ToString());

            m_SIPAccount.status = CDataSIPAccount.Status.Registing;

            //SIPスレッドを実行する
            CWrapLibSIP.SIP_Run();

            return true;
        }

        private bool SIP_Logout()
        {
            uint hr;

            //スレッドを停止する
            hr = CWrapLibSIP.SIP_STOP();
            Debug.WriteLine("btn_close_Click : SIP_STOP => " + hr.ToString());

            //スレッドが停止するまで待機する
            for (int i = 0; i < 1000; ++i)
            {
                if (CWrapLibSIP.SIP_IsRunning() == true) break;
                System.Threading.Thread.Sleep(10);
            }

            //SIP通信wの終了する
            hr = CWrapLibSIP.SIP_Terminate(m_SIPInstance);
            Debug.WriteLine("btn_close_Click : SIP_Terminate => " + hr.ToString());
            hr = CWrapLibSIP.SIP_UnInit(m_SIPInstance);
            Debug.WriteLine("btn_close_Click : SIP_UnInit => " + hr.ToString());

            return true;
        }

        private void SIPCallback_Regist(IntPtr param, int err, int scode, string sip_message)
        {
            Debug.WriteLine(string.Format("SIPCallback_Regist : scode={0}, err={1}", scode, err));

            if (sip_message != null && sip_message != "")
            {
                try
                {
                    //ASCII エンコード
                    byte[] data = System.Text.Encoding.ASCII.GetBytes(sip_message);

                    SIP_Response respons = SIP_Response.Parse(data);
                    Debug.WriteLine(string.Format("SIPCallback_Regist : To = {0}", respons.To.Address.Uri.ToString()));
                    Debug.WriteLine(string.Format("SIPCallback_Regist : From = {0}", respons.From.Address.Uri.ToString()));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("SIPCallback_Regist : Error => {0}", ex.Message));
                }
            }

            if (scode == 200)
                m_SIPAccount.status = CDataSIPAccount.Status.Registeded;
            else
                m_SIPAccount.status = CDataSIPAccount.Status.CantRegist;
        }

        private void SIPCallback_MessageReceive(IntPtr param, int err, int scode, string sip_message)
        {
            //ASCII エンコード
            byte[] data = System.Text.Encoding.ASCII.GetBytes(sip_message);

            SIP_Request respons = SIP_Request.Parse(data);
            Debug.WriteLine(string.Format("SIPCallback_MessageReceive : To = {0}", respons.To.Address.Uri.ToString()));
            Debug.WriteLine(string.Format("SIPCallback_MessageReceive : From = {0}", respons.From.Address.Uri.ToString()));

            int pos = sip_message.IndexOf("\r\n\r\n");
            if (pos > 0)
            {
                //ノード情報を更新する
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    string content = sip_message.Substring(pos + 4);
                    Debug.WriteLine(string.Format("SIPCallback_MessageReceive : content = {0}", content));
                    textMessage.Text = textMessage.Text + "\r\n" + respons.From.Address.DisplayName + " : " + content;
                }));
            }
        }

        private void SIPCallback_MessageResponse(IntPtr param, int err, int scode, string sip_message)
        {
            if(sip_message == null)
            {
                Debug.WriteLine(string.Format("SIPCallback_MessageResponse : err={0}, scode={1}", err, scode));
                return;
            }

            //ASCII エンコード
            byte[] data = System.Text.Encoding.ASCII.GetBytes(sip_message);

            SIP_Response respons = SIP_Response.Parse(data);
            Debug.WriteLine(string.Format("SIPCallback_MessageResponse : To = {0}", respons.To.Address.Uri.ToString()));
            Debug.WriteLine(string.Format("SIPCallback_MessageResponse : From = {0}", respons.From.Address.Uri.ToString()));
        }

        private void SIPCallback_Exit(IntPtr param, int err, int scode, string sip_message)
        {
            Debug.WriteLine(string.Format("SIPCallback_Exit"));
        }

        private void SIPCallback_Invite_Offer(IntPtr param, int err, int scode, string sip_message)
        {
            int pos = sip_message.IndexOf("\r\n\r\n");
            if (pos > 0)
            {
               string content = sip_message.Substring(pos + 4);
               Debug.WriteLine(string.Format("SIPCallback_Invite_Offer : body = {0}", content));
            }

            Debug.WriteLine(string.Format("SIPCallback_Invite_Offer"));
        }

        private void SIPCallback_Invite_Receive(IntPtr param, int err, int scode, string sip_message)
        {
            int pos = sip_message.IndexOf("\r\n\r\n");
            if (pos > 0)
            {
                string content = sip_message.Substring(pos + 4);
                Debug.WriteLine(string.Format("SIPCallback_Invite_Receive : body = {0}", content));

                try
                {
                    //SIPメッセージを確認する
                    byte[] data = System.Text.Encoding.ASCII.GetBytes(sip_message);

                    SIP_Request request = SIP_Request.Parse(data);
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Receive : To = {0}", request.To.Address.Uri.ToString()));
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Receive : From = {0}", request.From.Address.Uri.ToString()));

                    //SDPを確認する
                    LumiSoft.Net.SDP.SDP_Message sdp_message = LumiSoft.Net.SDP.SDP_Message.Parse(content);
                    foreach(SDP_MediaDescription sdp_media in sdp_message.MediaDescriptions)
                    {
                       Debug.WriteLine(
                           string.Format("SIPCallback_Invite_Receive : IP={0},Port{1},MeduaType={2},Protocol={3}",
                           sdp_media.Connection.Address,
                           sdp_media.Port,
                           sdp_media.MediaType,
                           sdp_media.Protocol));
                    }

                    //クライアントリストにIPを設定する
                    this.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        foreach (ClientData item in listClient.Items)
                        {
                            Debug.WriteLine(
                                string.Format("SIPCallback_Invite_Receive : useruri={0},from={1}",
                                item.useruri, request.From.Address.Uri.ToString()));

                            if (string.Compare(item.useruri,request.From.Address.Uri.ToString(),true) == 0)
                            {
                                int ncount = 0;
                                foreach (SDP_MediaDescription sdp_media in sdp_message.MediaDescriptions)
                                {
                                    switch (ncount)
                                    {
                                        case 0:
                                            item.local_ip = sdp_media.Connection.Address;
                                            item.local_port = sdp_media.Port.ToString();
                                            break;
                                        case 1:
                                            item.local_ip2 = sdp_media.Connection.Address;
                                            item.local_port2 = sdp_media.Port.ToString();
                                            break;
                                        case 2:
                                            item.local_ip3 = sdp_media.Connection.Address;
                                            item.local_port3 = sdp_media.Port.ToString();
                                            break;
                                    }
                                    ++ncount;
                                }
                            }
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Receive : Error at SDP_Message.Parse => " + ex.Message));
                }
            }

            Debug.WriteLine(string.Format("SIPCallback_Invite_Receive"));
        }

        private void SIPCallback_Invite_Answer(IntPtr param, int err, int scode, string sip_message)
        {
            int pos = sip_message.IndexOf("\r\n\r\n");
            if (pos > 0)
            {
                string content = sip_message.Substring(pos + 4);
                Debug.WriteLine(string.Format("SIPCallback_Invite_Answer : body = {0}", content));

                try
                {
                    //SIPメッセージを確認する
                    byte[] data = System.Text.Encoding.ASCII.GetBytes(sip_message);

                    SIP_Response respons = SIP_Response.Parse(data);
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Answer : To = {0}", respons.To.Address.Uri.ToString()));
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Answer : From = {0}", respons.From.Address.Uri.ToString()));

                    //SDPを確認する
                    LumiSoft.Net.SDP.SDP_Message sdp_message = LumiSoft.Net.SDP.SDP_Message.Parse(content);
                    foreach (SDP_MediaDescription sdp_media in sdp_message.MediaDescriptions)
                    {
                        Debug.WriteLine(
                            string.Format("SIPCallback_Invite_Answer : IP={0},Port{1},MeduaType={2},Protocol={3}",
                            sdp_media.Connection.Address,
                            sdp_media.Port,
                            sdp_media.MediaType,
                            sdp_media.Protocol));
                    }

                    //クライアントリストにIPを設定する
                    this.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        foreach (ClientData item in listClient.Items)
                        {
                            Debug.WriteLine(
                                string.Format("SIPCallback_Invite_Answer : useruri={0},from={1}",
                                item.useruri, respons.To.Address.Uri.ToString()));

                            if (string.Compare(item.useruri, respons.To.Address.Uri.ToString(), true) == 0)
                            {
                                int ncount = 0;
                                foreach (SDP_MediaDescription sdp_media in sdp_message.MediaDescriptions)
                                {
                                    switch (ncount)
                                    {
                                        case 0:
                                            item.local_ip = sdp_media.Connection.Address;
                                            item.local_port = sdp_media.Port.ToString();
                                            break;
                                        case 1:
                                            item.local_ip2 = sdp_media.Connection.Address;
                                            item.local_port2 = sdp_media.Port.ToString();
                                            break;
                                        case 2:
                                            item.local_ip3 = sdp_media.Connection.Address;
                                            item.local_port3 = sdp_media.Port.ToString();
                                            break;
                                    }
                                    ++ncount;
                                }
                            }
                        }
                    }));

                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("SIPCallback_Invite_Answer : Error at SDP_Message.Parse => " + ex.Message));
                }
            }

            Debug.WriteLine(string.Format("SIPCallback_Invite_Answer"));
        }

        /// <summary>
        /// SIPイベントコールバックハンドラ
        /// </summary>
        /// <param name="param"></param>
        /// <param name="err">通信エラーコード</param>
        /// <param name="type">イベントタイプ</param>
        /// <param name="scode">SIPステータスコード</param>
        /// <param name="sip_message">SIPメッセージ</param>
        public void SIPCallBack(IntPtr param, int err, CWrapLibSIP.SIP_CALLBACK_TYPE type, int scode, string sip_message)
        {
            //Debug.WriteLine(string.Format("SIPCallback : err={2}, type={0}, message={1}", type.ToString(), sip_message, err));
            Debug.WriteLine(string.Format("SIPCallback : err={0}, type={1}", err, type.ToString()));

            switch (type)
            {
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_REGIST:
                    SIPCallback_Regist(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_MESSAGE_RECEIVE:
                    SIPCallback_MessageReceive(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_MESSAGE_RESPONCE:
                    SIPCallback_MessageResponse(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_EXIT:
                    SIPCallback_Exit(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_INVITE_OFFER:
                    SIPCallback_Invite_Offer(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_INVITE_RECEIVE:
                    SIPCallback_Invite_Receive(param, err, scode, sip_message);
                    break;
                case CWrapLibSIP.SIP_CALLBACK_TYPE.SIPCALLBACK_INVITE_ANSWER:
                    SIPCallback_Invite_Answer(param, err, scode, sip_message);
                    break;
            }
        }
#endregion

        #region イベントハンドラ
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow::Window_Loaded >>");

            //実行ディレクトリを取得する
            m_ExcuteDirectory = GetStatupDirectory();

            //SIP設定をロードする
            string account_file = GetAppDataPath() + "\\sipaccount.xml";
            if(LoadSIPAccount(account_file) == false)
            {
                //TODO:アカウント設定画面を表示する
            }
            m_SIPAccount.status = CDataSIPAccount.Status.None;
            txtSIPStatus.DataContext = m_SIPAccount;

            //コンタクトリストをロードする
            LoadClientList();
            ClientData client = new ClientData();

            //共有ファイル設定をロードする
            LoadShareData();

            //データフォルダを作成する
            string data_folder = GetAppDataPath() + CLIENT_DATA_FOLDER;
            if (Directory.Exists(data_folder) == false)
            {
                Directory.CreateDirectory(data_folder);
            }

            //ローディイグアニメーションを開始する
            //AnimationClientLoading_Run();
            clientLoadingInformation.Visibility = System.Windows.Visibility.Collapsed;

            //動作中のPCのロカールIPを取得する
            m_LocapIP = GetIPAddress();

            //通信用ソケットを作成する
            m_UDPTransfer = new UDPFileTransfer();
            m_UDPTransfer.EventHndler = EventTransfer;
            m_UDPTransfer.Init();

            //SIPオブジェクトを作成する
            CWrapLibSIP.API_Init(out m_SIPInstance);
            m_SIPCallback = new CWrapLibSIP.CallBack(SIPCallBack);

            //SIPサーバーへレジストする
            SIP_Login();

            //STUNの情報を取得する
            GetStunInformation(m_StunServer, m_StunServerPort);

            //受信処理を開始する
            m_UDPTransfer.RunListen();

            //SIPに接続情報を設定する
            string public_ip = "";
            int public_port = 0;
            int ip_count = 0;
            int local_port = m_UDPTransfer.GetPort();

            if (m_StunResult.PublicEndPoint != null)
            {
                public_ip = m_StunResult.PublicEndPoint.Address.ToString();
                public_port = m_StunResult.PublicEndPoint.Port;
            }

            if(public_ip != "")
            {
                CWrapLibSIP.SIP_SetAddress(m_SIPInstance, 0, CWrapLibSIP.AF_INET, public_ip, public_port);
                ++ip_count;
            }
            foreach (string ip in m_LocapIP)
            {
                if(ip != null)
                {
                    CWrapLibSIP.SIP_SetAddress(m_SIPInstance, ip_count, CWrapLibSIP.AF_INET, ip, local_port);
                    ++ip_count;
                }
            }

            //ACK送信タイマーを起動する
            m_TimerUpdateClient.Start();

            Debug.WriteLine("MainWindow::Window_Loaded <<");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine("MainWindow::Window_Closing >>");

            //アプリ終了フラグを有効にする
            m_isTerminateApplication = true;

            //SIPサーバーへレジストする
            SIP_Logout();

            //UDP受信を停止する
            m_TimerUpdateClient.Stop();

            //設定を保存する
            Properties.Settings.Default.Save();

            //SIP設定を保存する
            string account_file = GetAppDataPath() + "\\sipaccount.xml";
            SaveSIPAccount(account_file);

            //コンタクトリストを保存する
            SaveClientList();

            //共有データを保存する
            string file = GetAppDataPath() + "\\sharedata.xml";
            ArrayList ar = new ArrayList();
            foreach (ShareData data in listShareItem.Items)
            {
                ar.Add(data);
            }
            SaveArrayData(file, ar, new Type[] { typeof(ShareData) });
            Debug.WriteLine("MainWindow::Window_Closing <<");
        }

        private void listShareItem_Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("listShareItem_Drop >>");
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                foreach (var s in files)
                {
                    //登録データが重複していないかを調べる
                    bool isFound = false;
                    foreach (ShareData item in listShareItem.Items)
                    {
                        if (item.name == s)
                        {
                            isFound = true;
                            break;
                        }
                    }
                    if (isFound == true) continue;

                    //拡張子を確認する
                    string extention = System.IO.Path.GetExtension(s);
                    if (extention.ToUpper() != ".JPG") continue;

                    //データを作成する
                    FileInfo fileinfo = new FileInfo(s);
                    ShareData data = new ShareData();
                    
                    data.name = s;
                    data.guid = System.Guid.NewGuid().ToString();
                    data.length = fileinfo.Length;
                    data.datatype = ShareData.DATA_TYPE.File;

                    //データを登録する
                    listShareItem.Items.Add(data);
                }
            } 
            Debug.WriteLine("listShareItem_Drop <<");
        }

        private void listShareItem_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void listClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSharedData();
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            switch (tabList.SelectedIndex)
            {
                case 0://共有リスト
                    {
                        ShareData dat = ((Button)sender).DataContext as ShareData;
                        listShareItem.Items.Remove(dat);
                    }
                    break;
                case 1://接続先の共有リスト
                    {
                        SharedData dat = ((Button)sender).DataContext as SharedData;
                        dat.is_delete = true;
                        SaveCurrentSharedData();
                    }
                    break;
            }
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            uint hr;

            ClientData client = (ClientData)listClient.SelectedItem;

            if (client == null) return;

            hr = CWrapLibSIP.SIP_Message(m_SIPInstance, client.useruri, textChatMessage.Text);
            Debug.WriteLine("btn_message_Click : SIP_Message => " + hr.ToString());
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            uint hr;

            ClientData client = (ClientData)listClient.SelectedItem;

            if (client == null) return;

            string public_ip = "";
            int public_port = 0;
            int local_port = m_UDPTransfer.GetPort();

            if (m_StunResult.PublicEndPoint != null)
            {
                public_ip = m_StunResult.PublicEndPoint.Address.ToString();
                public_port = m_StunResult.PublicEndPoint.Port;
            }

            hr = CWrapLibSIP.SIP_Invite(m_SIPInstance, client.useruri);

            Debug.WriteLine("btnConnect_Click : SIP_Invite => " + hr.ToString());
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            uint hr;

            ClientData client = (ClientData)listClient.SelectedItem;

            if (client == null) return;

            hr = CWrapLibSIP.SIP_Bye(m_SIPInstance, client.useruri);
            Debug.WriteLine("btnDisconnect_Click : SIP_Bye => " + hr.ToString());

        }

        #endregion

    }
}
