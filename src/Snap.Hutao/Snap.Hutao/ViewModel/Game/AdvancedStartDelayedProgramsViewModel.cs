// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Snap.Hutao.Core.IO;
using Snap.Hutao.Factory.Picker;
using Snap.Hutao.Service.Game.AdvancedStart;
using Snap.Hutao.Service.Game.AdvancedStart.Model;
using Snap.Hutao.Service.Notification;
using System.Collections.ObjectModel;
using System.IO;

namespace Snap.Hutao.ViewModel.Game;

[Service(ServiceLifetime.Singleton)]
[BindableCustomPropertyProvider]
internal sealed partial class AdvancedStartDelayedProgramsViewModel : Abstraction.ViewModel
{
    private readonly AdvancedStartDelayedProgramStore store;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;

    [GeneratedConstructor]
    public partial AdvancedStartDelayedProgramsViewModel(IServiceProvider serviceProvider);

    public ObservableCollection<AdvancedStartDelayedProgramEntry> Entries { get; private set => SetProperty(ref field, value); } = [];

    [ObservableProperty]
    private AdvancedStartDelayedProgramEntry? selectedEntry;

    protected override ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        Entries = store.Load();
        return ValueTask.FromResult(true);
    }

    [Command("AddDelayedProgramCommand")]
    private async Task AddDelayedProgramAsync()
    {
        await taskContext.SwitchToBackgroundAsync();
        (bool ok, ValueFile file) = fileSystemPickerInteraction.PickFile("Picker", "program", "*.exe");
        if (!ok)
        {
            return;
        }

        string path = file;
        string name = Path.GetFileNameWithoutExtension(path);

        await taskContext.SwitchToMainThreadAsync();
        AdvancedStartDelayedProgramEntry entry = new(name, path, 0);
        Entries.Add(entry);
        SelectedEntry = entry;
        store.Save(Entries);
    }

    [Command("RemoveDelayedProgramCommand")]
    private void RemoveDelayedProgram()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        Entries.Remove(SelectedEntry);
        SelectedEntry = null;
        store.Save(Entries);
    }

    [Command("SaveDelayedProgramCommand")]
    private void SaveDelayedProgram()
    {
        store.Save(Entries);
        messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramPathSaved));
    }

    [Command("PickDelayedProgramPathCommand")]
    private async Task PickDelayedProgramPathAsync(AdvancedStartDelayedProgramEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        await taskContext.SwitchToBackgroundAsync();
        (bool ok, ValueFile file) = fileSystemPickerInteraction.PickFile("Picker", "program", "*.exe");
        if (!ok)
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        entry.Path = file;
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            entry.Name = Path.GetFileNameWithoutExtension(entry.Path);
        }

        store.Save(Entries);
    }
}
