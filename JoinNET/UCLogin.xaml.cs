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
using System.Diagnostics;
using System.IO;

namespace JoinNET
{
    /// <summary>
    /// UCLogin.xaml の相互作用ロジック
    /// </summary>
    public partial class UCLogin : CBasePage
    {
        //イベントID
        public enum EventID
        {
            login = 0,
        }

        //イベント処理用
        public Func<EventID, Object, bool> m_EventHndler = null;

        public UCLogin()
        {
            InitializeComponent();
        }

        public void Enable(bool is_enable)
        {
            btnLogin.IsEnabled = is_enable;
            textPassword.IsEnabled = is_enable;
            textUserID.IsEnabled = is_enable;
        }

        private void hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (e.Uri != null && string.IsNullOrEmpty(e.Uri.OriginalString) == false)
            {
                string uri = e.Uri.AbsoluteUri;
                Process.Start(new ProcessStartInfo(uri));

                e.Handled = true;
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if(m_EventHndler != null)
            {
                m_EventHndler(EventID.login, null);
            }
        }
    }
}
