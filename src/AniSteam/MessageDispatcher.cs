namespace AniSteam;

public sealed class MessageDispatcher : Control
{
    public MessageDispatcher() { Visible = false; }

    protected override CreateParams CreateParams
    {
        get {
            var parameters = base.CreateParams;
            parameters.Parent = new IntPtr(-3);
            return parameters;
        }
    }
}
