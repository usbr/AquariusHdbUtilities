using System;
using System.Net;
using System.Data;
using System.Linq;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;

namespace HDB2AQDB
{
    class HdbWrite
    {
        // Search for [JR] tag to find areas that could use some work
        private static bool jrDebug = false;

        private static string dbServer;
        private static string dbUser = Program.hdbUserWriter;
        public static string dbPass = Program.hdbPswdWriter;
        
        static decimal s_AGEN_ID = 7;//Bureau of Reclamation
        static decimal s_COLLECTION_SYSTEM_ID = 13;//See loading application
        static decimal s_LOADING_APPLICATION_ID = 120;//HDB-Aquarius Data Loader
        static decimal s_METHOD_ID = 13;//N/A
        static decimal s_COMPUTATION_ID = 1;//unknown


        /// <summary>
        /// Connects to HDB
        /// </summary>
        /// <returns></returns>
        private static OracleConnection ConnectHDB()
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////
            // Open Oracle DB connections
            if (jrDebug)
            { Console.Write("Connecting to HDB... "); }
            OracleConnection dbConx = new OracleConnection();
            dbConx.ConnectionString = "Data Source=" + dbServer + ";User Id=" + dbUser + ";Password=" + dbPass + ";";
            dbConx.Open();
            if (jrDebug)
            { Console.WriteLine("Success!"); }
            return dbConx;
        }


        /// <summary>
        /// Disconnects HDB
        /// </summary>
        /// <param name="conx"></param>
        private static void DisconnectHDB(OracleConnection conx)
        { conx.Dispose(); }


        public static void PutHdbData(string hdb, int sdi, string interval, DateTime tStart, double value)
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Connect to HDB Server
            dbServer = hdb;
            var oDB = ConnectHDB();
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Write ODB data
            OracleCommand cmd = new OracleCommand("MODIFY_R_BASE_RAW", oDB);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("SITE_DATATYPE_ID", OracleDbType.Int32).Value = sdi;
            cmd.Parameters.Add("INTERVAL", OracleDbType.Varchar2).Value = interval;
            cmd.Parameters.Add("START_DATE_TIME", OracleDbType.Date).Value = tStart;
            cmd.Parameters.Add("END_DATE_TIME", OracleDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("VALUE", OracleDbType.Decimal).Value = value;
            cmd.Parameters.Add("AGEN_ID", OracleDbType.Int32).Value = s_AGEN_ID;
            cmd.Parameters.Add("OVERWRITE_FLAG", OracleDbType.Varchar2).Value = "O";
            cmd.Parameters.Add("VALIDATION", OracleDbType.Varchar2).Value = "Z";
            cmd.Parameters.Add("COLLECTION_SYSTEM_ID", OracleDbType.Varchar2).Value = s_COLLECTION_SYSTEM_ID;
            cmd.Parameters.Add("LOADING_APPLICATION_ID", OracleDbType.Varchar2).Value = s_LOADING_APPLICATION_ID;
            cmd.Parameters.Add("METHOD_ID", OracleDbType.Varchar2).Value = s_METHOD_ID;
            cmd.Parameters.Add("COMPUTATION_ID", OracleDbType.Varchar2).Value = s_COMPUTATION_ID;
            cmd.Parameters.Add("DO_UPDATE_Y_OR_N", OracleDbType.Varchar2).Value = "N";
            cmd.Parameters.Add("DATA_FLAGS", OracleDbType.Varchar2).Value = DBNull.Value;
            cmd.Parameters.Add("TIME_ZONE", OracleDbType.Varchar2).Value = DBNull.Value;

            cmd.ExecuteNonQuery();

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Disconnect from HDB
            DisconnectHDB(oDB);
        }
    }
}
