using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace AzureFunctionsHelpers
{
    internal class BindingInformation
    {
        public enum Directions
        {
            In = 0,
            Out = 1, 
            Inout = 2
        }

        public int Direction { get; set; } = -1;
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public Dictionary<string, Object> otherInformation { get; set; } = new Dictionary<string, Object>();

        internal string ConvertToRpcRawBinding(out BindingInfo bindingInfo)
        {
            string rawBinding = string.Empty;
            JObject rawBindingObject = new JObject();
            BindingInfo outInfo = new BindingInfo();


            if (!Enum.IsDefined(typeof(BindingInfo.Types.Direction), Direction))
            {
                throw new Exception("The bindingInfo's Direction is not valid");
            }
            outInfo.Direction = (BindingInfo.Types.Direction)Direction;
            rawBindingObject.Add("Direction", Direction);
            outInfo.Type = Type;
            rawBindingObject.Add("Type", Type);

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
