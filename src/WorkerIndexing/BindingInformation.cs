using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Functions.PowerShellWorker.WorkerIndexing
{
    internal class BindingInformation
    {
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
            rawBindingObject.Add("name", Name);
            BindingInfo outInfo = new BindingInfo();


            if (Direction == Directions.Unknown)
            {
                throw new Exception("The bindingInfo's Direction is not valid");
            }
            outInfo.Direction = (BindingInfo.Types.Direction)Direction;
            rawBindingObject.Add("direction", Enum.GetName(typeof(BindingInfo.Types.Direction), outInfo.Direction).ToLower());
            outInfo.Type = Type;
            rawBindingObject.Add("type", Type);

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
