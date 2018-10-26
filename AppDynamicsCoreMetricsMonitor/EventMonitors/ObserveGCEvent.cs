using AppDynamicsCoreMetricsMonitor.SupportingCode;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;

namespace AppDynamicsCoreMetricsMonitor.EventMonitors

{
    class ObserveGCEvents
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        // TextWriter Out = new StreamWriter("e:\\Logs\\GCOutput.txt");
         TextWriter Out = Console.Out;
         Dictionary<string, List<int>> appPools = new Dictionary<string, List<int>>();
         Uri targetUri = new Uri(ConfigurationManager.AppSettings["AnalyticsListener"]);
         HttpClient client = new HttpClient();
         //int count = 0;
         List<MetricPackage> myMetrics = new List<MetricPackage>();

        private MetricPackage CreateMetricPackage(string metric, long value)
        {
            return new MetricPackage() { aggregatorType = "AVERAGE", metricName = metric, value = value };
        }

        public void Run()
        {

            var monitoredAppPools = ConfigTools.GetConfiguredAppPools();
            var monitoredApps = ConfigTools.GetConfiguredApps();
            var console = Boolean.Parse(ConfigurationManager.AppSettings["ConsoleOutput"]);
            var api = Boolean.Parse(ConfigurationManager.AppSettings["APIOutput"]);

            if (client == null)
                client.BaseAddress = targetUri;


            if (TraceEventSession.IsElevated() != true)
            {
                Console.WriteLine("Must be elevated (Admin) to run this method.");
                Debugger.Break();
                return;
            }


            try
            {
                var appPoolNames = IISAdminTools.GetApplicationPools();

                foreach (var appPoolName in appPoolNames)
                {
                    appPools.Add(appPoolName, IISAdminTools.GetAppPoolProcesses(appPoolName));
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("not registered"))
                {
                    Console.WriteLine("Could not initialize; IIS isn't installed upon this machine.");
                }
                Debugger.Break();
            }

            //var monitoringTimeSec = 60;
            //Console.WriteLine("The monitor will run for a maximum of {0} seconds", monitoringTimeSec);
            //Console.WriteLine("Press Ctrl-C to stop monitoring of GC Allocs");

            // create a real time user mode session
            using (var userSession = new TraceEventSession("ObserveGCAllocs"))
            {
                // Set up Ctrl-C to stop the session
                SetupCtrlCHandler(() => { userSession.Stop(); });

                // enable the CLR provider with default keywords (minus the rundown CLR events)
                userSession.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose,
                    (ulong)(ClrTraceEventParser.Keywords.GC));

                // Create a stream of GC Allocation events (happens every time 100K of allocations happen)
                IObservable<GCAllocationTickTraceData> gcAllocStream = userSession.Source.Clr.Observe<GCAllocationTickTraceData>();

                // Print the outgoing stream to the console
                //gcAllocStream.Subscribe(allocData =>
                //{
                //    if (GetProcessName(allocData.ProcessID) == "devenv")
                //    {
                //        //Out.WriteLine("GC Alloc  :  Proc: {0,10}({1,3}) Amount: {2,6:f1}K  TypeSample: {3}", GetProcessName(allocData.ProcessID), GetProcessCPU(allocData.ProcessID), allocData.AllocationAmount / 1000.0, allocData.TypeName);
                //    }
                //});

                // Create a stream of GC Collection events
                IObservable<GCHeapStatsTraceData> gcCollectStream = userSession.Source.Clr.Observe<GCHeapStatsTraceData>();

                // Print the outgoing stream to the console
                gcCollectStream.Subscribe(collectData =>
                {
                    var appName = GetProcessName(collectData.ProcessID);
                    var metricsList = new List<MetricPackage>();
                    //Out.WriteLine("Application Name : {0}", appName);
                    if (DoesProcessIdExist(collectData.ProcessID))
                    {
                        var appPoolName = GetAppPoolName(collectData.ProcessID);
                        //Out.WriteLine("App Pool Name : {0}", appPoolName);
                        if (monitoredAppPools.Contains(appPoolName))
                        {
                            int pid = collectData.ProcessID;
                            Process toMonitor = Process.GetProcessById(pid);
                            long memoryUsed = toMonitor.WorkingSet64;
                            long momoryCommitted = toMonitor.PeakWorkingSet64;
                            var machineName = System.Environment.MachineName;
                            if (api)
                            {
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appPoolName, "Memory Heap - Gen 0 Usage"), collectData.GenerationSize0));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appPoolName, "Memory Heap - Gen 1 Usage"), collectData.GenerationSize1));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appPoolName, "Memory Heap - Gen 2 Usage"), collectData.GenerationSize2));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appPoolName, "Large Object Heap - Current Usage"), collectData.GenerationSize3));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|Usage Metrics|{2}", machineName, appPoolName, "Current Usage"), memoryUsed));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|Usage Metrics|{2}", machineName, appPoolName, "Current Committed"), momoryCommitted));

                            }
                            if (console)
                            {
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Memory Heap - Gen 0 Usage", collectData.GenerationSize0);
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Memory Heap - Gen 1 Usage", collectData.GenerationSize1);
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Memory Heap - Gen 2 Usage", collectData.GenerationSize2);
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Large Object Heap - Current Usage", collectData.GenerationSize3);
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Current Usage", memoryUsed);
                                Out.WriteLine("name = Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appPoolName, "Current Committed", momoryCommitted);
                            }
                        }
                    }
                    else if (monitoredApps.Contains(appName))
                    {

                        {
                            var machineName = System.Environment.MachineName;
                            int pid = collectData.ProcessID;
                            Process toMonitor = Process.GetProcessById(pid);
                            long memoryUsed = toMonitor.WorkingSet64;
                            long momoryCommitted = toMonitor.PeakWorkingSet64;

                            if (api)
                            {

                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appName, "Memory Heap - Gen 0 Usage"), collectData.GenerationSize0));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appName, "Memory Heap - Gen 1 Usage"), collectData.GenerationSize1));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appName, "Memory Heap - Gen 2 Usage"), collectData.GenerationSize2));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}", machineName, appName, "Large Object Heap - Current Usage"), collectData.GenerationSize3));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|Usage Metrics|{2}", machineName, appName, "Current Usage"), memoryUsed));
                                metricsList.Add(CreateMetricPackage(string.Format("Custom Metrics|Memory|Nodes|{0}|{1}|Usage Metrics|{2}", machineName, appName, "Current Committed"), momoryCommitted));
                            }
                            if (console)
                            {
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Memory Heap - Gen 0 Usage", collectData.GenerationSize0);
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Memory Heap - Gen 1 Usage", collectData.GenerationSize1);
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Memory Heap - Gen 2 Usage", collectData.GenerationSize2);
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Large Object Heap - Current Usage", collectData.GenerationSize3);
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Current Usage", memoryUsed);
                                Out.WriteLine("name=Custom Metrics|Memory|Nodes|{0}|{1}|GC Metrics|{2}, value={3}", machineName, appName, "Current Committed", momoryCommitted);
                            }
                        }

                        //    Out.WriteLine("GC Collect:  Proc: {0,10}({1,3}) Gen0: {2,6:f1}M Gen1: {3,6:f1}M Gen2: {4,6:f1}M LargeObj: {5,6:f1}M",
                        //         GetProcessName(collectData.ProcessID),
                        //         GetProcessCPU(collectData.ProcessID),
                        //         collectData.GenerationSize0 / 1000000.0,
                        //         collectData.GenerationSize1 / 1000000.0,
                        //         collectData.GenerationSize2 / 1000000.0,
                        //         collectData.GenerationSize3 / 1000000.0);
                    }
                    if (metricsList.Count > 0)
                        WriteLines(metricsList);
                });

                //IObservable<long> timer = Observable.Timer(new TimeSpan(0, 0, monitoringTimeSec));
                //timer.Subscribe(delegate
                //{
                //    Console.WriteLine("Stopped after {0} sec", monitoringTimeSec);
                //    userSession.Dispose();
                //});

                // OK we are all set up, time to listen for events and pass them to the observers.  
                userSession.Source.Process();
            }

            Console.WriteLine("Done with program.");

        }
        private bool DoesProcessIdExist(int processID)
        {
            var result = false;
            foreach (var ap in appPools)
            {
                if (result)
                    return result;
                result = ap.Value.Contains(processID);
            }
            return result;
        }
        private  string GetAppPoolName(int processID)
        {
            var result = string.Empty;
            foreach (var ap in appPools)
            {
                if (ap.Value.Contains(processID))
                {
                    return ap.Key;
                }
            }
            return result;
        }
        /// <summary>
        /// Returns the process name for a given process ID
        /// </summary>
        private  string GetProcessName(int processID)
        {
            // Only keep the cache for 10 seconds to avoid issues with process ID reuse.  
            var now = DateTime.UtcNow;
            if ((now - s_processNameCacheLastUpdate).TotalSeconds > 10)
                s_processNameCache.Clear();
            s_processNameCacheLastUpdate = now;

            string ret = null;
            if (!s_processNameCache.TryGetValue(processID, out ret))
            {
                Process proc = null;
                try { proc = Process.GetProcessById(processID); }
                catch (Exception) { }
                if (proc != null)
                    ret = proc.ProcessName;
                if (string.IsNullOrWhiteSpace(ret))
                    ret = processID.ToString();
                s_processNameCache.Add(processID, ret);
            }
            return ret;
        }
        private  float GetProcessCPU(int processID)
        {
            
            var process = Process.GetProcessById(processID);

            var total_cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            var process_cpu = new PerformanceCounter("Process", "% Processor Time", GetProcessName(processID));
            var processUsage = (total_cpu.NextValue() / 100) * process_cpu.NextValue(); // process_cpu.NextValue() / Environment.ProcessorCount;
            var process_cpu_usage = (total_cpu.NextValue() / 100) * process_cpu.NextValue();
            return processUsage;

        }
        private  Dictionary<int, string> s_processNameCache = new Dictionary<int, string>();
        private  DateTime s_processNameCacheLastUpdate;


        private async  void WritetoAppD(List<MetricPackage> myMetrics)
        {
            try
            {
                if (client == null)
                {
                    client = new HttpClient();
                }

                if (client.BaseAddress != targetUri)
                {
                    client.BaseAddress = targetUri;
                }

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                HttpResponseMessage response = await client.PostAsJsonAsync(@"api/v1/metrics", myMetrics.ToArray());
                Out.WriteLine("Sent {0} metrics with status code {1}",myMetrics.Count, response.StatusCode);
                if(response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                {
                    Out.WriteLine(response.ReasonPhrase);
                    Out.WriteLine("Response Content : {0} \n requestContent : {1}",response.Content.ToString(), response.RequestMessage.Content.ToString());
                }
            }
            catch (Exception ex)
            {
                Out.WriteLine("Error : {0}", ex.Message);
            }
        }
        private void WriteLines(List<MetricPackage> metrics)
        {
            WritetoAppD(metrics);
        }

        //private   void WriteLine(string metric, long value)
        //{
            
        //    var obj = new object();
        //    lock (obj)
        //    {
        //        count++;
        //        //List<MetricPackage> myMetrics = new List<MetricPackage>();
        //        myMetrics.Add(new MetricPackage() { aggregatorType = "AVERAGE", metricName = metric, value = value });
        //        if (count > 10)
        //        {
        //            WritetoAppD(myMetrics);
        //            myMetrics.Clear();
        //            count = 0;
        //        }
        //    }
        //}

        #region Console CtrlC handling
        private  bool s_bCtrlCExecuted;
        private  ConsoleCancelEventHandler s_CtrlCHandler;
        /// <summary>
        /// This implementation allows one to call this function multiple times during the
        /// execution of a console application. The CtrlC handling is disabled when Ctrl-C 
        /// is typed, one will need to call this method again to re-enable it.
        /// </summary>
        /// <param name="action"></param>
        private  void SetupCtrlCHandler(Action action)
        {
            s_bCtrlCExecuted = false;
            // uninstall previous handler
            if (s_CtrlCHandler != null)
                Console.CancelKeyPress -= s_CtrlCHandler;

            s_CtrlCHandler =
                (object sender, ConsoleCancelEventArgs cancelArgs) =>
                {
                    if (!s_bCtrlCExecuted)
                    {
                        s_bCtrlCExecuted = true;    // ensure non-reentrant

                        Out.WriteLine("Stopping monitor");

                        action();                   // execute custom action

                        // terminate normally (i.e. when the monitoring tasks complete b/c we've stopped the sessions)
                        cancelArgs.Cancel = true;
                    }
                };
            Console.CancelKeyPress += s_CtrlCHandler;
        }
        #endregion
    }
}