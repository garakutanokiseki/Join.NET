using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Community.CsharpSqlite.SQLiteClient;
using System.Diagnostics;

namespace JoinNET
{
    class DBMessage : DBBase<DataMessage>
    {
        private SqliteConnection m_Sql = new SqliteConnection();
        private System.Data.Common.DbCommand m_SqlCmd;

        public override bool Open(string szFile)
        {
            int nRet;

            Debug.WriteLine("DBMessage::Open szFile = " + szFile);

            //データベースを開く
            m_Sql.ConnectionString = string.Format("Version=3,uri=file:{0}", szFile);
            m_Sql.Open();

            //テーブルがあるかを確認する
            m_SqlCmd = m_Sql.CreateCommand();

            //テーブルの有無を確認して必要に応じてテーブルを作成する
            try
            {
                m_SqlCmd.CommandText = "SELECT count(*) FROM data";

                //コマンドを実行する
                nRet = m_SqlCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DBMessage::Open Error => " + ex.Message);

                //データベースが開けない場合はエラー
                if (ex.Message.Contains("no such table") == false) return false;
                //テーブルを作成する
                m_SqlCmd.CommandText = "CREATE TABLE data(ID INTEGER PRIMARY KEY, fromID INTEGER, message TEXT, date INTEGER, _from TEXT, _to TEXT, is_sent INTEGER)";
                nRet = m_SqlCmd.ExecuteNonQuery();
            }

            return true;
        }

        public override bool Close()
        {
            if (m_Sql == null) return true;
            m_Sql.Close();
            return true;
        }

        public override int Add(DataMessage dat)
        {
            int nRet;

            //データを追加する
            try
            {
                int is_send = dat.is_sent ? 1 : 0;
                string szStr = "INSERT INTO data(ID, fromID, message, date, _from, _to, is_sent) VALUES(NULL, " +dat.fromID.ToString() + ",\"" + dat.message + "\" , " + dat.date.ToFileTimeUtc().ToString() + " , '" + dat.From + "' , '" + dat.To + "', " + is_send.ToString() +  ")";
                Debug.WriteLine("DBMessage::Add : " + szStr);
                m_SqlCmd.CommandText = szStr;
                m_SqlCmd.CommandType = System.Data.CommandType.Text;
                nRet = m_SqlCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DBMessage::Add : Error => " + ex.Message);
                if (ex.Message.IndexOf("unrecognized token") >= 0) return -2;
                return -1;
            }

            return m_Sql.LastInsertRowId;
        }

        public override bool Delete(int nID)
        {
            //データを削除する
            int nRet;
            try
            {
                m_SqlCmd.CommandText = "DELETE FROM data WHERE ID=" + nID.ToString();
                nRet = m_SqlCmd.ExecuteNonQuery();
            }
            catch 
            {
                return false;
            }

            if (nRet == 0) return false;
            return true;
        }

        public bool DeleteFromData(int fromID, string from)
        {
            //データを削除する
            int nRet;
            try
            {
                m_SqlCmd.CommandText = string.Format("DELETE FROM data WHERE fromID={0} and _from='{1}'", fromID, from);
                nRet = m_SqlCmd.ExecuteNonQuery();
            }
            catch 
            {
                return false;
            }

            if (nRet == 0) return false;
            return true;
        }

        private DataMessage GetData(System.Data.Common.DbDataReader reader)
        {
            DataMessage dat = new DataMessage();

            dat.ID = GetIntFromSQL(reader, "ID");
            dat.message = GetStringFromSQL(reader, "message");
            dat.From = GetStringFromSQL(reader, "_from");
            dat.fromID = GetIntFromSQL(reader, "fromID");
            dat.To = GetStringFromSQL(reader, "_to");
            dat.date = DateTime.FromFileTime( GetLongFromSQL(reader, "date") );
            if (GetIntFromSQL(reader, "is_sent") == 1)
                dat.is_sent = true;
            else
                dat.is_sent = false;

            return dat;
        }

        public override DataMessage Get(int nIdx)
        {
            m_SqlCmd.CommandText = "SELECT * FROM data limit 1 offset " + nIdx.ToString();

            //コマンドを実行する
            System.Data.Common.DbDataReader reader = m_SqlCmd.ExecuteReader();

            if (reader.Read() == false) return null;

            return GetData(reader);
        }

        public override int Count()
        {
            int nRet = 0;
            //テーブルの有無を確認して必要に応じてテーブルを作成する
            try
            {
                m_SqlCmd.CommandText = "SELECT count(*) FROM data";

                //コマンドを実行する
                //nRet = m_SqlCmd.ExecuteNonQuery();
                nRet = int.Parse(m_SqlCmd.ExecuteScalar().ToString());
            }
            catch 
            {
            }

            return nRet;
        }

        public override bool Update(DataMessage dat)
        {
            try
            {

                int is_send = dat.is_sent ? 1 : 0;
                m_SqlCmd.CommandText = string.Format("UPDATE data SET date='{0}', message='{1}', is_sent={3} WHERE ID='{2}'",
                    dat.date.ToFileTimeUtc(), dat.message, dat.ID, is_send);

                //コマンドを実行する
                m_SqlCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DBMessage::Update : Error => " + ex.Message);
                return false;
            }

            return true;
        }

        //受信したデータを更新する
        public bool UpdateFromData(DataMessage dat)
        {
            try
            {

                int is_send = dat.is_sent ? 1 : 0;
                m_SqlCmd.CommandText = string.Format("UPDATE data SET message='{0}' WHERE fromID={1} and _from='{2}'",
                    dat.message, dat.fromID, dat.From);

                //コマンドを実行する
                m_SqlCmd.ExecuteNonQuery();
            }
            catch 
            {
                return false;
            }

            return true;
        }
    }
}
