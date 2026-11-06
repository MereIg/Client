using System;
using Godot;

public partial class ImportButton : Button
{
    private FileDialog importDialog;

    public override void _Ready()
    {
        importDialog = GetNode<FileDialog>("ImportDialog");

        if (Mobile.IsAndroid)
        {
            // Godot's own picker gives real file paths, which the map parser needs
            importDialog.UseNativeDialog = false;
            importDialog.Access = FileDialog.AccessEnum.Filesystem;
        }
    }

    public override void _Pressed()
    {
        if (!Mobile.IsAndroid)
        {
            importDialog.Show();
            return;
        }

        if (!Mobile.HasStorageAccess())
        {
            _ = ToastNotification.Notify("Allow \"All files access\" for Rhythia, then tap Import again");
            Mobile.RequestStorageAccess();
            return;
        }

        importDialog.CurrentDir = Mobile.DOWNLOADS_FOLDER;
        importDialog.PopupCentered((Vector2I)(GetTree().Root.GetVisibleRect().Size * 0.9f));
    }
}
