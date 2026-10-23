namespace VinceProductivityTray.Setup;

internal enum LaunchAfterInstallResult { Declined, Started, Failed }

internal static class InstallCompletion
{
    // Called only after the install transaction has completed successfully.
    // A launch failure must not turn a completed installation into a rollback.
    internal static LaunchAfterInstallResult OfferLaunch(
        Func<bool> ask, Action launch, Action<Exception> reportFailure)
    {
        if (!ask()) return LaunchAfterInstallResult.Declined;
        try
        {
            launch();
            return LaunchAfterInstallResult.Started;
        }
        catch (Exception exception)
        {
            reportFailure(exception);
            return LaunchAfterInstallResult.Failed;
        }
    }
}
