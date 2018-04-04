using System;
using System.IO;
using System.Data.Linq;
using System.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RestSharp;
using Newtonsoft.Json;
using Reclamation.TimeSeries;

namespace HDB2AQDB
{
    class Program
    {
        // AQUARIUS GLOBAL VARIABLES
        static string aqSrvr = Environment.MachineName;
        static string aqUser;
        static string aqPswd;
        public static string hdbUserReader;
        public static string hdbPswdReader;
        public static string hdbUserWriter;
        public static string hdbPswdWriter;
        static RestClient acquisitionClient;
        static RestClient publishClient;
        static RestClient provisionClient;
        static string acquisitionAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Acquisition/v2";
        static string publishAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Publish/v2";
        static string provisionAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Provisioning/v1";
        static string authToken;

        // HDB GLOBAL VARIABLES
        static string hdb, sdID, interval;
        static DateTime startDate, endDate;
        static int okCount = 0, failCount = 0, appendCount = 0, utcConversion = 8;

        // SYNC PROGRAM VARIABLES
        static List<string> hdbValues = new List<string> { "LCHDB2", "YAOHDB", "UCHDB2", "LCHDEV" };
        static List<string> processValues = new List<string> { "READ", "WRITE", "BOTH" };
        static Logger logFile = new Logger();
        static int pointCount;
        static List<IRestResponse> appendRequestIds = new List<IRestResponse>();
        static string aquasOutputDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // BUILD VARIABLES
        static bool jrDEBUG = false;


        /// <summary>
        /// Help for command line program
        /// </summary>
        static void ShowHelp()
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("HDB to Aquarius Manual Data Transfer");
            Console.WriteLine("");
            Console.WriteLine("--aqdataupdate");
            Console.WriteLine("      tells the program to run DRADI in data update mode");
            Console.WriteLine("--auto");
            Console.WriteLine("      tells the program to run DRADI in auto mode");
            Console.WriteLine("--manual");
            Console.WriteLine("      tells the program to run DRADI manually using the inputs below");
            Console.WriteLine("--sdid=[X]");
            Console.WriteLine("      with [X] as the required SDID to update or ALL for all SDIDs");
            Console.WriteLine("--tStart=[X]");
            Console.WriteLine("      with [X] as a valid date in YYYY-MM-DD format");
            Console.WriteLine("--tEnd=[X]");
            Console.WriteLine("      with [X] as a valid date in YYYY-MM-DD format");
            Console.WriteLine("      When performing data updates on newly added SDIDs, Aquarius");
            Console.WriteLine("         can only process/transfer up to 3 years' worth of data so the");
            Console.WriteLine("         date range from tStart to tEnd should be no more that 3 years apart");
            Console.WriteLine("");
            Console.WriteLine("Sample Usage:");
            Console.WriteLine("DRADI --aqdataupdate --manual --sdid=1930 --tStart=2017-01-01");
            Console.WriteLine("DRADI --aqdataupdate --manual --sdid=ALL --tStart=2017-01-01");
            Console.WriteLine("DRADI --aqdataupdate --manual --sdid=2089 --tStart=2012-01-01 --tEnd=2014-01-01");
            Console.WriteLine("DRADI --aqdataupdate --auto");
            Console.WriteLine("");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("");
            Console.WriteLine("Aquarius Approval Status (AquAS)");
            Console.WriteLine("--getapprovals");
            Console.WriteLine("      builds the files needed by the AquAS UI");
            Console.WriteLine("");
            Console.WriteLine("Sample Usage:");
            Console.WriteLine("DRADI --getapprovals");
            Console.WriteLine("");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("Press ENTER to continue... ");
            Console.ReadLine();
        }


        /// <summary>
        /// JR TEST
        /// </summary>
        static void MainTEST()
        {
            GetCredentials();
            //HdbWrite.PutHdbData("lchdb2", 1202, "month", new DateTime(2017, 10, 1), 5.0);
        }


        /// <summary>
        /// MAIN ENTRY POINT FOR THE PROGRAM
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] argList)
        {
            GetCredentials();

            ///////////////////////////////////////////////////////////////////////
            // DEFINE DEBUG SETTINGS
            if (jrDEBUG)
            {
                #region
                aqSrvr = "IBR3LCRAQU02";
                acquisitionAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Acquisition/v2";
                publishAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Publish/v2";
                provisionAPI = @"http://" + aqSrvr + ".bor.doi.net/AQUARIUS/Provisioning/v1";

                //argList = new string[1];
                //argList[0] = "getapprovals";

                argList = new string[5];
                argList[0] = "aqdataupdate";
                argList[1] = "manual";
                argList[2] = "sdid=13973";
                int year1 = 2016;
                int year2 = 2017;
                argList[3] = "tStart=" + (year1 - 1) + "-12-31";
                if (year2 == DateTime.Now.Year)
                { argList[4] = "tEnd=" + year2 + "-08-24"; }
                else
                { argList[4] = "tEnd=" + year2 + "-12-31"; }
                #endregion
            }            

            Arguments args = new Arguments(argList);
            if (args.Count == 0)
            {
                ShowHelp();
                return;
            }

            // INITIALIZE CONNECTION
            ConnectToAquarius();

            tsInventory tsItems;
            // GET ALL TS ITEMS IN AQDB
            if (args.Contains("getapprovals"))
            { tsItems = GetAqTimeSeries(true); }
            else
            { tsItems = GetAqTimeSeries(); }

            ///////////////////////////////////////////////////////////////////////
            // GET APPROVAL LEVELS IN AQDB
            if (args.Contains("getapprovals"))
            {
                #region
                Console.WriteLine("Running AquAS...");
                var t1 = new DateTime(DateTime.Now.Year - 2, 1, 1);
                var t2 = new DateTime(DateTime.Now.Year, 12, 31);

                //initialize output files
                var sitesTags = "";
                int siteNum = 0;
                var thisYear = new List<string>(); thisYear.Add("site\tday\tvalue");
                var lastYear = new List<string>(); lastYear.Add("site\tday\tvalue");
                var twoYear = new List<string>(); twoYear.Add("site\tday\tvalue");

                //build AquAS output files
                BuildAquasOutputs(tsItems, t1, t2, ref sitesTags, ref siteNum, thisYear, lastYear, twoYear);

                //output dashboard input files
                sitesTags.TrimEnd(',');
                System.IO.File.WriteAllLines(aquasOutputDir + @"\sitesTags.txt", new string[] { sitesTags });
                System.IO.File.WriteAllLines(aquasOutputDir + @"\thisYear.txt", thisYear.ToArray());
                System.IO.File.WriteAllLines(aquasOutputDir + @"\lastYear.txt", lastYear.ToArray());
                System.IO.File.WriteAllLines(aquasOutputDir + @"\twoYearsAgo.txt", twoYear.ToArray());
                #endregion
            }
            ///////////////////////////////////////////////////////////////////////
            // IMPORT HDB DATA FOR TS ITEMS IN AQDB
            else if (args.Contains("aqdataupdate"))
            {
                #region
                foreach (var ts in tsItems.TimeSeriesDescriptions)
                {
                    // CHECK IF TS HAS AQ EXTENDED ATTRIBUTES
                    var hdbSyncVars = ts.ExtendedAttributes;
                    if (hdbSyncVars[0].Value != null && hdbSyncVars[1].Value != null && hdbSyncVars[2].Value != null && ts.TimeSeriesType.ToLower() == "reflected")
                    {
                        // GET INPUTS FROM AQDB REQUIRED BY HDB SYNC PROCESS
                        hdb = hdbSyncVars[0].Value;
                        string syncProc = hdbSyncVars[1].Value;
                        sdID = hdbSyncVars[2].Value;

                        // CHECK FOR REQUIRED AQDB VARIABLES AND PROCESS
                        if (hdbValues.Contains(hdb) && processValues.Contains(syncProc) && Regex.IsMatch(sdID, @"^\d+$"))
                        {
                            // GET TS INTERVAL FROM AQDB
                            interval = ts.ComputationPeriodIdentifier;
                            if (interval == "Hourly")
                            { interval = "HOUR"; }
                            else
                            { interval = "DAY"; }

                            // BUILD QUERY DATES GIVEN INTERVAL
                            if (interval == "HOUR")
                            {
                                startDate = DateTime.Now.AddDays(-1);
                                endDate = DateTime.Now;
                            }
                            else
                            {
                                startDate = DateTime.Now.AddDays(-7);
                                endDate = DateTime.Now;
                            }
                            logFile.Log(" Processing SDID#" + sdID);

                            // CHECK IF AQ RawStartTime EXISTS -- NEW TS IF !EXISTS SO TRY TO POPULATE BACK TO 01JAN2012
                            if (ts.RawStartTime == null && !args.Contains("manual"))
                            {
                                // Hard coded default start date per BHO
                                startDate = new DateTime(2012, 1, 1, 0, 0, 0);
                                // Transfer 3-year chunks of data
                                if ((endDate.Year - startDate.Year) >= 3)
                                {
                                    var origStart = startDate;
                                    var origEnd = endDate;
                                    var chunkCount = System.Math.Ceiling((endDate.Year - startDate.Year) / 3.0);
                                    for (int i = 0; i < chunkCount; i++)
                                    {
                                        startDate = origStart.AddYears(3 * i).AddDays(-1);
                                        endDate = origStart.AddYears(3 + (3 * i));
                                        if (startDate > origEnd)
                                        { endDate = origEnd; }
                                        ReflectedTimeSeriesOverWriteAppend(ts.UniqueId);
                                    }
                                    startDate = origStart;
                                    endDate = origEnd;
                                }
                                else
                                {
                                    ReflectedTimeSeriesOverWriteAppend(ts.UniqueId);
                                }
                            }
                            // TS EXISTS SO ONLY GET UPDATED DATA
                            else
                            {
                                if (args.Contains("manual"))
                                {
                                    // [JR] MANUAL OVERRRIDE TO FILL IN SPECIFIC DATES
                                    //if (sdID == "7776" || sdID == "8018")
                                    if (argList.Length == 0 || !args.Contains("sdid") || !args.Contains("tStart"))
                                    {
                                        ShowHelp();
                                        return;
                                    }
                                    else
                                    {
                                        //startDate = new DateTime(2017, 4, 27, 0, 0, 0);
                                        var sdIDCheck = args["sdid"].ToString();
                                        if (sdID == sdIDCheck || sdIDCheck.ToLower() == "all")
                                        {
                                            startDate = DateTime.Parse(args["tStart"].ToString());
                                            try
                                            {
                                                endDate = DateTime.Parse(args["tEnd"].ToString());
                                            }
                                            catch
                                            {
                                                endDate = DateTime.Now;
                                            }
                                            Console.Write("Filling SDID " + sdID + "... ");
                                            ReflectedTimeSeriesOverWriteAppend(ts.UniqueId);
                                            Console.WriteLine("Done!");
                                        }
                                    }
                                }
                                else if (args.Contains("auto"))
                                {
                                    ReflectedTimeSeriesOverWriteAppend(ts.UniqueId, true);
                                }
                                else
                                {

                                }
                            }
                        } //END AQTS PROCESSING
                    } //END AQ EXTENDED ATTRIBUTE CHECK
                } //LOOP TO NEXT AQTS OBJECT

                // SLEEP FOR 10 SECONDS BEFORE CHECKING APPEND STATUS
                System.Threading.Thread.Sleep((int)System.TimeSpan.FromSeconds(10).TotalMilliseconds);

                // CHECK STATUS OF APPEND REQUESTS
                foreach (var appendItem in appendRequestIds)
                {
                    var result = TimeSeriesAppendStatus(appendItem);
                    if (result[0] < 400)
                    { okCount++; }
                    else
                    { failCount++; }
                    appendCount += result[1];
                }
                logFile.Log(" APPEND STATUS | " + okCount + " SDIDs Succeeded | " + failCount + " SDIDs Failed | " + appendCount + " Total points appended");
                #endregion
            }
            ///////////////////////////////////////////////////////////////////////
            // GET HDB UPDATED DATA FOR TS ITEMS IN AQDB
            else if (args.Contains("aqgetupdateddata"))
            {
                #region
                foreach (var ts in tsItems.TimeSeriesDescriptions)
                {
                    // CHECK IF TS HAS AQ EXTENDED ATTRIBUTES
                    var hdbSyncVars = ts.ExtendedAttributes;
                    if (hdbSyncVars[0].Value != null && hdbSyncVars[1].Value != null && hdbSyncVars[2].Value != null && ts.TimeSeriesType.ToLower() == "reflected")
                    {
                        // GET INPUTS FROM AQDB REQUIRED BY HDB SYNC PROCESS
                        hdb = hdbSyncVars[0].Value;
                        string syncProc = hdbSyncVars[1].Value;
                        sdID = hdbSyncVars[2].Value;

                        // CHECK FOR REQUIRED AQDB VARIABLES AND PROCESS
                        if (hdbValues.Contains(hdb) && processValues.Contains(syncProc) && Regex.IsMatch(sdID, @"^\d+$"))
                        {
                            // GET TS INTERVAL FROM AQDB
                            interval = ts.ComputationPeriodIdentifier;
                            if (interval == "Hourly")
                            { interval = "HOUR"; }
                            else
                            { interval = "DAY"; }

                            // BUILD QUERY DATES GIVEN INTERVAL
                            if (interval == "HOUR")
                            {
                                startDate = DateTime.Now.AddDays(-1);
                                endDate = DateTime.Now;
                            }
                            else
                            {
                                startDate = DateTime.Now.AddDays(-7);
                                endDate = DateTime.Now;
                            }
                            logFile.Log(" Processing SDID#" + sdID);


                        } //END AQTS PROCESSING
                    } //END AQ EXTENDED ATTRIBUTE CHECK
                } //LOOP TO NEXT AQTS OBJECT

                // SLEEP FOR 10 SECONDS BEFORE CHECKING APPEND STATUS
                System.Threading.Thread.Sleep((int)System.TimeSpan.FromSeconds(10).TotalMilliseconds);

                // CHECK STATUS OF APPEND REQUESTS
                foreach (var appendItem in appendRequestIds)
                {
                    var result = TimeSeriesAppendStatus(appendItem);
                    if (result[0] < 400)
                    { okCount++; }
                    else
                    { failCount++; }
                    appendCount += result[1];
                }
                logFile.Log(" APPEND STATUS | " + okCount + " SDIDs Succeeded | " + failCount + " SDIDs Failed | " + appendCount + " Total points appended");
                #endregion

            }
            ///////////////////////////////////////////////////////////////////////
            // SHOW HELP
            else
            {
                ShowHelp();
            }

            // DISCONNECT
            DisconnectFromAquarius();
        }


        /// <summary>
        /// Read HDB and Aquarius credentials from text file
        /// </summary>
        static void GetCredentials()
        {
            try
            {
                var path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory.ToString(), "credentials.txt");
                string[] text = File.ReadAllLines(path);
                hdbUserReader = text[0].Split('=')[1].ToString();
                hdbPswdReader = text[1].Split('=')[1].ToString();
                aqUser = text[2].Split('=')[1].ToString();
                aqPswd = text[3].Split('=')[1].ToString();
                hdbUserWriter = text[4].Split('=')[1].ToString();
                hdbPswdWriter = text[5].Split('=')[1].ToString();
            }
            catch
            {
                Console.WriteLine("Textfile containing HDB & Aquarius credentials not found...");
                Console.WriteLine("\tcredentials.txt has to be in the same folder as the executable ");
                Console.WriteLine("\tprogram and should contain:");
                Console.WriteLine("\thdbUser-Read=XXXXX");
                Console.WriteLine("\thdbPass-Read=XXXXX");
                Console.WriteLine("\taquUser=XXXXX");
                Console.WriteLine("\taquPass=XXXXX");
                Console.WriteLine("\thdbUser-Write=XXXXX");
                Console.WriteLine("\thdbPass-Write=XXXXX");
                Console.WriteLine("");
            }
        }


        /// <summary>
        /// Connect to Aquarius DB server
        /// </summary>
        private static void ConnectToAquarius()
        {
            // Initialize Rest clients for connecting to AQ
            acquisitionClient = new RestClient(acquisitionAPI);
            publishClient = new RestClient(publishAPI);
            provisionClient = new RestClient(provisionAPI);

            // Define session variables for authentication
            var request = new RestRequest("session/", Method.POST);
            request.AddParameter("Username", aqUser); // adds to POST or URL querystring based on Method
            request.AddParameter("EncryptedPassword", aqPswd);
            request.AddParameter("Locale", "");

            // Execute the request and get authentication token
            IRestResponse restResponse = acquisitionClient.Execute(request);
            authToken = restResponse.Content; // raw content as string
            ValidateResponse(restResponse, "Connected to Aquarius");
        }


        /// <summary>
        /// Invalidates the authentication token from ConnectToAquarius()
        /// </summary>
        private static void DisconnectFromAquarius()
        {
            var request = new RestRequest("session/", Method.DELETE);
            IRestResponse restResponse = acquisitionClient.Execute(request);
            //ValidateResponse(restResponse, "Disconnected from Aquarius");
            logFile.Log(" Disconnected from Aquarius");
            logFile.Log("-------------------------------------------");
        }
        

        /// <summary>
        /// Attaches authentication token from ConnectToAquarius() to RestRequest 
        /// </summary>
        /// <param name="restRequest"></param>
        /// <returns></returns>
        private static RestRequest AuthorizeRequest(RestRequest restRequest)
        {
            restRequest.AddHeader("x-authentication-token", authToken);
            return restRequest;
        }


        /// <summary>
        /// Checks API Reponse and appends entries to the log
        /// </summary>
        /// <param name="restResponse"></param>
        /// <param name="successMessage"></param>
        /// <param name="failMessage"></param>
        /// <returns></returns>
        private static bool ValidateResponse(IRestResponse restResponse, string successMessage, string failMessage = "")
        {
            int restStatusCode = (int)restResponse.StatusCode;
            if (restStatusCode < 400)
            {
                logFile.Log(" " + successMessage);
                return true;
            }
            else
            {
                logFile.Log(" FAIL " + failMessage + " Aquarius Error: " + restResponse.Content);
                return false;
            }
        }


        /// <summary>
        /// Get TS items from AQ
        /// </summary>
        private static tsInventory GetAqTimeSeries(bool getPublishedTS = false)
        {
            // Get available Locations
            var request = new RestRequest("GetTimeSeriesDescriptionList/", Method.GET);
            if (getPublishedTS)
            {
                request.AddParameter("Publish", getPublishedTS);
            }
            request = AuthorizeRequest(request);
            IRestResponse restResponse = publishClient.Execute(request);
            ValidateResponse(restResponse, "List of Aquarius TS objects fetched");
            return JsonConvert.DeserializeObject<tsInventory>(restResponse.Content);
        }


        /// <summary>
        /// Get Approval Levels for AQTS
        /// </summary>
        private static tsCorrectedData GetAqTimeSeriesApprovals(string tsID, DateTime t1, DateTime t2)
        {
            // Get available Locations
            var request = new RestRequest("GetTimeSeriesCorrectedData/", Method.GET);
            request.AddParameter("TimeSeriesUniqueId", tsID);
            request.AddParameter("QueryFrom", ConvertDateTimeJVS(t1));
            request.AddParameter("QueryTo", ConvertDateTimeJVS(t2));
            request.AddParameter("GetParts", "MetadataOnly");
            request.AddParameter("ReturnFullCoverage", "true");

            request = AuthorizeRequest(request);
            IRestResponse restResponse = publishClient.Execute(request);
            ValidateResponse(restResponse, "Approval Levels for " + tsID + " fetched");
            return JsonConvert.DeserializeObject<tsCorrectedData>(restResponse.Content);
        }


        /// <summary>
        /// Append Basic TS data for new values
        /// </summary>
        //private static void TimeSeriesAppend(string tsID)
        //{
        //    // Get HDB data
        //    string dataPoints = GetSiteDataTypeData();

        //    // Build API call
        //    var request = new RestRequest("timeseries/" + tsID + @"/append", Method.POST);
        //    request.AddParameter("UniqueId", tsID);
        //    request.AddParameter("Points", dataPoints);

        //    request = AuthorizeRequest(request);
        //    IRestResponse restResponse = acquisitionClient.Execute(request);
        //    // Log append request
        //    appendRequestIds.Add(restResponse);
        //    ValidateResponse(restResponse, "Write to Aquarius DB in progress");
        //}


        /// <summary>
        /// Append Basic TS data and overwrite existing values
        /// </summary>
        //private static void TimeSeriesOverWriteAppend(string tsID)
        //{
        //    bool getHdbUpdates = true;
        //    // Get HDB data
        //    string dataPoints = GetSiteDataTypeData(getHdbUpdates);

        //    // Build API call
        //    //var request = new RestRequest("timeseries/" + tsID + @"/overwriteappend", Method.POST);
        //    var request = new RestRequest("timeseries/" + tsID + @"/append", Method.POST);
        //    request.AddParameter("UniqueId", tsID);
        //    request.AddParameter("Points", dataPoints);
        //    //request.AddParameter("TimeRange", "Interval");

        //    request = AuthorizeRequest(request);
        //    IRestResponse restResponse = acquisitionClient.Execute(request);
        //    // Log append request
        //    appendRequestIds.Add(restResponse);
        //    ValidateResponse(restResponse, "Write to Aquarius DB in progress");
        //}


        /// <summary>
        /// Append Reflected TS data and overwrite existing values
        /// </summary>
        private static void ReflectedTimeSeriesOverWriteAppend(string tsID, bool getHdbUpdates = false)
        {
            // Get HDB data
            string tRange = "";
            string dataPoints = "";
            GetSiteDataTypeData(out dataPoints, out tRange, getHdbUpdates);

            if (dataPoints == "[{}]")
            {
                logFile.Log(" FAIL HDB Error: No data found for " + sdID + " in " + hdb);
                failCount++;
            }
            else
            {
                // Build API call
                var request = new RestRequest("timeseries/" + tsID + @"/reflected", Method.POST);
                request.AddParameter("UniqueId", tsID);
                request.AddParameter("Points", dataPoints);
                request.AddParameter("TimeRange", tRange);

                request = AuthorizeRequest(request);
                IRestResponse restResponse = acquisitionClient.Execute(request);
                // Log append request
                appendRequestIds.Add(restResponse);
                ValidateResponse(restResponse, "Write to Aquarius DB in progress");
            }
        }


        /// <summary>
        /// Check Append status given API response
        /// </summary>
        private static int[] TimeSeriesAppendStatus(IRestResponse restResponse)
        {
            int[] output = new int[2];
            // output[0] = response code
            // output[1] = points appended

            var appendResponse = JsonConvert.DeserializeObject<tsAppendReponse>(restResponse.Content);
            if (appendResponse.AppendRequestIdentifier == null) //no data found in HDB
            {
                output[0] = 400;
                output[1] = 0;
            }
            else
            {
                // Build API call
                var request = new RestRequest("timeseries/appendstatus/" + appendResponse.AppendRequestIdentifier,
                    Method.GET);

                request = AuthorizeRequest(request);
                restResponse = acquisitionClient.Execute(request);
                var appendStatus = JsonConvert.DeserializeObject<tsAppendStatus>(restResponse.Content);

                output[0] = (int)restResponse.StatusCode;
                output[1] = Convert.ToInt32(appendStatus.NumberOfPointsAppended);
            }

            return output;
        }


        /// <summary>
        /// Query HDB data for SDID
        /// </summary>
        /// <returns></returns>
        private static void GetSiteDataTypeData(out string data, out string tRange, bool update = false)
        {
            // Old API format
            //byte[] theData = Encoding.ASCII.GetBytes(
            //    @"2011-01-23 01:00,2.8,,,,1,
            //    2011-01-23 01:15:33,3.8,,,,1,
            //    2011-01-23 01:30:33,2.8,,,,1,
            //    2011-01-23 01:45:33,8.8,,,,1,");

            // New JSON format
            //{
            //    "UniqueId": "",
            //      "Points": [
            //        {
            //          "GradeCode": 0,
            //          "Qualifiers": [
            //            ""
            //          ],
            //          "Time": "Instant",
            //          "Value": 0
            //        }
            //      ],
            //      "TimeRange": "Interval"
            //}

            pointCount = 0;
            DataTable dTab = new DataTable();
            dTab = HdbQuery.GetHdbData(hdb, sdID, interval, startDate, endDate);
            utcConversion = 8; // Data not from the INSTANT tables need an 8 for UTC-07 to UTC+00 conversion

            // Try the INSTANT tables if there is no data in the HOUR table
            if (dTab.Rows.Count == 0 && interval == "HOUR") // No data in HDB
            {
                dTab = HdbQuery.GetHdbData(hdb, sdID, "instant", startDate, endDate);
                utcConversion = 7; // Data from the INSTANT tables need an 8 for UTC-07 to UTC+00 conversion
            }
            

            //if (update)
            //{
            //    dTab = HdbQuery.GetHdbUpdates(hdb, sdID, interval, startDate, endDate);
            //}
            //else
            //{
            //    dTab = HdbQuery.GetHdbData(hdb, sdID, interval, startDate, endDate);
            //}
            //var dTab = HdbQuery.GetHdbData(hdb, sdID, interval, startDate, endDate, true);
            data = "";            
            
            if (dTab.Rows.Count == 0) // No data in HDB
            {
                data = "[{}]";
                tRange = @"{""Start"":""" + ConvertUtcMinus07(DateTime.Now.AddHours(-1)).ToString("s", System.Globalization.CultureInfo.InvariantCulture) +
                            @""",""End"":""" + ConvertUtcMinus07(DateTime.Now.AddHours(1)).ToString("s", System.Globalization.CultureInfo.InvariantCulture) +
                            @"""}";
            } 
            else
            {
                data = @"[";
                foreach (DataRow row in dTab.Rows)
                {
                    DateTime t = ConvertUtcMinus07(DateTime.Parse(row[0].ToString()));
                    double val = double.Parse(row[1].ToString());
                    data = data + @"{GradeCode:0,Qualifiers:[],Time:" +
                        //t.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + // for Basic TS
                        ConvertDateTimeJVS(t) + // for Reflected TS
                        @",Value:" + val + "},";
                    pointCount++;
                    //Console.WriteLine(t.ToString() + " - " + ConvertDateTimeJVS(t));
                }
                data = data.Remove(data.Length - 1); //remove last comma from loop
                data = data + "]";

                tRange = @"{""Start"":""" + ConvertUtcMinus07(DateTime.Parse(dTab.Rows[0][0].ToString()).AddMinutes(-1)).ToString("s", System.Globalization.CultureInfo.InvariantCulture) +
                            @""",""End"":""" + ConvertUtcMinus07(DateTime.Parse(dTab.Rows[dTab.Rows.Count - 1][0].ToString()).AddMinutes(1)).ToString("s", System.Globalization.CultureInfo.InvariantCulture) +
                            @"""}";
            }
        }
        

        /// <summary>
        /// Converts UTC-07 to UTC+00
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static DateTime ConvertUtcMinus07(DateTime t)
        {
            return t.AddHours(utcConversion);
        }


        /// <summary>
        /// Converts to AQ JVS Format
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static string ConvertDateTimeJVS(DateTime t)
        {
            var tOut = t.ToString("yyyy-MM-dd") + "T" + t.ToString("HH:mm") + ":00.0000000+00:00";
            return tOut;
        }


        /// <summary>
        /// Builds the text files required by the AquAS UI
        /// </summary>
        /// <param name="tsItems"></param>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <param name="sitesTags"></param>
        /// <param name="siteNum"></param>
        /// <param name="thisYear"></param>
        /// <param name="lastYear"></param>
        /// <param name="twoYear"></param>
        private static void BuildAquasOutputs(tsInventory tsItems, DateTime t1, DateTime t2, ref string sitesTags, ref int siteNum, List<string> thisYear, List<string> lastYear, List<string> twoYear)
        {
            tsItems.TimeSeriesDescriptions.Sort((x,y) => string.Compare(x.LocationIdentifier,y.LocationIdentifier));
            foreach (var ts in tsItems.TimeSeriesDescriptions)
            {
                //if (ts.Label.Contains("Published |"))
                //{
                //get approvals
                var tsApprovals = GetAqTimeSeriesApprovals(ts.UniqueId, t1, t2);
                Console.WriteLine("Processing " + tsApprovals.LocationIdentifier + " | " + tsApprovals.Parameter + @""",");
                //build approvals table
                var dTab = new DataTable();
                dTab.Columns.Add("startDate", typeof(DateTime));
                dTab.Columns.Add("endDate", typeof(DateTime));
                dTab.Columns.Add("loadDate", typeof(DateTime));
                dTab.Columns.Add("ApprovalLevel", typeof(string));
                dTab.Columns.Add("Comment", typeof(string));
                foreach (var item in tsApprovals.Approvals)
                {
                    var dRow = dTab.NewRow();
                    dRow["startDate"] = item.StartTime;
                    dRow["endDate"] = item.EndTime;
                    dRow["loadDate"] = item.DateAppliedUtc;
                    dRow["approvalLevel"] = item.LevelDescription;
                    dRow["comment"] = item.Comment;
                    dTab.Rows.Add(dRow);
                }
                dTab.DefaultView.Sort = "loadDate ASC";
                dTab = dTab.DefaultView.ToTable();

                //build approvals series with no approval data
                var s = new Series();
                for (DateTime t = t1; t <= t2; t = t.AddDays(1))
                {
                    s.Add(t, 0.0, "4");//no approval data
                }

                // iterate through approval levels in AQ and update series
                foreach (DataRow row in dTab.Rows)
                {
                    DateTime ithStart = DateTime.Parse(row["startDate"].ToString()).Date;
                    DateTime ithEnd = DateTime.Parse(row["endDate"].ToString()).Date;
                    string ithApproval = row["approvalLevel"].ToString();

                    string approvalFlag;
                    if (ithApproval.ToLower() == "working")
                    { approvalFlag = "1"; }
                    else if (ithApproval.ToLower() == "in review")
                    { approvalFlag = "2"; }
                    else if (ithApproval.ToLower() == "approved")
                    { approvalFlag = "3"; }
                    else
                    {
                        approvalFlag = "4";
                    }

                    if (ithStart < t1) { ithStart = t1; }
                    if (ithEnd > t2) { ithEnd = t2; }
                    var sTemp = new Series();
                    for (DateTime t = ithStart; t <= ithEnd; t = t.AddDays(1))
                    {
                        sTemp.Add(t, 0.0, approvalFlag);
                    }
                    s = Reclamation.TimeSeries.Math.Merge(sTemp, s);
                }

                //write data entries to dashboard input file
                /*
                 *************************
                 sitesTags.txt - THIS IS A COMMA-DELIMITED TEXT FILE OF THE ROWS IN THE DASHBOARD MAPS TO THE SITE# FOR THE DATA FILE
                 "SITE 1 | PAR 1","SITE 1 | PAR 2","SITE 2 | PAR 1","SITE 2 | PAR 2","SITE 2 | PAR 3"
                 */
                sitesTags += @"""" + tsApprovals.LocationIdentifier + " | " + tsApprovals.Parameter + @""",";
                siteNum = siteNum + 1;
                /*
                *************************
                 thisYear.txt - THIS IS A TAB-DELIMITED FILE OF THE DAILY ROWS FOR THE DASHBOARD
                 site day value
                 1	1	1
                 1	2	1
                 1	.	2                         
                 1	.	4                         
                 1	.	3                         
                 1	365	3                         
                 *************************
                 */
                foreach (Point pt in s)
                {
                    var yearDiff = pt.DateTime.Year - t1.Year;
                    //var jDay = pt.DateTime.DayOfYear;
                    //var val = pt.Flag;
                    var val = siteNum.ToString() + "\t" + pt.DateTime.DayOfYear.ToString() + "\t" + pt.Flag;
                    switch (yearDiff)
                    {
                        case 0:
                            twoYear.Add(val);
                            break;
                        case 1:
                            lastYear.Add(val);
                            break;
                        case 2:
                            thisYear.Add(val);
                            break;
                        default:
                            break;
                    }
                }
                //}
            }
        }

        
    }
}

