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
    /// UCSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class UCSetting : CBasePage
    {
        public UCSetting()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.Description = "出力フォルダを選択してください。";
            fbd.RootFolder = Environment.SpecialFolder.Desktop;
            fbd.SelectedPath = documen_root.Text;
            fbd.ShowNewFolderButton = true;

            //ダイアログを表示する
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //選択されたフォルダを表示する
                documen_root.Text = fbd.SelectedPath;
            }
        }

        private void cmbLanguege_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                DataSetting setting = (DataSetting)this.DataContext;
                Properties.Settings.Default.language = setting.language;
                ResourceService.Current.ChangeCulture(setting.language);
            }

            catch (Exception ex)
            {

            }
        }
    }
}
