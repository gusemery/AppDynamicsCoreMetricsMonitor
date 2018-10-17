using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;

namespace AppDynamicsCoreMetricsMonitor.SupportingCode
{
    public static class ConfigTools
    {
        public static List<string> GetConfiguredAppPools()
        {
            var result = new List<string>();
            var appPoolSettings = ConfigurationManager.GetSection("MonitorSettings/appPools") as NameValueCollection;
            if (appPoolSettings.Count == 0)
            {
                Console.WriteLine("Post Settings are not defined");
                return new List<string>(); 
                // throw new NotImplementedException("No app pools are defined for this application to monitor....");
            }
            else
            {
                foreach (var key in appPoolSettings.AllKeys)
                {
                    var isMonitored = appPoolSettings[key];
                    if (isMonitored.ToLowerInvariant() == "true")
                    {
                        result.Add(key);
                    }
                }
            }
            return result;
        }

        public static List<string> GetConfiguredApps()
        {
            var result = new List<string>();
            var appSettings = ConfigurationManager.GetSection("MonitorSettings/Applications") as NameValueCollection;
            if (appSettings.Count == 0)
            {
                Console.WriteLine("Post Settings are not defined");
                return new List<string>();
                //throw new NotImplementedException("No applications are defined for this application to monitor....");
            }
            else
            {
                foreach (var key in appSettings.AllKeys)
                {
                    var isMonitored = appSettings[key];
                    if (isMonitored.ToLowerInvariant() == "true")
                    {
                        result.Add(key);
                    }
                }
            }
            //if (GetConfiguredAppPools().Count > 0)
            //    result.Add("w3wp");
            return result;
        }
    }
}
