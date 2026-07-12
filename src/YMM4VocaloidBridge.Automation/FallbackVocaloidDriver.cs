namespace YMM4VocaloidBridge.Automation;

public sealed class FallbackVocaloidDriver(
    IVocaloidDriver automaticDriver,
    IVocaloidDriver assistedDriver,
    Func<VocaloidAutomationException, Task>? automaticFailureObserver = null) : IVocaloidDriver
{
    public async Task<VocaloidRenderResult> RenderAsync(
        VocaloidRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await automaticDriver.RenderAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (VocaloidAutomationException exception)
        {
            if (automaticFailureObserver is not null)
            {
                await automaticFailureObserver(exception).ConfigureAwait(false);
            }

            try
            {
                var fallback = await assistedDriver.RenderAsync(request, cancellationToken).ConfigureAwait(false);
                return fallback with
                {
                    UsedFallback = true,
                    Events = ["automatic-failed:" + exception.Message, .. fallback.Events],
                };
            }
            catch (Exception fallbackException) when (fallbackException is not OperationCanceledException)
            {
                throw new VocaloidAutomationException(
                    $"Automatic mode failed: {exception.Message} Assisted mode also failed: {fallbackException.Message}",
                    new AggregateException(exception, fallbackException));
            }
        }
    }
}
