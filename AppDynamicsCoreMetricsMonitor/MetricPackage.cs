using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDynamicsCoreMetricsMonitor
{
    public class MetricPackage
    {
        public string metricName { get; set; }
        public string aggregatorType { get; set; }
        public long value { get; set; }
    }
}
