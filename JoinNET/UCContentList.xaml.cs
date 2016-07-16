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
using System.Xml.Serialization;
using System.Threading;
using System.Windows.Threading;

namespace JoinNET
{
    /// <summary>
    /// UCContentList.xaml の相互作用ロジック
    /// </summary>
    public partial class UCContentList : UserControl
    {
        //スレッド
        Thread m_ThreadLoadSharedData;
        bool m_isTopThreadLoadSharedData;

        //実行ディレクトリ
        public string m_ExcuteDirectory;

        //アプリケーション全体の終了フラグ
        public bool m_isTerminateApplication = false;

        public UCContentList()
        {
            InitializeComponent();
        }

        //自分の共有データを読み込む
        public void LoadMyShareData(string file)
        {
            var thread = new Thread(() =>
            {
                //string file = UtilData.GetAppDataPath() + "\\sharedata.xml";
                ArrayList ar = UtilData.LoadArrayData(file, new Type[] { typeof(DataShare) });
                if (ar != null)
                {
                    foreach (DataShare data in ar)
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

        //自分の共有データを保存する
        public void SaveMyShareData(string file)
        {
            //共有データを保存する
            ArrayList ar = new ArrayList();
            foreach (DataShare data in listShareItem.Items)
            {
                ar.Add(data);
            }
            UtilData.SaveArrayData(file, ar, new Type[] { typeof(DataShare) });
        }

        //接続先の共有データを読み込む
        public void LoadSharedData(string file)
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
                //string file = UtilData.GetAppDataPath() + "\\" + m_ConnectedClient.user + "_file.xml";
                ArrayList ar = UtilData.LoadArrayData(file, new Type[] { typeof(SharedData) });
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

#if false
                //ファイルリスト送信要求を行う
                if (m_ConnectedClient.node_status == DataClient.NodeStatus.Connected)
                {
                    //RequestFileList(m_ConnectedClient);
                }
#endif
                Debug.WriteLine("LoadSharedData::Thread <<");
            });
            m_ThreadLoadSharedData.Start();
            Debug.WriteLine("LoadSharedData <<");

        }

        //接続先の共有データを保存する
        public bool SaveCurrentSharedData(string file)
        {
            try
            {
                //データを保存する
                ArrayList arTmp = new ArrayList();
                foreach (SharedData shareddata in listConnectedShare.Items)
                {
                    arTmp.Add(shareddata);
                }
                //string file = UtilData.GetAppDataPath() + "\\" + m_ConnectedClient.user + "_file.xml";
                UtilData.SaveArrayData(file, arTmp, new Type[] { typeof(SharedData) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveCurrentSharedData : Error : " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定したItemCollectionにShareDataがあるかを確認する
        /// </summary>
        /// <param name="data"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        public bool IsExistData(DataShare data, ItemCollection list)
        {
            //データがリストにあるかを確認する
            bool isExist = false;
            foreach (DataShare current in list)
            {
                if (data.guid == current.guid)
                {
                    isExist = true;
                    break;
                }
            }
            return isExist;
        }

        private void listShareItem_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
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
                    foreach (DataShare item in listShareItem.Items)
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
                    DataShare data = new DataShare();

                    data.name = s;
                    data.guid = System.Guid.NewGuid().ToString();
                    data.length = fileinfo.Length;
                    data.datatype = DataShare.DATA_TYPE.File;

                    //データを登録する
                    listShareItem.Items.Add(data);
                }
            }
            Debug.WriteLine("listShareItem_Drop <<");
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            switch (tabList.SelectedIndex)
            {
                case 0://共有リスト
                    {
                        DataShare dat = ((Button)sender).DataContext as DataShare;
                        listShareItem.Items.Remove(dat);
                    }
                    break;
                case 1://接続先の共有リスト
                    {
                        SharedData dat = ((Button)sender).DataContext as SharedData;
                        dat.is_delete = true;
                        //SaveCurrentSharedData();
                    }
                    break;
            }
        }
    }
}
