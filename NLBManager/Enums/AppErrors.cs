namespace NLBManager
{
    // ReSharper disable UnusedMember.Global
    /// <summary>
    ///     Enum of the reasons that can cause the app to stop with an error
    /// </summary>
    public enum AppErrors
    {
        None = 0,
        ServiceNotFound = 1,
        ClusterNotFound = 2,
        ClusterIpNotFound = 3,
        Unknown = -1
    }
}