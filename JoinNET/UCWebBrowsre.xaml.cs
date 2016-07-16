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
using System.Reflection;

namespace JoinNET
{
    /// <summary>
    /// UCWebBrowsre.xaml の相互作用ロジック
    /// </summary>
    public partial class UCWebBrowsre : CBasePage
    {
        public UCWebBrowsre()
        {
            InitializeComponent();

            var axIWebBrowser2 = typeof(WebBrowser).GetProperty("AxIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            var comObj = axIWebBrowser2.GetValue(Browser, null);

            // JavaScriptのエラー表示を抑止する
            comObj.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, comObj, new object[] { true });
        }

        private void Browser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if(e.Uri != null)
                textUrl.Text = e.Uri.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Browser.Navigate(new Uri(textUrl.Text));
        }
    }
}
