using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace JoinNET
{
    public class UtilData
    {
        //実行ディレクトリ
        static public string m_ExcuteDirectory;

        /// <summary>
        /// 実行ディレクトリを取得する(Windowロード時に実行する必要あり)
        /// </summary>
        /// <returns>実行ディレクトリパス</returns>
        static public string GetStatupDirectory()
        {
            string exePath = Environment.GetCommandLineArgs()[0];
            string exeFullPath = System.IO.Path.GetFullPath(exePath);
            return System.IO.Path.GetDirectoryName(exeFullPath);
        }

        static public string GetAppDataPath()
        {
            if (m_ExcuteDirectory == null)
            {
                m_ExcuteDirectory = GetStatupDirectory();
            }
            var assm = System.Reflection.Assembly.GetEntryAssembly();
            string szDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + assm.GetName().Name;
            if (Directory.Exists(szDir) == false)
            {
                Directory.CreateDirectory(szDir);
            }

            return szDir;
        }

        static public ArrayList LoadArrayData(string szFile, Type[] type)
        {
            //XmlSerializerオブジェクトを作成
            var se = new XmlSerializer(typeof(ArrayList), type);
            ArrayList ar;

            try
            {
                using (var fs = new FileStream(szFile, FileMode.Open))
                {
                    ar = (ArrayList)se.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadShareData : Error : " + ex.Message);
                return null;
            }

            return ar;
        }

        static public bool SaveArrayData(string szFile, ArrayList collection, Type[] type)
        {
            try
            {
                //XMLファイルを出力する
                var se = new XmlSerializer(typeof(ArrayList), type);
                using (var fs = new FileStream(szFile, FileMode.Create))
                {
                    se.Serialize(fs, collection);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveArrayData : Error : " + ex.Message);
                return false;
            }
            return true;
        }
    }
}
