// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using G33kJavu.ViewModels;
using System.IO;
using System.Linq;

namespace G33kJavu.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;

        e.Handled = true;
    }

    // ReSharper disable once AsyncVoidMethod
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return;

        var items = e.Data.GetFiles();
        if (items == null)
            return;

        var paths = items.Select(o => o?.Path?.LocalPath).Where(o => !string.IsNullOrEmpty(o)).ToList();
        if (paths.Count == 0)
            return;

        string? folderPath = null;
        for (var i = 0; i < paths.Count; i++)
        {
            var p = paths[i]!;
            if (Directory.Exists(p))
            {
                folderPath = p;
                break;
            }

            if (File.Exists(p))
            {
                folderPath = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(folderPath))
                    break;
            }
        }

        if (string.IsNullOrEmpty(folderPath))
            return;

        if (DataContext is MainWindowViewModel vm)
            await vm.SelectRootFolderAndScanAsync(new DirectoryInfo(folderPath));

        e.Handled = true;
    }
}
