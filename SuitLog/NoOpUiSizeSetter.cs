namespace SuitLog;

public class NoOpUiSizeSetter : UiSizeSetterShipLogFact
{
    public override void Awake()
    {
        // No-op, to avoid getting text component (this is the parent of both texts)
    }

    public override void DoResizeAction(UITextSize textSizeSetting)
    {
       // No-op
    }
}
