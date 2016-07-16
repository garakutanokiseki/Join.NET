using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace JoinNET
{
    public class DataSetting : INotifyPropertyChanged
    {
        public enum enumTunnelMode
        {
            WebServer = 1,      //Internal Web Server
            OptionalFunction,   //Optional IP address and port
        }

        private enumTunnelMode _TunnelMode = enumTunnelMode.WebServer;
        public enumTunnelMode TunnelMode
        {
            get
            {
                return _TunnelMode;
            }
            set
            {
                _TunnelMode = value;
            }
        }

        private string _document_root = "";
        public string document_root
        {
            get
            {
                return _document_root;
            }
            set
            {
                _document_root = value;
            }
        }

        private bool _is_auto_html = true;
        public bool is_auto_html
        {
            get
            {
                return _is_auto_html;
            }
            set
            {
                _is_auto_html = value;
            }
        }

        private string _target_ip = "127.0.0.1";
        public string target_ip
        {
            get
            {
                return _target_ip;
            }
            set
            {
                _target_ip = value;
            }
        }

        private int _target_port = 0;
        public int target_port
        {
            get
            {
                return _target_port;
            }
            set
            {
                _target_port = value;
            }
        }

        private string _language = "";
        public string language
        {
            get
            {
                return _language;
            }
            set
            {
                _language = value;
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
