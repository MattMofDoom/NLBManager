using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Web;
using Lurgle.Logging;
// ReSharper disable InconsistentNaming

namespace NLBManager.Classes
{
    public static class Common
    {
        public const string NlbStart = "Start";
        public const string NlbStop = "Stop";
        public const string LogServiceName = "Configured Service to watch: {ServiceName:l}, Running: {ServiceRunning}";

        public const string LogClusterName =
            "Configured Cluster to control: {ClusterName:l}, Cluster IP: {ClusterIp:l}, Current State: {NLBStatus}";

        public const string LogServiceState =
            "Service: {ServiceName:l}, Started: {ServiceStarted}, NLB Status: {NLBStatus}, Perform NLB Action: {NLBAction:l}";

        public const string LogServiceNotFound = "Service not found: {ServiceName:l}, Stopping ...";
        public const string LogClusterNotFound = "Target cluster not found: {ClusterName:l}, Stopping ...";
        public const string LogClusterIpNotFound = "Target cluster IP address not found: {ClusterName:l}, Stopping ...";
        public const string LogError = "Error: Exception {Exception:l}";
        public const string LogStart = "{Service:l} v{Version:l} Started";
        public const string LogStop = "{Service:l} v{Version:l} Stopped";
        public const string LogStopError = "{Service:l} v{Version:l} Stopped (Error: {Error})";
        private const string LogUnexpectedStatusCode = "Unexpected NLB status code: {StatusCode:l}";

        static Common()
        {
            //Using GetEntryAssembly will fail under ASP.NET, so we fall back to using HttpContext and then GetExecutingAssembly
            var isSuccess = true;
            try
            {
                AppName = Assembly.GetEntryAssembly()?.GetName().Name;
                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
            }
            catch
            {
                isSuccess = false;
            }

            //This might be a web application
            if (!isSuccess)
                try
                {
                    var memberInfo = HttpContext.Current.ApplicationInstance.GetType().BaseType;
                    if (memberInfo != null)
                    {
                        AppName = memberInfo.Assembly.GetName().Name;
                        AppVersion = memberInfo.Assembly.GetName()
                            .Version
                            .ToString();
                    }

                    isSuccess = true;
                }
                catch
                {
                    isSuccess = false;
                }

            if (!isSuccess)
                try
                {
                    AppName = Assembly.GetExecutingAssembly().GetName().Name;
                    AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                catch
                {
                    //We surrender ...
                    AppName = string.Empty;
                    AppVersion = string.Empty;
                }
        }

        public static string AppName { get; }
        public static string AppVersion { get; }

        public static string GetClusterIp(string clusterName)
        {
            try
            {
                var nlbScope = new ManagementScope(@"root\MicrosoftNLB");
                var nlbCluster = new ManagementClass(nlbScope.Path.Path, "MicrosoftNLB_ClusterSetting", null);
                foreach (var o in nlbCluster.GetInstances())
                {
                    var nlb = (ManagementObject) o;
                    var name = (string) nlb.GetPropertyValue("ClusterName");
                    var ip = (string) nlb.GetPropertyValue("ClusterIPAddress");
                    if (name.Equals(clusterName)) return ip;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
                return string.Empty;
            }
        }

        public static ManagementObject GetClusterNode(string clusterIp)
        {
            try
            {
                var nlbScope = new ManagementScope(@"root\MicrosoftNLB");
                var nlbClass = new ManagementClass(nlbScope.Path.Path, "MicrosoftNLB_Node", null);
                foreach (var o in nlbClass.GetInstances())
                {
                    var nlb = (ManagementObject) o;
                    //var nodeName = (string) nlb.GetPropertyValue("Name");
                    var nodeIp = (string) nlb.GetPropertyValue("DedicatedIPAddress");
                    if (GetClusterNodeIps(clusterIp).Contains(nodeIp)) return nlb;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
                return null;
            }
        }

        public static NlbStatus GetNlbStatus(ManagementObject nlb)
        {
            try
            {
                var nlbStatusCode = nlb.GetPropertyValue("StatusCode").ToString();
                if (Enum.TryParse(nlbStatusCode, out NlbStatus nlbStatus)) return nlbStatus;

                Log.Error().Add(LogUnexpectedStatusCode, nlbStatusCode);
                return NlbStatus.Unknown;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
                return NlbStatus.Unknown;
            }
        }

        public static void StartNlb(ManagementObject nlb)
        {
            try
            {
                nlb.InvokeMethod(NlbStart, null);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
            }
        }

        public static void StopNlb(ManagementObject nlb)
        {
            try
            {
                nlb.InvokeMethod(NlbStop, null);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
            }
        }

        private static List<string> GetClusterNodeIps(string clusterIp)
        {
            try
            {
                var ipClass = new ManagementClass("Win32_NetworkAdapterConfiguration");
                foreach (var ip in ipClass.GetInstances().Cast<ManagementObject>()
                    .Where(mgmt => (bool) mgmt["IPEnabled"]))
                {
                    var ipAddresses = ((string[]) ip.GetPropertyValue("IPAddress")).ToList();
                    if (ipAddresses.ToList().Contains(clusterIp)) return ipAddresses;
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
                return new List<string>();
            }
        }

        public static ManagementObject GetService(string serviceName)
        {
            try
            {
                var serviceClass = new ManagementClass("Win32_Service");
                foreach (var service in serviceClass.GetInstances().Cast<ManagementObject>().Where(mgmt =>
                    ((string) mgmt["Name"]).Equals(serviceName, StringComparison.CurrentCultureIgnoreCase)))
                    return service;

                return null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(LogError, ex.Message);
                return null;
            }
        }
    }
}