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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Threading;
using System.Windows.Media.Animation;

namespace JoinNET
{
    /// <summary>
    /// UCContactList.xaml の相互作用ロジック
    /// </summary>
    public partial class UCContactList : CBasePage
    {
        //イベントID
        public enum EventID
        {
            connect = 0,
            disconnect,
            logout,
            SelectionChanged,
            loaded,
            delete,
        }

        //イベント処理用
        public Func<EventID, Object, bool> m_EventHndler = null;

        //アプリケーション全体の終了フラグ
        bool m_isTerminateApplication = false;

        /// <summary>
        /// endpointからノードを取得する
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public DataClient GetClientDataFromEndpoint(IPEndPoint endpoint)
        {
            string address = endpoint.Address.ToString();
            string port = endpoint.Port.ToString();

            foreach (DataClient data in listClient.Items)
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

        #region 設定の保存・読み込み

        public void LoadClientList(string file)
        {
            listClient.Items.Clear();

            var thread = new Thread(() =>
            {
                //string file = UtilData.GetAppDataPath() + "\\clientsdata.xml";
                ArrayList ar = UtilData.LoadArrayData(file, new Type[] { typeof(DataClient) });
                if (ar != null)
                {
                    foreach (DataClient data in ar)
                    {
                        if (m_isTerminateApplication == true) break;

                        this.Dispatcher.Invoke((Action)(() =>
                        {
                            listClient.Items.Add(data);
                        }));
                        Thread.Sleep(5);
                    }
                    m_EventHndler(EventID.loaded, null);

                }
            });
            thread.Start();
        }

        public bool SaveClientList(string file)
        {
            try
            {
                //データを保存する
                ArrayList arTmp = new ArrayList();
                foreach (DataClient shareddata in listClient.Items)
                {
                    arTmp.Add(shareddata);
                }
                //string file = UtilData.GetAppDataPath() + "\\clientsdata.xml";
                UtilData.SaveArrayData(file, arTmp, new Type[] { typeof(DataClient) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveClientList : Error : " + ex.Message);
                return false;
            }

            return true;
        }
        #endregion

        public bool Contact_Add(string user)
        {
            if (Contact_IsExist(user) == true) return false;

            DataClient tmp = new DataClient();
            tmp.user = user;
            listClient.Items.Add(tmp);

            return true;
        }

        public bool Contact_IsExist(string user)
        {
            foreach (DataClient client in listClient.Items)
            {
                if (client.user == user)
                {
                    return true;
                }
            }

            return false;
        }

        public void Contact_Set_NodeStatus(DataClient.NodeStatus status)
        {
            foreach (DataClient data in listClient.Items)
            {
                data.node_status = status;
            }
        }

        public UCContactList()
        {
            InitializeComponent();
        }

        public void ShowConnectingPanel(bool is_show)
        {
            if(is_show == true)
            {
                PanelConnecting.Visibility = Visibility.Visible;
                imageLoading.BeginStoryboard((Storyboard)FindResource("Storyboard_LoadingAnimation"));
            }
            else
            {
                PanelConnecting.Visibility = Visibility.Collapsed;
                Storyboard story = (Storyboard)FindResource("Storyboard_LoadingAnimation");
                story.Stop();
            }
        }

        private void listClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //LoadSharedData();
            m_EventHndler(EventID.SelectionChanged, listClient.SelectedItem);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            //DataClient client = (DataClient)listClient.SelectedItem;
            DataClient client = (DataClient)((Button)sender).DataContext;
            if (client == null) return;

            if(m_EventHndler != null)
            {
                m_EventHndler(EventID.delete, client);
            }

            listClient.Items.Remove(client);
        }

        private void listClient_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataClient client = (DataClient)((ListBoxItem)sender).DataContext;
            if (client == null) return;

            if (m_EventHndler != null)
            {
                m_EventHndler(EventID.connect, client);
            }
        }
    }
}
