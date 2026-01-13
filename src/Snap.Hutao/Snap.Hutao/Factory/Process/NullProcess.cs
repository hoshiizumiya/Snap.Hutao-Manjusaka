// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Diagnostics;
using Snap.Hutao.Win32.Foundation;

namespace Snap.Hutao.Factory.Process;

internal sealed class NullProcess : IProcess
{
    public int Id => 0;

    public nint Handle => 0;

    public HWND MainWindowHandle => default;

    public bool HasExited => true;

    public int ExitCode => 0;

    public void Start()
    {
        // Do nothing
    }

    public void ResumeMainThread()
    {
        // Do nothing
    }

    public void WaitForExit()
    {
        // Do nothing
    }

    public void Kill()
    {
        // Do nothing
    }

    public void Dispose()
    {
        // Do nothing
    }
}