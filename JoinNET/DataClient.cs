using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace JoinNET
{
    public class DataClient : INotifyPropertyChanged
    {

        private string _ID = "";
        public string ID
        {
            get
            {
                return _ID;
            }
            set
            {
                _ID = value;
                OnPropertyChanged("ID");
            }
        }

        private string _user = "";
        public string user
        {
            get
            {
                return _user;
            }
            set
            {
                _user = value;
                OnPropertyChanged("user");
            }
        }

        private string _global_ip = "";
        public string global_ip
        {
            get
            {
                return _global_ip;
            }
            set
            {
                _global_ip = value;
                OnPropertyChanged("global_ip");
            }
        }

        private string _global_port = "";
        public string global_port
        {
            get
            {
                return _global_port;
            }
            set
            {
                _global_port = value;
                OnPropertyChanged("global_port");
            }
        }

        private string _local_ip = "";
        public string local_ip
        {
            get
            {
                return _local_ip;
            }
            set
            {
                _local_ip = value;
                OnPropertyChanged("local_ip");
            }
        }

        private string _local_port = "";
        public string local_port
        {
            get
            {
                return _local_port;
            }
            set
            {
                _local_port = value;
                OnPropertyChanged("local_port");
            }
        }

        private string _local_ip2 = "";
        public string local_ip2
        {
            get
            {
                return _local_ip2;
            }
            set
            {
                _local_ip2 = value;
                OnPropertyChanged("local_ip2");
            }
        }

        private string _local_port2 = "";
        public string local_port2
        {
            get
            {
                return _local_port2;
            }
            set
            {
                _local_port2 = value;
                OnPropertyChanged("local_port2");
            }
        }

        private string _local_ip3 = "";
        public string local_ip3
        {
            get
            {
                return _local_ip3;
            }
            set
            {
                _local_ip3 = value;
                OnPropertyChanged("local_ip3");
            }
        }

        private string _local_port3 = "";
        public string local_port3
        {
            get
            {
                return _local_port3;
            }
            set
            {
                _local_port3 = value;
                OnPropertyChanged("local_port3");
            }
        }

        private string _status = "";
        public string status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                OnPropertyChanged("status");
            }
        }

        //ノードの状態
        public enum NodeStatus
        {
            None,                       //未定義
            RequestConnect,             //接続要求を送信中
            Connected,                  //接続済み(ACKを受信済み)
        }

        //現在の接続状態
        private NodeStatus _node_status = NodeStatus.None;
        public NodeStatus node_status
        {
            get
            {
                return _node_status;
            }
            set
            {
                _node_status = value;
                OnPropertyChanged("node_status");
            }
        }

        //接続要求シーケンス番号
        public uint connection_request_sequenceNumber = 0;

        //通信可能なIPアドレス
        private string _commnication_address;
        public string commnication_address
        {
            get
            {
                return _commnication_address;
            }
            set
            {
                _commnication_address = value;
                OnPropertyChanged("commnication_address");
            }
        }

        //通信可能なポート番号
        private string _comminication_port;
        public string comminication_port
        {
            get
            {
                return _comminication_port;
            }
            set
            {
                _comminication_port = value;
                OnPropertyChanged("comminication_port");
            }
        }

        // イベント宣言
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
    }
}
