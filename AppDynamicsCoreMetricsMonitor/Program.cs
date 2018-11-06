using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppDynamicsCoreMetricsMonitor.SupportingCode;
using AppDynamicsCoreMetricsMonitor.EventMonitors;
namespace AppDynamicsCoreMetricsMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            var ob = new ObserveGCEvents();
            ob.Run();
            //Console.ReadKey();
        }
        private static string GetpIDs(List<int> values)
        {
            string result = string.Empty;
            foreach(int i in values)
            {
                result = result + i.ToString() + ",";
            }
            return result;
        }
        private static void Dictionary(string v, List<int> list)
        {
            throw new NotImplementedException();
        }
    }
}
