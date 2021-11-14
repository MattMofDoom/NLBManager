using System.ServiceProcess;

namespace NLBManager
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            var servicesToRun = new ServiceBase[]
            {
                new NlbManager()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}