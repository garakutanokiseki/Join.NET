using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Xml.Serialization;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using PixelLab.Wpf.Transitions;

using Codeplex.Data;

using System.Net;
using System.Net.Sockets;

using LumiSoft.Net.STUN.Client;

using TCPtunnelViaUDP;
using JoinNET.WebServer;

using System.ComponentModel;
using System.Globalization;

namespace JoinNET
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region トラジションの定義
        //トラジションのID
        public enum TrasitionType
        {
            Trasition_None = 0,
            Trasition_Fade,
            Trasition_FadeWipe2,
            Transition_Translate_Right,
            Transition_Translate_Left,
            Trasition_SlideRight,
            Trasition_SlideLeft,
            Transition_MaxCount,
        }

        //トラジション
        private PixelLab.Wpf.Transitions.Transition[] m_Transition;
        private const int TRASITION_MAX_COUNT = 7;
        #endregion

        //ネットワーク情報
        private string[] m_LocapIP;

        //通信処理用
        DataAccount m_Account = new DataAccount();
        CJoinnetClient m_JoinnetWeb = new CJoinnetClient();
        DataAddressSet m_CurrentConnectionAddrsss;//m_TuunelClientの接続先アドレスセット

        //ユーザー設定
        DataSetting m_Setting;

        //通信用ソケット
        CTCPTunnelClient m_TuunelClient;
        CTCPTunnelServer m_TunnelServer;
        Socket m_SocketTunnelClient;
        Socket m_SocketTunnelServer;

        //WEBサーバー
        CWebServer m_WebServer;

        //STUNサーバ0定義
        private const string m_StunServer = "stun.l.google.com";
        private const int m_StunServerPort = 19302;

        //STUN取得結果
        private STUN_Result m_StunResultTunnelClient;
        private STUN_Result m_StunResultTunnelServer;

        //保存ディレクトリ名
        const string ROOT_DATA_FOLDER = "\\data";
        const string CLIENT_DATA_FOLDER = "\\DataClient";

        //実行ディレクトリ
        string m_ExcuteDirectory;

        //ユーザーコントロール
        CBasePage         m_PreviousPage; //前に表示していたページ
        UCLogin             m_PageLogin;
        UCContactList       m_PageContactList;
        UCWebBrowsre        m_PageWebBrowser;
        UCSetting           m_PageUcSetting;
        UCConnectionList    m_PageConnectionList;
        UCOptionalFunction  m_PageOptionalFunction;
        UC_About            m_PageAbout;

        //タイマー
        DispatcherTimer m_TimerUpdateConnectionList;

        public MainWindow()
        {
            InitializeComponent();

            //トラジションをリソースから読み込む
            m_Transition = new Transition[TRASITION_MAX_COUNT];
            m_Transition[(int)TrasitionType.Trasition_None] = (Transition)FindResource("Transition_Base");
            m_Transition[(int)TrasitionType.Trasition_Fade] = (Transition)FindResource("Transition_Fade");
            m_Transition[(int)TrasitionType.Trasition_FadeWipe2] = (Transition)FindResource("Transition_FadeWipe2");
            m_Transition[(int)TrasitionType.Transition_Translate_Right] = (Transition)FindResource("Transition_Translate_Right");
            m_Transition[(int)TrasitionType.Transition_Translate_Left] = (Transition)FindResource("Transition_Translate_Left");
            m_Transition[(int)TrasitionType.Trasition_SlideRight] = (Transition)FindResource("Transition_SlideRight");
            m_Transition[(int)TrasitionType.Trasition_SlideLeft] = (Transition)FindResource("Transition_SlideLeft");

            //言語設定の初期値を取得する
            if (Properties.Settings.Default.language == "")
            {
                Properties.Settings.Default.language = CultureInfo.CurrentCulture.Name;
            }

            //各ページのインスタンスを作る
            m_PageLogin = new UCLogin();
            m_PageLogin.m_EventHndler = EventHandler_PageLogin;

            m_PageContactList = new UCContactList();
            m_PageContactList.m_EventHndler = EventHandler_ContactList;

            m_PageWebBrowser = new UCWebBrowsre();

            m_PageUcSetting = new UCSetting();

            m_PageConnectionList = new UCConnectionList();

            m_PageOptionalFunction = new UCOptionalFunction();

            m_PageAbout = new UC_About();

            //トラジションを設定する
            PanelMain.Transition = m_Transition[(int)TrasitionType.Trasition_Fade];

            //タイマーを作成する
            m_TimerUpdateConnectionList = new DispatcherTimer(DispatcherPriority.Normal);
            m_TimerUpdateConnectionList.Interval = new TimeSpan(0, 0, 1);
            m_TimerUpdateConnectionList.Tick += new EventHandler(TimerUpdateConnectionList);

            //初期ページを設定する
            NavigateTo(m_PageLogin, TrasitionType.Trasition_None, false);

            //メニューにデータコンテキストを設定する
            pane_toolbar.DataContext = this;
            pane_status.DataContext = this;

            //ハンドラを設定する
            m_JoinnetWeb.m_EventHndler = WebSocketCallback;
        }

        #region メンバ関数
        DataMessage CreateConnectionMessage(STUN_Result stun_result, Socket socket, DataMessage.Command command, string ToUri)
        {
            //通信アドレスを取得する
            IPEndPoint endpoint = (IPEndPoint)socket.LocalEndPoint;
            int local_port = endpoint.Port;

            DataAddressSet address_set = new DataAddressSet();

            switch (m_Setting.TunnelMode)
            {
                case DataSetting.enumTunnelMode.OptionalFunction:
                    address_set.tunnelmode = DataAddressSet.TunnelMode.OptionalFunction;
                    break;
                case DataSetting.enumTunnelMode.WebServer:
                default:
                    address_set.tunnelmode = DataAddressSet.TunnelMode.WebServer;
                    break;
            }

            address_set.grobal = new DataAddress();
            address_set.grobal.address = stun_result.PublicEndPoint.Address.ToString();
            address_set.grobal.port = stun_result.PublicEndPoint.Port;

            address_set.local = new DataAddress[m_LocapIP.Count()];
            for (int i = 0; i < m_LocapIP.Count(); ++i)
            {
                address_set.local[i] = new DataAddress();
                address_set.local[i].address = m_LocapIP[i];
                address_set.local[i].port = local_port;
            }

            //JSONを作成する
            string json_string = DynamicJson.Serialize(address_set);

            Debug.WriteLine("Menu_Connect_Click address_set = " + json_string);

            //メッセージを記録する
            DataMessage data_message = new DataMessage();
            data_message.ID = -1;
            data_message.date = DateTime.Now;
            data_message.From = m_Account.Name;
            data_message.To = ToUri;
            data_message.is_sent = false;
            data_message.command = command;
            data_message.message = json_string;

            return data_message;
        }

        DataClient UpdateClientInformation(string user, DataAddressSet address_set)
        {
            //クライアントリストにIPを設定する
            foreach (DataClient item in m_PageContactList.listClient.Items)
            {
                if (string.Compare(item.user, user, true) != 0) continue;

                item.global_ip = address_set.grobal.address;
                item.global_port = address_set.grobal.port.ToString();

                item.local_ip = "";
                item.local_port = "";

                item.local_ip2 = "";
                item.local_port2 = "";

                item.local_ip2 = "";
                item.local_port2 = "";

                for (int i = 0;i <  address_set.local.Count();++i)
                {
                    if (address_set.local[i].address == null) continue;
                    switch (i)
                    {
                        case 0:
                            item.local_ip = address_set.local[i].address;
                            item.local_port = address_set.local[i].port.ToString();
                            break;
                        case 1:
                            item.local_ip2 = address_set.local[i].address;
                            item.local_port2 = address_set.local[i].port.ToString();
                            break;
                        case 2:
                            item.local_ip3 = address_set.local[i].address;
                            item.local_port3 = address_set.local[i].port.ToString();
                            break;
                    }
                }

                return item;
            }

            return null;
        }

        void NavigateTo(CBasePage toUserControl, TrasitionType trasition_type, bool is_enable_back_button)
        {
            if (PanelMain.Content == toUserControl) return;

            if(PanelMain.Content == m_PageConnectionList)
            {
                //表示タイマーを停止する
                m_TimerUpdateConnectionList.Stop();
            }
            else if(PanelMain.Content == m_PageWebBrowser)
            {
                //ブラウザを非表示する(遷移中に表示されているとアニメーションが正常に表示されない対策)
                m_PageWebBrowser.Browser.Visibility = Visibility.Hidden;
            }
            else if (PanelMain.Content == m_PageUcSetting)
            {
                //Webサーバーの設定を行う
                m_WebServer.SetDocumentRoot(m_Setting.document_root);
                m_WebServer.IsAutoIndexes = m_Setting.is_auto_html;

                //トンネルの転送先を設定する
                switch (m_Setting.TunnelMode)
                {
                    case DataSetting.enumTunnelMode.OptionalFunction:
                        m_TunnelServer.m_address = m_Setting.target_ip;
                        m_TunnelServer.m_port = m_Setting.target_port;
                        break;
                    case DataSetting.enumTunnelMode.WebServer:
                    default:
                        m_TunnelServer.m_address = "127.0.0.1";
                        m_TunnelServer.m_port = m_WebServer.Port;
                        break;
                }
            }

            toUserControl.is_enable_back_button = is_enable_back_button;

            //前に表示されたページを保存する
            try
            {
                //サイドメニュー内のページで無い時は記録する
                if (PanelMain.Content != m_PageConnectionList &&
                    PanelMain.Content != m_PageUcSetting &&
                    PanelMain.Content != m_PageAbout)
                {
                    m_PreviousPage = (CBasePage)PanelMain.Content;
                }
            }
            catch (Exception ex)
            {
                m_PreviousPage = null;
            }

            //遷移する
            PanelMain.Transition = m_Transition[(int)trasition_type];
            PanelMain.Content = toUserControl;

            //ブラウザを表示する(遷移中に表示されているとアニメーションが正常に表示されない対策)
            if (PanelMain.Content == m_PageWebBrowser)
            {
                m_PageWebBrowser.Browser.Visibility = Visibility.Visible;
            }

            //戻るボタンの状態を設定する
            if (is_enable_back_button == true)
            {
                btnBack.Visibility = Visibility.Visible;
            }
            else
            {
                btnBack.Visibility = Visibility.Hidden;
            }
            btnBack.IsEnabled = is_enable_back_button;
        }
        #endregion

        #region プロパティ
        // イベント宣言
        private string DirectoryUser
        {
            get
            {
                var assm = System.Reflection.Assembly.GetEntryAssembly();
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + assm.GetName().Name + ROOT_DATA_FOLDER + "\\" + m_Account.Name;
            }
        }

        private string DirectoryClient
        {
            get
            {
                return DirectoryUser + CLIENT_DATA_FOLDER;
            }
        }

        private bool m_is_login = false;
        public bool is_login
        {
            get
            {
                return m_is_login;
            }

            set
            {
                m_is_login = value;
                OnPropertyChanged("is_login");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // イベントに対応するOnPropertyChanged メソッドを作る
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
#endregion

#region 設定の保存・読み込み
        private bool LoadSIPAccount(string fileName)
        {
            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(DataAccount));
                //読み込むファイルを開く
                System.IO.StreamReader sr = new System.IO.StreamReader(
                    fileName, new System.Text.UTF8Encoding(false));
                //XMLファイルから読み込み、逆シリアル化する
                m_Account = (DataAccount)serializer.Deserialize(sr);
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
                    new System.Xml.Serialization.XmlSerializer(typeof(DataAccount));
                //書き込むファイルを開く（UTF-8 BOM無し）
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, m_Account);
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

        private bool LoadSetting(string fileName)
        {
            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(DataSetting));
                //読み込むファイルを開く
                System.IO.StreamReader sr = new System.IO.StreamReader(
                    fileName, new System.Text.UTF8Encoding(false));
                //XMLファイルから読み込み、逆シリアル化する
                m_Setting = (DataSetting)serializer.Deserialize(sr);
                //ファイルを閉じる
                sr.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadSetting : Error : " + ex.Message);
                return false;
            }

            return true;
        }

        private bool SaveSetting(string fileName)
        {
            try
            {
                //XmlSerializerオブジェクトを作成
                //オブジェクトの型を指定する
                System.Xml.Serialization.XmlSerializer serializer =
                    new System.Xml.Serialization.XmlSerializer(typeof(DataSetting));
                //書き込むファイルを開く（UTF-8 BOM無し）
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, m_Setting);
                //ファイルを閉じる
                sw.Close();
            }

            catch (Exception ex)
            {
                Debug.WriteLine("SaveSetting Error = " + ex.Message);
                return false;
            }

            return true;
        }

#endregion

#region STUN処理
        /// <summary>
        /// STUNサーバーからNATの状態を取得する
        /// </summary>
        /// <returns></returns>
        private STUN_Result GetStunInformation(Socket socket,  string stun_server, int stun_port)
        {
            try
            {
                return STUN_Client.Query(stun_server, stun_port, socket);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainWindow::GetStunInformation Error = " + ex.Message);
                return null;
            }
        }

        private string[] GetIPAddress()
        {
            //string ipaddress = "";
            IPHostEntry ipentry = Dns.GetHostEntry(Dns.GetHostName());

            string[] ipaddress = new string[ipentry.AddressList.Count()];

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

#region UDP通信
        /// <summary>
        /// DataClientk構造体から通信先アドレスを取得する
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private ClientCommunicationData GetTargetAddress(STUN_Result stun_result, Socket socket, DataClient data)
        {
            string ip = "";
            int port = 0;

            ClientCommunicationData returndata = new ClientCommunicationData();

            string public_ip = "";
            int public_port = 0;

            if (socket == null) return null;

            IPEndPoint endpoint = (IPEndPoint)socket.LocalEndPoint;
            int local_port = endpoint.Port;

            if (stun_result != null && stun_result.PublicEndPoint != null)
            {
                public_ip = stun_result.PublicEndPoint.Address.ToString();
                public_port = stun_result.PublicEndPoint.Port;
            }

            if (data.global_ip != null && public_ip != data.global_ip && data.global_port != "")
            {
                ip = data.global_ip;
                port = int.Parse(data.global_port);

                returndata.source_ip = public_ip;
                returndata.source_port = local_port;
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

                    //1つめのIPを確認する
                    if (data.local_ip != null && data.local_ip != "" && data.local_port != "")
                    {
                        pos = data.local_ip.LastIndexOf('.');
                        target_net_address = data.local_ip.Substring(0, pos);

                        if (local_net_address == target_net_address)
                        {
                            ip = data.local_ip;
                            port = int.Parse(data.local_port);

                            returndata.source_ip = m_LocapIP[i];
                            returndata.source_port = local_port;
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

                            if (local_net_address == target_net_address)
                            {
                                ip = data.local_ip2;
                                port = int.Parse(data.local_port2);

                                returndata.source_ip = m_LocapIP[i];
                                returndata.source_port = local_port;
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

                            if (local_net_address == target_net_address)
                            {
                                ip = data.local_ip3;
                                port = int.Parse(data.local_port3);

                                returndata.source_ip = m_LocapIP[i];
                                returndata.source_port = local_port;
                            }
                        }
                    }
                    if (ip != "") break;
                }
            }

            //接続済みの場合は取得済みのアドレス/ポートを使う
            if (data.node_status == DataClient.NodeStatus.Connected)
            {
                ip = data.commnication_address;
                port = int.Parse(data.comminication_port);
            }

            //送信先のデータを格納する
            returndata.target_ip = ip;
            returndata.target_port = port;

            return returndata;
        }
#endregion

#region WebSocket ハンドラ
        private void OnMessageRequest(DataMessage data_message)
        {
            Debug.WriteLine("OnMessageRequest : from=" + data_message.From + ", message=" + data_message.message);
            DataAddressSet address_set = DynamicJson.Parse(data_message.message);

            //受信した情報を保存する
            DataClient client = UpdateClientInformation(data_message.From, address_set);

            //コネクション情報を作成する
            DataMessage data_message_answer = CreateConnectionMessage(m_StunResultTunnelServer, m_SocketTunnelServer, DataMessage.Command.Approval, data_message.From);

            //送信する
            m_JoinnetWeb.Send(DynamicJson.Serialize(data_message_answer));

            //リクエストを送信する
            ClientCommunicationData address = GetTargetAddress(m_StunResultTunnelServer, m_SocketTunnelServer, client);
            if (address != null)
                m_TunnelServer.SendConnectionRequest(address.target_ip, address.target_port);
        }

        private void OnessageApproval(DataMessage data_message)
        {
            Debug.WriteLine("OnessageApproval : from=" + data_message.From + ", message=" + data_message.message);
            m_CurrentConnectionAddrsss = DynamicJson.Parse(data_message.message);

            //受信した情報を保存する
            DataClient client = UpdateClientInformation(data_message.From, m_CurrentConnectionAddrsss);

            if (m_TuunelClient != null)
            {
                m_TuunelClient.Stop();
            }
            ClientCommunicationData address = GetTargetAddress(m_StunResultTunnelClient, m_SocketTunnelClient, client);
            if (address != null)
            {
                m_TuunelClient = new CTCPTunnelClient("127.0.0.1", 0, m_SocketTunnelClient, address.target_ip, address.target_port);
                var thread = new Thread(() =>
                {
                    m_TuunelClient.Run();
                });
                thread.Start();
                m_TuunelClient.SendConnectionRequest();
            }
        }

        private void OnMessageReject(DataMessage data_message)
        {

        }

        private void WS_MessageReceived(string received_message)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                DataMessage data_message = DynamicJson.Parse(received_message);
                data_message.fromID = data_message.ID;
                data_message.is_sent = true;

                
                if (m_PageContactList.Contact_IsExist(data_message.From) == false)
                {
                    string message = string.Format("登録されていないユーザー {0}さんからのメッセージ届きました。\n\n{1}\n\nコンタクトリストに追加しメッセージを受信しますか？", data_message.From, data_message.message);
                    if (MessageBox.Show(message, "問い合わせ", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        m_PageContactList.Contact_Add(data_message.From);
                        DataClient client = (DataClient)m_PageContactList.listClient.Items[m_PageContactList.listClient.Items.Count - 1];
                    }
                    else
                    {
                        //TODO:拒否のメッセージを返す
                        return;
                    }
                }

                    Debug.WriteLine(string.Format("WS_MessageReceived : Command={0}, message={1}", data_message.command, data_message.message));
                    switch (data_message.command)
                    {
                        case DataMessage.Command.Request:
                            //接続要求
                            OnMessageRequest(data_message);
                            break;
                        case DataMessage.Command.Approval:
                            //接続承認
                            OnessageApproval(data_message);
                            break;
                        case DataMessage.Command.Reject:
                            //接続許可
                            OnMessageReject(data_message);
                            break;
                    }
            }));
        }

        private bool WebSocketCallback(CJoinnetClient.EventID eventid, object obj)
        {
            switch (eventid)
            {
                case CJoinnetClient.EventID.message_recevied:
                    WS_MessageReceived((string)obj);
                    break;
            }

            return true;
        }
        #endregion

        #region タイマー
        void TimerUpdateConnectionList(object sender, EventArgs e)
        {
            List<TCPData> list = m_TunnelServer.GetCurrentConnection();

            Debug.WriteLine(string.Format("TimerUpdateConnectionList : item_count = {0}", list.Count));

            //切断されたデータを削除する
            for (int i = 0; i < m_PageConnectionList.listClient.Items.Count; ++i)
            {
                bool is_found = false;
                foreach (TCPData current_data in list)
                {
                    TCPData view_data = (TCPData)m_PageConnectionList.listClient.Items[i];
                    if (current_data.address == view_data.address)
                    {
                        is_found = true;
                        break;
                    }
                }
                if (is_found == false)
                {
                    m_PageConnectionList.listClient.Items.RemoveAt(i);
                    --i;
                }
            }

            //リストに無いものを追加する
            foreach (TCPData current_data in list)
            {
                Debug.WriteLine(string.Format("TimerUpdateConnectionList : Address = {0}", current_data.address.ToString()));

                bool is_found = false;
                for (int i = 0; i < m_PageConnectionList.listClient.Items.Count; ++i)
                {
                    TCPData view_data = (TCPData)m_PageConnectionList.listClient.Items[i];
                    if (current_data.address == view_data.address)
                    {
                        is_found = true;
                        break;
                    }
                }
                if (is_found == false)
                {
                    m_PageConnectionList.listClient.Items.Add(current_data);
                }
            }
        }
        #endregion

        #region ユーザーコントロール・ハンドラ
        private bool EventHandler_PageLogin(UCLogin.EventID eventid, object obj)
        {
            switch (eventid)
            {
                case UCLogin.EventID.login:
                    {
                        m_Account.Name = m_PageLogin.textUserID.Text;
                        m_Account.Password = m_PageLogin.textPassword.Password;

                        m_PageLogin.Enable(false);
                        m_PageLogin.textStatus.Text = "ログイン中・・・";

                        //ユーザーディレクトリを作成する
                        if(Directory.Exists(DirectoryUser) == false)
                        {
                            Debug.WriteLine("EventHandler_PageLogin : " + DirectoryUser);
                            Directory.CreateDirectory(DirectoryUser);
                        }

                        if (Directory.Exists(DirectoryClient) == false)
                        {
                            Directory.CreateDirectory(DirectoryClient);
                        }

                        //接続先データをロードする
                        m_PageContactList.LoadClientList(DirectoryUser + "\\clientsdata.xml");

                        //サーバーにログインする
                        if(m_JoinnetWeb.Login(m_PageLogin.textUserID.Text, m_PageLogin.textPassword.Password) == true)
                        {
                            //設定をロードする
                            if(LoadSetting(DirectoryUser + "\\setting.xml") == false)
                            {
                                m_Setting = new DataSetting();
                                m_Setting.document_root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\jointnet\\" + m_Account.Name;
                                if(Directory.Exists(m_Setting.document_root) == false)
                                {
                                    Directory.CreateDirectory(m_Setting.document_root);
                                }
                                m_Setting.is_auto_html = true;

                                m_Setting.language = Properties.Settings.Default.language;
                            }
                            m_PageUcSetting.DataContext = m_Setting;

                            //Webサーバーの設定を行う
                            m_WebServer.SetDocumentRoot(m_Setting.document_root);
                            m_WebServer.IsAutoIndexes = m_Setting.is_auto_html;

                            //トンネルの転送先を設定する
                            switch(m_Setting.TunnelMode)
                            {
                                case DataSetting.enumTunnelMode.OptionalFunction:
                                    m_TunnelServer.m_address = m_Setting.target_ip;
                                    m_TunnelServer.m_port = m_Setting.target_port;
                                    break;
                                case DataSetting.enumTunnelMode.WebServer:
                                default:
                                    m_TunnelServer.m_address = "127.0.0.1";
                                    m_TunnelServer.m_port = m_WebServer.Port;
                                    break;
                            }

                            //通信を始める
                            if (m_JoinnetWeb.OpenChat() == true)
                            {
                                //ログインフラグを設定する
                                is_login = true;

                                //ステータスを設定する
                                textTunnelPortNumber.Text = "未接続";

                                //コンタクトリストに遷移
                                this.Dispatcher.Invoke((Action)(() =>
                                {
                                    NavigateTo(m_PageContactList, TrasitionType.Trasition_SlideLeft, false);

                                }));
                            }
                        }

                        //ログインに失敗した場合の処理
                        if(is_login == false)
                        {
                            m_PageLogin.textStatus.Text = "ログインできませんでした。";
                            m_JoinnetWeb.Logout();
                            m_PageLogin.Enable(true);
                        }
                    }
                    break;

            }
            return true;
        }

        private bool EventHandler_ContactList(UCContactList.EventID eventid, object obj)
        {
            switch (eventid)
            {
                case UCContactList.EventID.logout:
                    break;
                case UCContactList.EventID.connect:
                    {
                        DataClient client = (DataClient)obj;

                        //通信用ソケットを作成する
                        if (m_SocketTunnelClient == null)
                        {
                            m_SocketTunnelClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            EndPoint bindEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            m_SocketTunnelClient.Bind(bindEndPoint);
                        }

                        //ポート情報を取得する
                        if(m_StunResultTunnelClient == null)
                            m_StunResultTunnelClient = GetStunInformation(m_SocketTunnelClient, m_StunServer, m_StunServerPort);

                        //コネクション情報を作成する
                        DataMessage data_message = CreateConnectionMessage(m_StunResultTunnelClient, m_SocketTunnelClient, DataMessage.Command.Request, client.user);

                        //送信する
                        m_JoinnetWeb.Send(DynamicJson.Serialize(data_message));

                        //接続中の表示を行う
                        m_PageContactList.ShowConnectingPanel(true);

                        //受信確認スレッドを実行する
                        var thread = new Thread(() =>
                        {
                            Debug.WriteLine(string.Format("ThreadWait Connection >>"));

                            bool is_success = false;
                            DateTime start_time = DateTime.Now;

                            while (true)
                            {
                                if (m_TuunelClient != null &&  m_TuunelClient.m_isEstablish == true)
                                {
                                    Debug.WriteLine(string.Format("ThreadWait Connection  : Established !!!"));

                                    //Webブラウザに遷移
                                    this.Dispatcher.Invoke((Action)(() =>
                                    {
                                        switch (m_CurrentConnectionAddrsss.tunnelmode)
                                        {
                                            case DataAddressSet.TunnelMode.WebServer:
                                                {
                                                    NavigateTo(m_PageWebBrowser, TrasitionType.Trasition_SlideRight, true);

                                                    string szUrl = "http://127.0.0.1:" + m_TuunelClient.Port.ToString() + "/";
                                                    m_PageWebBrowser.Browser.Navigate(szUrl);
                                                }
                                                break;
                                            case DataAddressSet.TunnelMode.OptionalFunction:
                                                {
                                                    NavigateTo(m_PageOptionalFunction, TrasitionType.Trasition_SlideRight, true);
                                                }
                                                break;
                                        }

                                        textTunnelPortNumber.Text = m_TuunelClient.Port.ToString();
                                    }));

                                    is_success = true;
                                    break;
                                }

                                TimeSpan ts = DateTime.Now.Subtract(start_time);
                                if (ts.TotalSeconds > 10) break;

                                Thread.Sleep(100);
                            }

                            //接続中の表示を停止する
                            this.Dispatcher.Invoke((Action)(() =>
                            {
                                m_PageContactList.ShowConnectingPanel(false);
                            }));

                            if (is_success == false)
                            {
                                MessageBox.Show(this, "接続できませんでした。");
                            }
                            Debug.WriteLine(string.Format("ThreadWait Connection <<"));
                        });
                        thread.Start();
                    }
                    break;
                case UCContactList.EventID.disconnect:
                    break;
                case UCContactList.EventID.delete:
                    break;
                case UCContactList.EventID.SelectionChanged:
                    break;
                case UCContactList.EventID.loaded:
                    break;
            }
            return true;
        }
#endregion

#region イベントハンドラ
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //実行ディレクトリを取得する
            m_ExcuteDirectory = UtilData.GetStatupDirectory();

            //動作中のPCのロカールIPを取得する
            m_LocapIP = UtilNET.GetIPAddress();

            //表示言語を設定する
            ResourceService.Current.ChangeCulture(Properties.Settings.Default.language);

            //SIP設定をロードする
            string account_file = UtilData.GetAppDataPath() + "\\account.xml";
            if (LoadSIPAccount(account_file) == true)
            {
                m_PageLogin.textUserID.Text = m_Account.Name;
            }
            m_Account.status = DataAccount.Status.None;

            //Webサーバーを作成する
            m_WebServer = new CWebServer(0);
            m_WebServer.listen(".\\");

            //サーバーポートが確定するまで待機する
            for (int retry = 0;retry < 100; ++retry)
            {
                if (m_WebServer.IsAlive == false) break;
                if (m_WebServer.Port != 0) break;
                Thread.Sleep(10);
            }
            Debug.WriteLine(string.Format("Window_Loaded::lithen Port = {0} ", m_WebServer.Port));

            //通信用インスタンス(ソケット)を作成する
            m_SocketTunnelServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EndPoint bindEndPoint = new IPEndPoint(IPAddress.Any, 0);
            m_SocketTunnelServer.Bind(bindEndPoint);

            //ポート情報を取得する
            m_StunResultTunnelServer = GetStunInformation(m_SocketTunnelServer, m_StunServer, m_StunServerPort);

            //通信用インスタンス(サーバー)を作成する
            m_TunnelServer = new CTCPTunnelServer(m_SocketTunnelServer);
            m_TunnelServer.m_address = "127.0.0.1";
            m_TunnelServer.m_port = m_WebServer.Port;
            var thread = new Thread(() =>
            {
                m_TunnelServer.Run();
            });
            thread.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //トンネルサーバーを停止する
            m_TunnelServer.Stop();

            //トンネルクライアントを停止する
            if(m_TuunelClient != null)
            {
                m_TuunelClient.Stop();
            }

            //Webサーバーを停止する
            m_WebServer.stop();

            //ログイン中の場合はデータを保存する
            if (PanelMain.Content != m_PageLogin)
            {
                //コンタクトのステータスをすべて切断にする
                m_PageContactList.Contact_Set_NodeStatus(DataClient.NodeStatus.None);

                //接続先データを保存する
                m_PageContactList.SaveClientList(DirectoryUser + "\\clientsdata.xml");
            }

            //設定を保存する
            Properties.Settings.Default.Save();

            //SIP設定を保存する
            string account_file = UtilData.GetAppDataPath() + "\\account.xml";
            SaveSIPAccount(account_file);
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            //コンタクトリストを保存する
            m_PageContactList.SaveClientList(DirectoryUser + "\\clientsdata.xml");

            //設定をロードする
            SaveSetting(DirectoryUser + "\\setting.xml");

            //ログアウトする
            m_JoinnetWeb.Logout();

            //ログインフラグを設定する
            is_login = false;

            m_PageLogin.Enable(true);
            m_PageLogin.textStatus.Text = "";

            //ログイン画面に遷移
            NavigateTo(m_PageLogin, TrasitionType.Trasition_SlideRight, false);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            WndAddContact dlg = new WndAddContact();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                if (m_PageContactList.Contact_Add(dlg.textUserID.Text) == false)
                {
                    MessageBox.Show("指定されたユーザーは追加済みです。");
                    return;
                }
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if(PanelMain.Content == m_PageWebBrowser || PanelMain.Content == m_PageOptionalFunction)
            {
                //コンタクトリストに遷移
                NavigateTo(m_PageContactList, TrasitionType.Trasition_SlideLeft, m_PreviousPage.is_enable_back_button);
            }
            else if (PanelMain.Content == m_PageConnectionList ||
                        PanelMain.Content == m_PageUcSetting ||
                        PanelMain.Content == m_PageAbout)
            {
                    //前に表示されていたページに遷移
                    NavigateTo(m_PreviousPage, TrasitionType.Trasition_SlideRight, m_PreviousPage.is_enable_back_button);
            }
            else
            {
                //コンタクトリストに遷移
                NavigateTo(m_PageContactList, TrasitionType.Trasition_Fade, m_PreviousPage.is_enable_back_button);
            }

            //メニューを非選択にする
            listMenu.SelectedIndex = -1;
        }

        private TrasitionType GetTrasitionForMenuItem()
        {
            if (PanelMain.Content == m_PageConnectionList ||
                PanelMain.Content == m_PageUcSetting ||
                PanelMain.Content == m_PageAbout)
            {
                return TrasitionType.Trasition_Fade;
            }
            return TrasitionType.Trasition_SlideLeft;
        }

        private void menu_setting_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (is_login == true)
            {
                //設定に遷移
                NavigateTo(m_PageUcSetting, GetTrasitionForMenuItem(), true);
            }
        }

        private void menu_server_status_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (is_login == true)
            {
                //接続リストに遷移
                NavigateTo(m_PageConnectionList, GetTrasitionForMenuItem(), true);

                //表示タイマーを開始する
                m_TimerUpdateConnectionList.Start();
            }
        }

        private void menu_About_MouseDown(object sender, RoutedEventArgs e)
        {
            NavigateTo(m_PageAbout, GetTrasitionForMenuItem(), true);
        }

        private void content_menu_MouseLeave(object sender, MouseEventArgs e)
        {
            btnMenu.IsChecked = false;
        }

        #endregion
    }
}
