using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDynamicsCoreMetricsMonitor
{
    public class appPoolsConfig : ConfigurationSection
    {
        [ConfigurationProperty("appPool")]
        public appPoolConfig AppPool
        {
            get { return (appPoolConfig)this["AppPool"]; }
        }
    }
}
