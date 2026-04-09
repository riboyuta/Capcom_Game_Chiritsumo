public sealed partial class PlayerController
{
    public void NotifyExternalLaunch()
    {
        frameRequests.wasExternallyLaunchedThisFrame = true;
    }
}