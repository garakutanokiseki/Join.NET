using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace JoinNET
{
    public class DataMessage : INotifyPropertyChanged
    {
        public enum Command
        {
            None = 0,           //未定義

            Request = 1,        //接続要求
            Approval,           //承諾
            Reject,             //拒否
        }

        /// <summary>
        /// データのID
        /// </summary>
        private int _ID = 0;
        public int ID
        {
            get
            {
                return _ID;
            }
            set
            {
                _ID = value;
            }
        }

        /// <summary>
        /// メッセージID 0:自分のデータ、それ以外：自分以外が作成したデータ
        /// </summary>
        private int _fromID = 0;
        public int fromID
        {
            get
            {
                return _fromID;
            }
            set
            {
                _fromID = value;
            }
        }

        /// <summary>
        /// このデータが最後に行った操作
        /// </summary>
        private Command _command = Command.None;
        public Command command
        {
            get
            {
                return _command;
            }
            set
            {
                _command = value;
            }
        }

        /// <summary>
        /// このデータのメッセージ
        /// </summary>
        private string _message = "";
        public string message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged("message");
            }
        }

        /// <summary>
        /// このデータが発信された時刻
        /// </summary>
        private DateTime _date = DateTime.Now;
        public DateTime date
        {
            get
            {
                return _date;
            }
            set
            {
                _date = value;
                OnPropertyChanged("date");
            }
        }

        /// <summary>
        /// このデータの送信先
        /// </summary>
        private string _to = "";
        public string To
        {
            get
            {
                return _to;
            }
            set
            {
                _to = value;
            }
        }

        /// <summary>
        /// このデータの送信元
        /// </summary>
        private string _from = "";
        public string From
        {
            get
            {
                return _from;
            }
            set
            {
                _from = value;
                OnPropertyChanged("from");
            }
        }

        /// <summary>
        /// このデータが受信されたか(trur:受信済み、false:未受信)
        /// </summary>
        private bool _is_sent = false;
        public bool is_sent
        {
            get
            {
                return _is_sent;
            }
            set
            {
                _is_sent = value;
                OnPropertyChanged("is_sent");
            }
        }

        /// <summary>
        /// このデータを送信したときのcallid
        /// </summary>
        private string _callid = "";
        public string callid
        {
            get
            {
                return _callid;
            }
            set
            {
                _callid = value;
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
