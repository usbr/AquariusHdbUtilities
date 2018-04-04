using System;
using System.Collections.Generic;
using System.IO;

namespace HDB2AQDB
{
    /*******************************************************************************************  
     *  This code file contains the hard-coded classes for the JSON responses that the 
     *  API service generates. Classes are defined based on the API responses via the  
     *  Swagger-UI services.
     *******************************************************************************************
     */

    public class tsInventory
    {
        public string ResponseVersion { get; set; }
        public string ResponseTime { get; set; }
        public string Summary { get; set; }
        public List<tsItems> TimeSeriesDescriptions { get; set; }   // NESTED ARRAYS[] ARE DESERIALIZED BY A LIST OF THEIR CLASS
    }


    public class tsItems
    {
        public string Identifier { get; set; }
        public string UniqueId { get; set; }
        public string LocationIdentifier { get; set; }
        public string Parameter { get; set; }
        public string Unit { get; set; }
        public string UtcOffset { get; set; }
        public string UtcOffsetIsoDuration { get; set; }
        public string LastModified { get; set; }
        public string RawStartTime { get; set; }
        public string RawEndTime { get; set; }
        public string TimeSeriesType { get; set; }
        public string Label { get; set; }
        public string Comment { get; set; }
        public string Description { get; set; }
        public string Publish { get; set; }
        public string ComputationIdentifier { get; set; }
        public string ComputationPeriodIdentifier { get; set; }
        public string SubLocationIdentifier { get; set; }
        public List<tsHdbSyncAttributes> ExtendedAttributes { get; set; }
        public List<tsThresholds> Thresholds { get; set; }
    }


    public class tsHdbSyncAttributes
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
    }

    public class tsThresholds
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReferenceCode { get; set; }
        public string Severity { get; set; }
        public string Type { get; set; }
        public string DisplayColor { get; set; }
        public List<thresholdPeriods> Periods { get; set; }
    }
    
    public class thresholdPeriods
    {

        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string AppliedTime { get; set; }
        public string ReferenceValue { get; set; }
        public string SuppressData { get; set; }
    }


    public class tsAppendReponse
    {
        public string AppendRequestIdentifier { get; set; }
    }


    public class tsAppendStatus
    {
        public string AppendStatus { get; set; }
        public string NumberOfPointsAppended { get; set; }
        public string NumberOfPointsDeleted { get; set; }
    }


    public class tsCorrectedData
    {
        public string ResponseVersion { get; set; }
        public string ResponseTime { get; set; }
        public string Summary { get; set; }
        public string UniqueId { get; set; }
        public string Parameter { get; set; }
        public string Label { get; set; }
        public string LocationIdentifier { get; set; }
        public string Unit { get; set; }
        public List<Approvals> Approvals { get; set; }
    }


    public class Approvals
    {
        public string ApprovalLevel { get; set; }
        public string DateAppliedUtc { get; set; }
        public string User { get; set; }
        public string LevelDescription { get; set; }
        public string Comment { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }


    public class Logger
    {
        public void Log(string message)
        {
            string tempfile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\temp.txt";
            string filename = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\log.txt";
            using (var writer = new StreamWriter(tempfile))
            using (var reader = new StreamReader(filename))
            {
                writer.WriteLine("[{0}][{1}] {2} ", String.Format("{0:yyyyMMdd HH:mm:ss}", DateTime.Now), "Info", message);
                int linesToKeep = 250, nthLine = 0;
                while (nthLine < linesToKeep)
                {
                    writer.WriteLine(reader.ReadLine());
                    nthLine++;
                }
            }
            File.Copy(tempfile, filename, true);
            File.Delete(tempfile);

            //using (StreamWriter w = File.AppendText(System.IO.Path.GetDirectoryName(
            //    System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\log.txt"))
            //{
            //    w.WriteLine("[{0}][{1}] {2} ", String.Format("{0:yyyyMMdd HH:mm:ss}", DateTime.Now), "Info", message);
            //    w.Flush();
            //}
        }
    }
}
