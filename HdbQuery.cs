using System;
using System.Net;
using System.Data;
using System.Linq;
using System.Data.OracleClient;
using System.Collections.Generic;

namespace HDB2AQDB
{
    class HdbQuery
    {
        // Search for [JR] tag to find areas that could use some work
        private static bool jrDebug = false;
        // These are the DB log-in information
        private static string dbServer;
        public static string dbUser;
        public static string dbPass;


        public static DataTable GetHdbData(string hdb, string sdiValue, string interval, DateTime startDate, DateTime endDate, bool web = false)
        {
            DataTable dTab = new DataTable();
            if (web)
            {
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Build URL Query
                if (interval.ToLower() == "hour")
                { interval = "hr"; }
                else if (interval.ToLower() == "instant")
                { interval = "in"; }
                else
                { interval = "dy"; }
                // HDB CGI URL: http://ibr3lcrsrv01.bor.doi.net:8080/HDB_CGI.com?svr=lchdb2&sdi=1863&tstp=HR&t1=1/1/2017&t2=1/10/2017&format=8
                var url = @"http://ibr3lcrsrv01.bor.doi.net:8080/HDB_CGI.com?svr=" + hdb + "&sdi=" + sdiValue +
                    "&tstp=" + interval + "&t1=" + startDate.ToString("M-d-yyyy") + "&t2=" +
                    endDate.ToString("M-d-yyyy") + "&format=88";
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Get web data
                var response = new WebClient().DownloadString(url);
                var responseRows = new List<string>(response.Split('\n'));
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Build output DataTable
                dTab.Columns.Add("Date", typeof(string));
                dTab.Columns.Add("Value", typeof(string));
                responseRows.RemoveAt(0);
                foreach (var row in responseRows)
                {
                    var items = row.Split(',');
                    if (items.Count() == 2)
                    {
                        var dRow = dTab.NewRow();
                        dRow["Date"] = items[0].Trim();
                        dRow["Value"] = items[1].Trim();

                        if (dRow["Value"].ToString() != "NaN")
                        { dTab.Rows.Add(dRow); }
                    }
                }
            }
            else
            {
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Connect to HDB Server
                dbServer = hdb;
                var oDB = ConnectHDB();
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Get ODB data
                //dTab = queryHdbDataUsingStoredProcedure(oDB, sdiValue, interval, startDate, endDate);
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Get ODB data
                /* THIS IS THE SQL THAT IS RUN BY THIS METHOD
                        select START_DATE_TIME as HDB_DATETIME, VALUE 
                        from R_HOUR 
                        where SITE_DATATYPE_ID=1930 
                        and START_DATE_TIME >= '01-jan-2016' 
                        and START_DATE_TIME <= '02-jan-2016';
                        To_date ('28/2/2007 10:12', 'DD/MM/YYYY HH24:MI')
                 */
                var sql = "select START_DATE_TIME as HDB_DATETIME, " +
                        "VALUE from R_" + interval.ToUpper() + " " +
                        "where SITE_DATATYPE_ID=" + sdiValue + " " +
                        "and START_DATE_TIME >= To_date('" + startDate.ToString("dd-MMM-yyyy HH:mm") + "', 'DD-MON-YYYY HH24:MI') " +
                        "and START_DATE_TIME <= To_date('" + endDate.ToString("dd-MMM-yyyy HH:mm") + "', 'DD-MON-YYYY HH24:MI') ";
                dTab = queryHdbDataUsingSQL(oDB, sql);
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Disconnect from HDB
                DisconnectHDB(oDB);
            }
            return dTab;
        }


        public static DataTable GetHdbUpdates(string hdb, string sdiValue, string interval, DateTime startDate, DateTime endDate, DateTime checkDate, bool web = false)
        {
            DataTable dTab = new DataTable();
            if (web)
            {
                // WEB SERVICE NOT SETUP TO FETCH RECENT UPDATES - [JR]
            }
            else
            {
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Connect to HDB Server
                dbServer = hdb;
                var oDB = ConnectHDB();
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Get ODB data
                /* THIS IS THE SQL THAT IS RUN BY THIS METHOD
                        select START_DATE_TIME as HDB_DATETIME, VALUE 
                        from (
                          select * from r_hour 
                          where SITE_DATATYPE_ID=20183
                          and START_DATE_TIME >= (SYSDATE - 365*2)
                          and START_DATE_TIME <= (SYSDATE)
                          ) a
                        where a.DATE_TIME_LOADED > (SYSDATE - 1); 
                                            ASSUMES THAT WE WANT TO RELOAD ANYTHING UPDATED WITHIN THE LAST 24 HOURS
                                            THIS SHOULD BE AN INPUT BASED ON THE LAST SUCCESFUL RUN OF THE PROGRAM...                                                                
                 */
                var sql = "select START_DATE_TIME as HDB_DATETIME, VALUE " +
                        "from ( " + 
                        "select * from R_" + interval.ToUpper() + " " +
                        "where SITE_DATATYPE_ID=" + sdiValue + " " +
                        "and START_DATE_TIME >= To_date('" + startDate.ToString("dd-MMM-yyyy HH:mm") + "', 'DD-MON-YYYY HH24:MI') " +
                        "and START_DATE_TIME <= To_date('" + endDate.ToString("dd-MMM-yyyy HH:mm") + "', 'DD-MON-YYYY HH24:MI') " +
                        ") a where a.DATE_TIME_LOADED > To_date('" + checkDate.ToString("dd-MMM-yyyy HH:mm") + "', 'DD-MON-YYYY HH24:MI') "; 
                dTab = queryHdbDataUsingSQL(oDB, sql);
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                // Disconnect from HDB
                DisconnectHDB(oDB);
            }
            return dTab;
        }


        public static DataTable GetHdbInfo(string hdb, string sdiValue)
        {
            DataTable dTab = new DataTable();
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Connect to HDB Server
            dbServer = hdb;
            var oDB = ConnectHDB();
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Get ODB data
            /* THIS IS THE SQL THAT IS RUN BY THIS METHOD
                    select HDB_SITE_DATATYPE.SITE_DATATYPE_ID,
                    HDB_SITE.SITE_ID,
                    HDB_DATATYPE.DATATYPE_ID,
                    HDB_SITE.SITE_COMMON_NAME,
                    HDB_SITE.SITE_NAME,
                    HDB_OBJECTTYPE.OBJECTTYPE_NAME,
                    HDB_DATATYPE.DATATYPE_NAME,
                    HDB_DATATYPE.PHYSICAL_QUANTITY_NAME,
                    HDB_UNIT.UNIT_COMMON_NAME,
                    HDB_SITE.LAT,
                    HDB_SITE.LONGI,
                    HDB_STATE.STATE_CODE, 
                    HDB_SITE.DB_SITE_CODE
                    from HDB_SITE 
                    inner join HDB_SITE_DATATYPE on HDB_SITE.SITE_ID = HDB_SITE_DATATYPE.SITE_ID 
                    inner join HDB_DATATYPE on HDB_SITE_DATATYPE.DATATYPE_ID = HDB_DATATYPE.DATATYPE_ID 
                    inner join HDB_UNIT on HDB_DATATYPE.UNIT_ID = HDB_UNIT.UNIT_ID 
                    inner join HDB_STATE on HDB_SITE.STATE_ID = HDB_STATE.STATE_ID 
                    inner join HDB_OBJECTTYPE on HDB_SITE.OBJECTTYPE_ID = HDB_OBJECTTYPE.OBJECTTYPE_ID 
                    where HDB_SITE_DATATYPE.SITE_DATATYPE_ID in (1930);
             */
            var sql = "select HDB_SITE_DATATYPE.SITE_DATATYPE_ID, " +
                    "HDB_SITE.SITE_ID, " +
                    "HDB_DATATYPE.DATATYPE_ID, " +
                    "HDB_SITE.SITE_COMMON_NAME, " +
                    "HDB_SITE.SITE_NAME, " +
                    "HDB_OBJECTTYPE.OBJECTTYPE_NAME, " +
                    "HDB_DATATYPE.DATATYPE_NAME, " +
                    "HDB_DATATYPE.PHYSICAL_QUANTITY_NAME, " +
                    "HDB_UNIT.UNIT_COMMON_NAME, " +
                    "HDB_SITE.LAT, " +
                    "HDB_SITE.LONGI, " +
                    "HDB_STATE.STATE_CODE, " +
                    "HDB_SITE.DB_SITE_CODE " +
                    "from HDB_SITE " +
                    "inner join HDB_SITE_DATATYPE on HDB_SITE.SITE_ID = HDB_SITE_DATATYPE.SITE_ID " +
                    "inner join HDB_DATATYPE on HDB_SITE_DATATYPE.DATATYPE_ID = HDB_DATATYPE.DATATYPE_ID " +
                    "inner join HDB_UNIT on HDB_DATATYPE.UNIT_ID = HDB_UNIT.UNIT_ID " +
                    "inner join HDB_STATE on HDB_SITE.STATE_ID = HDB_STATE.STATE_ID " +
                    "inner join HDB_OBJECTTYPE on HDB_SITE.OBJECTTYPE_ID = HDB_OBJECTTYPE.OBJECTTYPE_ID " +
                    "where HDB_SITE_DATATYPE.SITE_DATATYPE_ID in (" + sdiValue + ")";
            dTab = queryHdbDataUsingSQL(oDB, sql);
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Disconnect from HDB
            DisconnectHDB(oDB);
            return dTab;
        }


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
            dbConx.ConnectionString = "Data Source=" + dbServer + ";User Id=" + dbUser + ";Password=" + dbServer.ToLower() + ";";
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


        /// <summary>
        /// Gets ODB data given an sql query and returns a DataTable with a common date range and sdi columns
        /// </summary>
        /// <param name="conx"></param>
        /// <param name="sdiList"></param>
        /// <param name="runIDs"></param>
        /// <returns></returns>
        private static DataTable queryHdbDataUsingSQL(OracleConnection conx, string sql)
        {
            // Initialize stuff...
            var dTab = new DataTable();
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Connect to and get ODB data
            if (jrDebug)
            { Console.Write("Downloading data... "); }
            OracleCommand cmd = new OracleCommand(sql, conx);
            cmd.CommandType = System.Data.CommandType.Text;
            OracleDataReader dr = cmd.ExecuteReader();
            var schemaTable = dr.GetSchemaTable();
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Put DB data into a .NET DataTable
            // Populate headers
            for (int i = 0; i < schemaTable.Rows.Count; i++)
            { dTab.Columns.Add(schemaTable.Rows[i]["ColumnName"].ToString(), typeof(string)); }
            // Populate data
            while (dr.Read())
            {
                var dRow = dTab.NewRow();
                for (int i = 0; i < dr.FieldCount; i++)
                {
                    if (dr[i].ToString() == "")
                    { dRow[dTab.Columns[i].ColumnName] = "NaN"; }
                    else
                    { dRow[dTab.Columns[i].ColumnName] = dr[i].ToString(); }
                }
                dTab.Rows.Add(dRow);
            }
            dTab = dTab.DefaultView.ToTable();
            if (jrDebug)
            { Console.WriteLine("Success!"); }
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // Return output
            dr.Dispose();
            cmd.Dispose();
            return dTab;
        }
        
        

    }
}
