using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NvkCommon.Utils;

namespace NvkCommon
{
    /// <summary>
    /// ClickOnce changed a bit between .Net Framework (FX) and .Net Core
    /// ClickOnce support was missing in .Net Core until .Net Core 7.
    /// Only properties were added:
    /// * https://learn.microsoft.com/en-us/visualstudio/deployment/access-clickonce-deployment-properties-dotnet
    /// The `CurrentDeployment` class was removed.
    /// 
    /// Furthermore, and very importantly, Strong Naming is required in order for Settings to persist after an update:
    /// * https://developercommunity.visualstudio.com/t/ApplicationSettingsBaseUpgrade-Method-n/1672135#T-N10009556
    /// 
    /// To Strong Name an app:
    /// 1. `sn -k MyApp.snk`
    /// 2. Project Properties -> Build -> Strong Naming -> Sign the assembly -> Enable and browse to the .snk file
    /// </summary>
    public class ApplicationDeployment
    {
        public static readonly string TAG = Log.TAG(typeof(ApplicationDeployment));

        public static bool IsNetworkDeployed
        {
            get
            {
                var isNetworkDeployed = Environment.GetEnvironmentVariable("ClickOnce_IsNetworkDeployed");
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"IsNetworkDeployed: {Quote(isNetworkDeployed)}");
                return isNetworkDeployed?.ToLower() == "true";
            }
        }

        public static Version CurrentVersion
        {
            get
            {
                var version = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion");
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"CurrentVersion: {Quote(version)}");
                return (version != null) ? new Version(version) : null;
            }
        }

        public static string DataDirectory
        {
            get
            {
                var dataDirectory = Environment.GetEnvironmentVariable("ClickOnce_DataDirectory");
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"DataDirectory: {Quote(dataDirectory)}");
                return dataDirectory;
            }
        }

        public static bool IsFirstRun
        {
            get
            {
                var isFirstRun = Environment.GetEnvironmentVariable("ClickOnce_IsFirstRun");
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"IsFirstRun: {Quote(isFirstRun)}");
                return isFirstRun?.ToLower() == "true";
            }
        }
    }
}
