using System;
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

namespace JoinNET
{
    /// <summary>
    /// UCConnectionList.xaml の相互作用ロジック
    /// </summary>
    public partial class UCConnectionList : CBasePage
    {
        //イベントID
        public enum EventID
        {
        }

        //イベント処理用
        public Func<EventID, Object, bool> m_EventHndler = null;

        public UCConnectionList()
        {
            InitializeComponent();
        }
    }
}
