using System;
using System.ServiceProcess;
using System.Threading;
using Lurgle.Logging;
using NLBManager.Classes;
using Timer = System.Timers.Timer;
// ReSharper disable InconsistentNaming

namespace NLBManager
{
    public partial class NlbManager : ServiceBase
    {
        private const int Available = 0;
        private const int Locked = 1;
        private bool _lastServiceState;
        private int _serviceLockState;

        private Timer _serviceTimer;

        public NlbManager()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                Logging.AddCommonProperty("ServiceName", Config.ServiceName);
                Logging.AddCommonProperty("TargetCluster", Config.TargetCluster);
                Logging.AddCommonProperty("ServiceTick", Config.ServiceTick);
                Logging.Init();

                //Get the current service state
                var service = Common.GetService(Config.ServiceName);
                if (service != null)
                {
                    _lastServiceState = (bool) service.GetPropertyValue("Started");
                }
                else
                {
                    Log.Information().Add(Common.LogServiceNotFound, Config.ServiceName);
                    StopError(AppErrors.ServiceNotFound);
                }

                Log.Information().Add(Common.LogServiceName, Config.ServiceName, _lastServiceState);
                var clusterIp = Common.GetClusterIp(Config.TargetCluster);
                Log.Information().Add(Common.LogClusterName, Config.TargetCluster, clusterIp,
                    Common.GetNlbStatus(Common.GetClusterNode(clusterIp)));

                //Force NLB to match the current service state
                CheckNlb(_lastServiceState, true);

                _serviceTimer = new Timer
                {
                    Interval = Config.ServiceTick,
                    AutoReset = true
                };
                _serviceTimer.Elapsed += ServiceTick;
                _serviceTimer.Start();

                Log.Information().Add(Common.LogStart, Common.AppName, Common.AppVersion);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(Common.LogError, ex.Message);
            }
        }

        protected override void OnStop()
        {
            try
            {
                _serviceTimer.Stop();
                Log.Information().Add(Common.LogStop, Common.AppName, Common.AppVersion);
                Logging.Close();
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(Common.LogError, ex.Message);
                Logging.Close();
            }
        }

        private void CheckNlb(bool serviceStarted, bool overrideServiceState = false)
        {
            var clusterIp = Common.GetClusterIp(Config.TargetCluster);

            if (!string.IsNullOrEmpty(clusterIp))
            {
                var nlb = Common.GetClusterNode(clusterIp);
                if (nlb != null)
                {
                    if (serviceStarted.Equals(_lastServiceState) && !overrideServiceState && !Config.ServiceMode.Equals(ServiceMode.SuspendMode)) return;
                    var nlbStatus = Common.GetNlbStatus(nlb);
                    switch (serviceStarted)
                    {
                        case true:
                            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                            switch (nlbStatus)
                            {
                                case NlbStatus.Stopped:
                                    Log.Information().Add(Common.LogServiceState,
                                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                        Config.ServiceName, serviceStarted, nlbStatus, Common.NlbStart);
                                    Common.StartNlb(nlb);
                                    break;
                            }

                            break;
                        case false:
                            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                            switch (nlbStatus)
                            {
                                case NlbStatus.Converged:
                                case NlbStatus.DefaultHost:
                                    Log.Information().Add(Common.LogServiceState,
                                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                        Config.ServiceName, serviceStarted, nlbStatus, Common.NlbStop);
                                    Common.StopNlb(nlb);
                                    break;
                            }

                            break;
                    }
                }
                else
                {
                    Log.Warning().Add(Common.LogClusterNotFound, Config.TargetCluster);
                    StopError(AppErrors.ClusterNotFound);
                }
            }
            else
            {
                Log.Warning().Add(Common.LogClusterIpNotFound, Config.TargetCluster);
                StopError(AppErrors.ClusterIpNotFound);
            }
        }

        private void ServiceTick(object sender, EventArgs e)
        {
            //Attempt to obtain an exclusive lock so that successive timer loops can't clobber a running loop
            var currentState = Interlocked.CompareExchange(ref _serviceLockState, Locked, Available);

            if (currentState != Available) return;
            try
            {
                var serviceStarted = false;

                var service = Common.GetService(Config.ServiceName);
                if (service != null)
                {
                    serviceStarted = (bool) service.GetPropertyValue("Started");
                }
                else
                {
                    Log.Warning().Add(Common.LogServiceNotFound, Config.ServiceName);
                    StopError(AppErrors.ServiceNotFound);
                }

                CheckNlb(serviceStarted);
                _lastServiceState = serviceStarted;

                service?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(Common.LogError, ex.Message);
            }

            // ReSharper disable once RedundantAssignment
            currentState = Interlocked.CompareExchange(ref _serviceLockState, Available, Locked);
        }

        /// <summary>
        ///     Perform cleanup operations and exit with an error
        /// </summary>
        /// <param name="errorType"></param>
        private void StopError(AppErrors errorType)
        {
            try
            {
                _serviceTimer.Stop();
                Log.Error()
                    .Add(Common.LogStopError, Common.AppName, Common.AppVersion, errorType);
                Logging.Close();
                Environment.Exit((int) errorType);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(Common.LogError, ex.Message);
                Logging.Close();
                Environment.Exit(ex.HResult);
            }
        }
    }
}