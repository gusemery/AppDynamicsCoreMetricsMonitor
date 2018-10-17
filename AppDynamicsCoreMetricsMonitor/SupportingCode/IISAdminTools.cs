using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDynamicsCoreMetricsMonitor.SupportingCode
{
    public static class IISAdminTools
    {
        public static string GetAppPoolName(int pid)
        {
            try
            {
                ServerManager manager = new ServerManager();
                Site defaultSite = manager.Sites["Default Web Site"];

                foreach (Application app in defaultSite.Applications)
                {
                    
                    Console.WriteLine(
                        "{0} is assigned to the '{1}' application pool.",
                        app.Path, app.ApplicationPoolName);
                }
            }
            catch (Exception)
            {

                throw;
            }

            return string.Empty;
        }
        public static List<string> GetApplicationPools()
        {
            List<string> result = new List<string>();
            try
            {
                using (var serverManager = new ServerManager())
                {
                    result = serverManager.ApplicationPools.Select(x => x.Name).ToList();
                }
            }
            catch(Exception)
            {
                throw;
            }
            return result;
        }

        public static List<int> GetAppPoolProcesses(string appPoolName)
        {
            List<int> result = new List<int>();

            using (var serverManager = new ServerManager())
            {
                var appPool = serverManager.ApplicationPools[appPoolName];
                //foreach (var appPool in serverManager.ApplicationPools)
                //{
                    if (appPool.Name == appPoolName)
                    {
                        foreach (var workerProcess in appPool.WorkerProcesses)
                        {
                            result.Add(workerProcess.ProcessId);
                        }
                    }
                //}
            }

            return result;
        }
    }
}
