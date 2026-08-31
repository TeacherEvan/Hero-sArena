using Godot;

public partial class CheckProcessMode : SceneTree
{
    public override void _Initialize()
    {
        var n = new Node2D();
        n.ProcessMode = Node.ProcessModeEnum.Disabled;
        GD.Print(n.ProcessMode);
        Quit();
    }
}
