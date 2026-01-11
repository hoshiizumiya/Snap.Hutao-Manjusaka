// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.IO;
using System.IO;

namespace Snap.Hutao.Core.Shell;

[Service(ServiceLifetime.Transient, typeof(IShellLinkInterop))]
internal sealed class ShellLinkInterop : IShellLinkInterop
{
    public bool TryCreateDesktopShortcutForElevatedLaunch()
    {
        string targetLogoPath = HutaoRuntime.GetDataDirectoryFile("ShellLinkLogo.ico");
        string elevatedLauncherPath = Environment.ProcessPath ?? string.Empty;

        try
        {
            InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Assets/Logo.ico", targetLogoPath);
            // Moved for unpackaged deployment
            // InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Snap.Hutao.Elevated.Launcher.exe", elevatedLauncherPath);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string target = Path.Combine(desktop, $"{SH.AppName}.lnk");

            // Always point the shortcut to the elevated launcher executable and pass FamilyName as argument.
            // The elevated launcher will interpret the argument to activate packaged app when appropriate.
            FileSystem.CreateLink(elevatedLauncherPath, String.Empty, targetLogoPath, target);

            return true;
        }
        catch
        {
            return false;
        }
    }
}