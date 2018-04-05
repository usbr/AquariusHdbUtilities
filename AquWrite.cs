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
    class AquWrite
    {
        /// <summary>
        /// Append Reflected TS data and overwrite existing values
        /// </summary>
        public static void ReflectedTimeSeriesOverWriteAppend(string tsID, bool getHdbUpdates = false)
        {
            // Get HDB data
            string tRange = "";
            string dataPoints = "";
            Program.GetSiteDataTypeData(out dataPoints, out tRange, getHdbUpdates);

            if (dataPoints != "[{}]")
            {
                // Build API call
                var request = new RestRequest("timeseries/" + tsID + @"/reflected", Method.POST);
                request.AddParameter("UniqueId", tsID);
                request.AddParameter("Points", dataPoints);
                request.AddParameter("TimeRange", tRange);

                request = Program.AuthorizeRequest(request);
                IRestResponse restResponse = Program.acquisitionClient.Execute(request);
                // Log append request
                Program.appendRequestIds.Add(restResponse);
                Program.ValidateResponse(restResponse, "Write to Aquarius DB in progress");
            }
        }
    }
}
