using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;

namespace JoinNET
{
    //共有するデータを示すクラス
    public class DataShare : INotifyPropertyChanged
    {
        public enum DATA_TYPE
        {
            Directory = 1,
            File,
        }

        /// <summary>
        /// このデータを示すGUID
        /// </summary>
        private string _guid;
        public string guid
        {
            get
            {
                return _guid;
            }
            set
            {
                _guid = value;
                OnPropertyChanged("guid");
            }
        }

        /// <summary>
        /// ファイル/フォルダ名
        /// </summary>
        private string _name;
        public string name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                OnPropertyChanged("name");
            }
        }

        /// <summary>
        /// ファイルサイズ
        /// </summary>
        private long _length;
        public long length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
                OnPropertyChanged("length");
            }
        }

        /// <summary>
        /// データタイプ
        /// </summary>
        private DATA_TYPE _datatype;
        public DATA_TYPE datatype
        {
            get
            {
                return _datatype;
            }
            set
            {
                _datatype = value;
                OnPropertyChanged("datatype");
            }
        }

        // イベント宣言
        public event PropertyChangedEventHandler PropertyChanged;

        // イベントに対応するOnPropertyChanged メソッドを作る
        protected void OnPropertyChanged(string name)
        {
            try
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(name));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ShareData Error => " + ex.Message);
            }
        }
    }

    //共有されたデータを示すクラス
    public class SharedData : DataShare
    {
        /// <summary>
        /// 保存されたファイルパス
        /// </summary>
        private string _filepath = "";
        public string filepath
        {
            get
            {
                return _filepath;
            }
            set
            {
                _filepath = value;
                OnPropertyChanged("filepath");
            }
        }

        /// <summary>
        /// ダウンロード率
        /// </summary>
        private double _downloadedrate = 0;
        public double downloadedrate
        {
            get
            {
                return _downloadedrate;
            }
            set
            {
                _downloadedrate = value;
                OnPropertyChanged("downloadedrate");
            }
        }

        /// <summary>
        /// 削除フラグ
        /// </summary>
        private bool _is_delete = false;
        public bool is_delete
        {
            get
            {
                return _is_delete;
            }
            set
            {
                _is_delete = value;
                OnPropertyChanged("is_delete");
            }
        }

    }
}
