using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    internal class BindingInformation
    {
        private const string BindingNameKey = "name";
        private const string BindingDirectionKey = "direction";
        private const string BindingTypeKey = "type";
        public enum Directions
        {
            Unknown = -1,
            In = 0,
            Out = 1, 
            Inout = 2
        }

        public Directions Direction { get; set; } = Directions.Unknown;
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public Dictionary<string, Object> otherInformation { get; set; } = new Dictionary<string, Object>();

        internal string ConvertToRpcRawBinding(out BindingInfo bindingInfo)
        {
            string rawBinding = string.Empty;
            JObject rawBindingObject = new JObject();
            rawBindingObject.Add(BindingNameKey, Name);
            BindingInfo outInfo = new BindingInfo();


            if (Direction == Directions.Unknown)
            {
                throw new Exception(PowerShellWorkerStrings.InvalidBindingInfoDirection);
            }
            outInfo.Direction = (BindingInfo.Types.Direction)Direction;
            rawBindingObject.Add(BindingDirectionKey, Enum.GetName(typeof(BindingInfo.Types.Direction), outInfo.Direction).ToLower());
            outInfo.Type = Type;
            rawBindingObject.Add(BindingTypeKey, Type);

            foreach (KeyValuePair<string, Object> pair in otherInformation)
            {
                // Wow this sucks, lots of overserialization
                rawBindingObject.Add(pair.Key, JToken.FromObject(pair.Value));
                //Console.WriteLine(pair.Key);
            }

            rawBinding = JsonConvert.SerializeObject(rawBindingObject);
            bindingInfo = outInfo;
            return rawBinding;
        }
    }
}
