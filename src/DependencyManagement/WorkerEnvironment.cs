using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.PowerShellWorker.DependencyManagement
{
    /// <summary>
    /// Hold information about the environment.
    /// </summary>
    internal static class WorkerEnvironment
    {
        // Environment variables to help figure out if we are running in Azure.
        private const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";
        private const string ContainerName = "CONTAINER_NAME";
        private const string LegionServiceHost = "LEGION_SERVICE_HOST";

        private static readonly DateTime PowerShellSDKDeprecationDate = new DateTime(2024, 11, 8);

        public static bool IsAppService()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AzureWebsiteInstanceId));
        }

        public static bool IsLinuxConsumption()
        {
            return !IsAppService() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ContainerName));
        }

        public static bool IsLinuxConsumptionOnLegion()
        {
            return !IsAppService() &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ContainerName)) &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LegionServiceHost));
        }

        public static bool IsPowerShellSDKDeprecated()
        {
            return DateTime.Now > PowerShellSDKDeprecationDate;
        }
    }
}
