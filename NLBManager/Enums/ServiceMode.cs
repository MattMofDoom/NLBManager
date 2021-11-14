namespace NLBManager
{
    public enum ServiceMode
    {
        /// <summary>
        /// NLB will not be started if it is stopped manually while the service is running
        /// </summary>
        Default,
        /// <summary>
        /// NLB will always be started if the service is running, unless the node is suspended
        /// </summary>
        SuspendMode,
    }
}
