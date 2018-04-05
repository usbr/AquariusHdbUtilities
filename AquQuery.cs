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
    class AquQuery
    {
        /// <summary>
        /// Connect to Aquarius DB server
        /// </summary>
        public static string ConnectToAquarius()
        {
            // Initialize Rest clients for connecting to AQ
            Program.acquisitionClient = new RestClient(Program.acquisitionAPI);
            Program.publishClient = new RestClient(Program.publishAPI);
            Program.provisionClient = new RestClient(Program.provisionAPI);

            // Define session variables for authentication
            var request = new RestRequest("session/", Method.POST);
            request.AddParameter("Username", Program.aqUser); // adds to POST or URL querystring based on Method
            request.AddParameter("EncryptedPassword", Program.aqPswd);
            request.AddParameter("Locale", "");

            // Execute the request and get authentication token
            IRestResponse restResponse = Program.acquisitionClient.Execute(request);
            string authToken = restResponse.Content; // raw content as string
            Program.ValidateResponse(restResponse, "Connected to Aquarius");
            return authToken;
        }


        /// <summary>
        /// Invalidates the authentication token from ConnectToAquarius()
        /// </summary>
        public static void DisconnectFromAquarius()
        {
            var request = new RestRequest("session/", Method.DELETE);
            IRestResponse restResponse = Program.acquisitionClient.Execute(request);
            //ValidateResponse(restResponse, "Disconnected from Aquarius");
            Program.logFile.Log(" Disconnected from Aquarius");
            Program.logFile.Log("-------------------------------------------");
        }


        /// <summary>
        /// Get TS items from AQ
        /// </summary>
        public static tsInventory GetAqTimeSeries(bool getPublishedTS = false)
        {
            // Get available Locations
            var request = new RestRequest("GetTimeSeriesDescriptionList/", Method.GET);
            if (getPublishedTS)
            {
                request.AddParameter("Publish", getPublishedTS);
            }
            request = Program.AuthorizeRequest(request);
            IRestResponse restResponse = Program.publishClient.Execute(request);
            Program.ValidateResponse(restResponse, "List of Aquarius TS objects fetched");
            tsInventory tsOut;
            if (getPublishedTS) //return all tsitems
            {
                tsOut = JsonConvert.DeserializeObject<tsInventory>(restResponse.Content);
            }
            else //filter tsitems needed for data transfer processing
            {
                tsOut = FilterAqTimeSeries(JsonConvert.DeserializeObject<tsInventory>(restResponse.Content));
            }
            return tsOut;
        }


        /// <summary>
        /// Isolates only the AQTS items that meet processing requirements
        /// </summary>
        /// <param name="allTS"></param>
        /// <returns></returns>
        private static tsInventory FilterAqTimeSeries(tsInventory allTS)
        {
            tsInventory filteredTS = new tsInventory();
            filteredTS.ResponseTime = allTS.ResponseTime;
            filteredTS.ResponseVersion = allTS.ResponseVersion;
            filteredTS.Summary = allTS.Summary;
            List<tsItems> filteredList = new List<tsItems>();
            foreach (var ts in allTS.TimeSeriesDescriptions)
            {
                // CHECK IF TS HAS AQ EXTENDED ATTRIBUTES AND IS A REFLECTED TS
                var hdbSyncVars = ts.ExtendedAttributes;
                if (hdbSyncVars[0].Value != null && hdbSyncVars[1].Value != null && hdbSyncVars[2].Value != null && ts.TimeSeriesType.ToLower() == "reflected")
                {
                    filteredList.Add(ts);
                }
            }
            filteredTS.TimeSeriesDescriptions = filteredList;
            return filteredTS;
        }


        /// <summary>
        /// Get Approval Levels for AQTS
        /// </summary>
        public static tsCorrectedData GetAqTimeSeriesApprovals(string tsID, DateTime t1, DateTime t2)
        {
            // Get available Locations
            var request = new RestRequest("GetTimeSeriesCorrectedData/", Method.GET);
            request.AddParameter("TimeSeriesUniqueId", tsID);
            request.AddParameter("QueryFrom", Program.ConvertDateTimeJVS(t1));
            request.AddParameter("QueryTo", Program.ConvertDateTimeJVS(t2));
            request.AddParameter("GetParts", "MetadataOnly");
            request.AddParameter("ReturnFullCoverage", "true");

            request = Program.AuthorizeRequest(request);
            IRestResponse restResponse = Program.publishClient.Execute(request);
            Program.ValidateResponse(restResponse, "Approval Levels for " + tsID + " fetched");
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
        /// Check Append status given API response
        /// </summary>
        public static int[] TimeSeriesAppendStatus(IRestResponse restResponse)
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

                request = Program.AuthorizeRequest(request);
                restResponse = Program.acquisitionClient.Execute(request);
                var appendStatus = JsonConvert.DeserializeObject<tsAppendStatus>(restResponse.Content);

                output[0] = (int)restResponse.StatusCode;
                output[1] = Convert.ToInt32(appendStatus.NumberOfPointsAppended);
            }

            return output;
        }





    }
}
