using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JoinNET
{
    abstract class DBBase<Type>
    {
        /// <summary>
        /// データーベースを開く
        /// </summary>
        /// <param name="szFile"></param>
        /// <returns></returns>
        public abstract bool Open(string szFile);

        /// <summary>
        /// データベースを閉じる
        /// </summary>
        /// <returns></returns>
        public abstract bool Close();

        /// <summary>
        /// データを追加する
        /// </summary>
        /// <param name="dat"></param>
        /// <returns></returns>
        public abstract int Add(Type dat);

        /// <summary>
        /// 指定したデータを削除する
        /// </summary>
        /// <param name="nID"></param>
        /// <returns></returns>
        public abstract bool Delete(int nID);

        /// <summary>
        /// 指定したデータを取得する
        /// </summary>
        /// <param name="nIdx"></param>
        /// <returns></returns>
        public abstract Type Get(int nIdx);

        /// <summary>
        /// データの個数を得る
        /// </summary>
        /// <returns></returns>
        public abstract int Count();

        /// <summary>
        /// データを更新する
        /// </summary>
        /// <param name="dat"></param>
        /// <returns></returns>
        public abstract bool Update(Type dat);

        public int GetIntFromSQL(System.Data.Common.DbDataReader reader, string name)
        {
            try
            {
                return reader.GetInt32(reader.GetOrdinal(name));
            }
            catch
            {
            }

            return 0;
        }

        public long GetLongFromSQL(System.Data.Common.DbDataReader reader, string name)
        {
            try
            {
                return reader.GetInt64((reader.GetOrdinal(name)));
            }
            catch
            {
            }

            return 0;
        }

        public string GetStringFromSQL(System.Data.Common.DbDataReader reader, string name)
        {
            try
            {
                return reader.GetString(reader.GetOrdinal(name));
            }
            catch
            {
            }

            return "";
        }
    }
}
