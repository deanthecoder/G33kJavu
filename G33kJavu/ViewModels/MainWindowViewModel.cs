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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DTC.Core.Commands;
using DTC.Core.Extensions;
using DTC.Core.UI;
using DTC.Core.ViewModels;
using G33kJavu.Core.Models;
using G33kJavu.Core.Scanning;
using G33kJavu.Core.Services;

namespace G33kJavu.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ScanService m_scanService = new ScanService();
    private readonly ScanSettings m_scanSettings = new ScanSettings();

    private CancellationTokenSource? m_scanCts;
    private DirectoryInfo? m_rootFolder;
    private DirectoryInfo? m_pendingRootFolder;
    private bool m_isScanning;
    private int m_progressPercent;
    private bool m_progressIsIndeterminate = true;
    private string m_progressText = "Idle";
    private FolderNodeViewModel? m_selectedFolder;
    private DuplicateMatchViewModel? m_selectedMatch;
    private string? m_diffTitleA;
    private string? m_diffTitleB;
    private string? m_diffToolTipA;
    private string? m_diffToolTipB;
    private bool m_isSettingsExpanded;

    private readonly List<FileItemViewModel> m_allFiles = [];
    private readonly Dictionary<int, ProcessedFile> m_filesById = [];
    private readonly List<DuplicateMatchViewModel> m_allMatches = [];
    private bool m_hideClonedFiles = true;
    private bool m_hideSameFileNames;

    public SettingsViewModel Settings { get; }

    public ObservableCollection<FolderNodeViewModel> FolderTreeRoots { get; } = [];
    public ObservableCollection<DuplicateMatchViewModel> Matches { get; } = [];
    public ObservableCollection<DiffLineViewModel> DiffLines { get; } = [];

    public ICommand SelectFolder { get; }
    public ICommand Rescan { get; }
    public ICommand CancelScan { get; }
    public ICommand CopyMatchInfo { get; }
    public ICommand RevealFileA { get; }
    public ICommand RevealFileB { get; }

    public MainWindowViewModel()
    {
        Settings = new SettingsViewModel(m_scanSettings);

        SelectFolder = new RelayCommand(async _ => await SelectFolderAsync());

        Rescan = new RelayCommand(async _ => await StartScanAsync(), _ => CanRescan);
        CancelScan = new RelayCommand(_ => m_scanCts?.Cancel(), _ => IsScanning);
        CopyMatchInfo = new RelayCommand(async o => await CopyMatchInfoAsync(o as DuplicateMatchViewModel));
        RevealFileA = new RelayCommand(o => RevealFile(o as DuplicateMatchViewModel, isFileA: true));
        RevealFileB = new RelayCommand(o => RevealFile(o as DuplicateMatchViewModel, isFileA: false));
    }

    public string WindowTitle => "G33kJavu — Duplicate Code Detector";

    public string RootFolderDisplay =>
        RootFolder == null ? "No folder selected" : RootFolder.FullName;

    public DirectoryInfo? RootFolder
    {
        get => m_rootFolder;
        private set
        {
            if (!SetField(ref m_rootFolder, value))
                return;
            OnPropertyChanged(nameof(RootFolderDisplay));
            OnPropertyChanged(nameof(CanRescan));
            ((RelayCommand)Rescan).RaiseCanExecuteChanged();
        }
    }

    public bool CanRescan => RootFolder != null && !IsScanning;

    public bool IgnoreStringContent
    {
        get => m_scanSettings.IgnoreStringContent;
        set
        {
            if (m_scanSettings.IgnoreStringContent == value)
                return;
            m_scanSettings.IgnoreStringContent = value;
            OnPropertyChanged();

            if (CanRescan)
                _ = StartScanAsync();
        }
    }

    public bool IgnoreComments
    {
        get => m_scanSettings.IgnoreComments;
        set
        {
            if (m_scanSettings.IgnoreComments == value)
                return;
            m_scanSettings.IgnoreComments = value;
            OnPropertyChanged();

            if (CanRescan)
                _ = StartScanAsync();
        }
    }

    public bool HideClonedFiles
    {
        get => m_hideClonedFiles;
        set
        {
            if (!SetField(ref m_hideClonedFiles, value))
                return;
            ApplyFolderFilter();
        }
    }

    public bool HideSameFileNames
    {
        get => m_hideSameFileNames;
        set
        {
            if (!SetField(ref m_hideSameFileNames, value))
                return;
            ApplyFolderFilter();
        }
    }

    public bool IsScanning
    {
        get => m_isScanning;
        private set
        {
            if (!SetField(ref m_isScanning, value))
                return;
            OnPropertyChanged(nameof(CanRescan));
            ((RelayCommand)Rescan).RaiseCanExecuteChanged();
            ((RelayCommand)CancelScan).RaiseCanExecuteChanged();
        }
    }

    public int ProgressPercent
    {
        get => m_progressPercent;
        private set => SetField(ref m_progressPercent, value);
    }

    public bool ProgressIsIndeterminate
    {
        get => m_progressIsIndeterminate;
        private set => SetField(ref m_progressIsIndeterminate, value);
    }

    public string ProgressText
    {
        get => m_progressText;
        private set => SetField(ref m_progressText, value);
    }

    public string MatchSummaryText =>
        Matches.Count == 0 ? "No matches" : $"{Matches.Count:n0} matches";

    public FolderNodeViewModel? SelectedFolder
    {
        get => m_selectedFolder;
        set
        {
            if (!SetField(ref m_selectedFolder, value))
                return;
            ApplyFolderFilter();
        }
    }

    public DuplicateMatchViewModel? SelectedMatch
    {
        get => m_selectedMatch;
        set
        {
            if (!SetField(ref m_selectedMatch, value))
                return;
            _ = LoadDiffAsync();
        }
    }

    public string? DiffTitleA
    {
        get => m_diffTitleA;
        private set => SetField(ref m_diffTitleA, value);
    }

    public string? DiffTitleB
    {
        get => m_diffTitleB;
        private set => SetField(ref m_diffTitleB, value);
    }

    public string? DiffToolTipA
    {
        get => m_diffToolTipA;
        private set => SetField(ref m_diffToolTipA, value);
    }

    public string? DiffToolTipB
    {
        get => m_diffToolTipB;
        private set => SetField(ref m_diffToolTipB, value);
    }

    public bool IsSettingsExpanded
    {
        get => m_isSettingsExpanded;
        set => SetField(ref m_isSettingsExpanded, value);
    }

    private async Task SelectFolderAsync()
    {
        var folder = await DialogService.Instance.SelectFolderAsync("Select a folder to scan");
        if (folder == null)
            return;
        await SelectRootFolderAndScanAsync(folder);
    }

    public async Task SelectRootFolderAndScanAsync(DirectoryInfo folder)
    {
        if (folder == null)
            return;

        RootFolder = folder;

        if (IsScanning)
        {
            m_pendingRootFolder = folder;
            m_scanCts?.Cancel();
            return;
        }

        await StartScanAsync();
    }

    private async Task StartScanAsync()
    {
        var rootFolder = RootFolder;
        if (rootFolder == null)
            return;

        IsScanning = true;
        ProgressIsIndeterminate = true;
        ProgressPercent = 0;
        ProgressText = "Enumerating…";

        DiffTitleA = DiffTitleB = null;
        DiffToolTipA = DiffToolTipB = null;
        DiffLines.Clear();

        Matches.Clear();
        m_allFiles.Clear();
        m_filesById.Clear();
        m_allMatches.Clear();
        FolderTreeRoots.Clear();
        OnPropertyChanged(nameof(MatchSummaryText));

        m_scanCts?.Dispose();
        m_scanCts = new CancellationTokenSource();
        var token = m_scanCts.Token;

        try
        {
            var progress = new Progress<ScanProgress>(p =>
                Dispatcher.UIThread.Post(() => UpdateProgress(p)));

            var result = await Task.Run(async () =>
                await m_scanService.ScanAsync(rootFolder, m_scanSettings, progress, token), token);

            PopulateFromResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DialogService.Instance.ShowMessage("Scan failed", ex.Message);
        }
        finally
        {
            IsScanning = false;

            var pending = m_pendingRootFolder;
            if (pending != null && RootFolder?.FullName == pending.FullName)
            {
                m_pendingRootFolder = null;
                await StartScanAsync();
            }
        }
    }

    private void UpdateProgress(ScanProgress p)
    {
        ProgressText = p.Phase switch
        {
            ScanPhase.Enumerating => "Enumerating files…",
            ScanPhase.Normalizing => $"Normalizing {p.FilesProcessed:n0} / {Math.Max(1, p.TotalFiles):n0}…",
            ScanPhase.Fingerprinting => $"Fingerprinting {p.FilesProcessed:n0} / {Math.Max(1, p.TotalFiles):n0}…",
            ScanPhase.Matching => $"Matching… ({p.MatchesFound:n0} matches)",
            ScanPhase.Finalizing => "Finalizing…",
            _ => "Working…"
        };

        if (p.TotalFiles > 0 && p.FilesProcessed >= 0)
        {
            ProgressPercent = (int)(100.0 * p.FilesProcessed / p.TotalFiles);
            ProgressIsIndeterminate = false;
        }
        else
        {
            ProgressIsIndeterminate = true;
        }
    }

    private void PopulateFromResult(ScanResult result)
    {
        var rootFolder = RootFolder;

        foreach (var file in result.Files)
            m_filesById[file.FileId] = file;

        foreach (var file in result.Files)
        {
            var rel = rootFolder != null ? Path.GetRelativePath(rootFolder.FullName, file.Path.FullName) : file.Path.FullName;
            m_allFiles.Add(new FileItemViewModel
            {
                FileId = file.FileId,
                File = file.Path,
                Category = file.Category,
                RelativePath = rel
            });
        }

        UpdateFolderTree();
        foreach (var match in result.Matches)
        {
            if (!m_filesById.TryGetValue(match.FileAId, out var fileA) ||
                !m_filesById.TryGetValue(match.FileBId, out var fileB))
                continue;
            m_allMatches.Add(DuplicateMatchViewModel.Create(match, fileA, fileB));
        }

        // Defensive UI-side de-duplication: collapse exact duplicate ranges per full path pair.
        // (This avoids repeated list entries if the engine yields duplicates due to file id/path aliasing.)
        var unique = m_allMatches
            .GroupBy(o => NormalizeMatchKey(o))
            .Select(o => o.First())
            .ToList();

        m_allMatches.Clear();
        m_allMatches.AddRange(unique);

        m_allMatches.Sort(static (a, b) => b.MatchedLineCount.CompareTo(a.MatchedLineCount));
        ApplyFolderFilter();
        OnPropertyChanged(nameof(MatchSummaryText));
    }

    private static (string Path1, string Path2, int P1Start, int P1End, int P2Start, int P2End) NormalizeMatchKey(DuplicateMatchViewModel vm)
    {
        var aPath = NormalizePathKey(vm.FileAFullPath);
        var bPath = NormalizePathKey(vm.FileBFullPath);

        var cmp = StringComparer.OrdinalIgnoreCase.Compare(aPath, bPath);
        if (cmp <= 0)
        {
            return (aPath, bPath,
                vm.Model.AStartOriginalLineNumber, vm.Model.AEndOriginalLineNumber,
                vm.Model.BStartOriginalLineNumber, vm.Model.BEndOriginalLineNumber);
        }

        return (bPath, aPath,
            vm.Model.BStartOriginalLineNumber, vm.Model.BEndOriginalLineNumber,
            vm.Model.AStartOriginalLineNumber, vm.Model.AEndOriginalLineNumber);
    }

    private static string NormalizePathKey(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            // Ignore.
        }

        return path
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private void UpdateFolderTree()
    {
        FolderTreeRoots.Clear();
        if (RootFolder == null)
            return;

        var rootNode = new FolderNodeViewModel
        {
            Name = RootFolder.Name,
            FullPath = RootFolder.FullName
        };
        FolderTreeRoots.Add(rootNode);

        foreach (var file in m_allFiles)
        {
            var dirFullPath = file.File.DirectoryName;
            if (string.IsNullOrEmpty(dirFullPath))
                continue;

            var relativeDir = Path.GetRelativePath(RootFolder.FullName, dirFullPath);
            if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
                continue;

            AddRelativeFolderPath(rootNode, relativeDir);
        }

        SelectedFolder = rootNode;
    }

    private static void AddRelativeFolderPath(FolderNodeViewModel root, string relativeDir)
    {
        var parts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var node = root;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part) || part == ".")
                continue;

            var existing = node.Children.FirstOrDefault(o => string.Equals(o.Name, part, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new FolderNodeViewModel
                {
                    Name = part,
                    FullPath = Path.Combine(node.FullPath, part)
                };
                node.Children.Add(existing);
            }
            node = existing;
        }
    }

    private void ApplyFolderFilter()
    {
        Matches.Clear();

        var folderPath = SelectedFolder?.FullPath;
        if (string.IsNullOrEmpty(folderPath) || RootFolder == null || folderPath == RootFolder.FullName)
        {
            for (var i = 0; i < m_allMatches.Count; i++)
            {
                var match = m_allMatches[i];
                if (IsHiddenByMode(match.Model))
                    continue;
                Matches.Add(match);
            }
        }
        else
        {
            for (var i = 0; i < m_allMatches.Count; i++)
            {
                var match = m_allMatches[i];
                if (IsHiddenByMode(match.Model))
                    continue;
                if (!m_filesById.TryGetValue(match.Model.FileAId, out var fileA) ||
                    !m_filesById.TryGetValue(match.Model.FileBId, out var fileB))
                    continue;

                if (fileA.Path.FullName.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) ||
                    fileB.Path.FullName.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                {
                    Matches.Add(match);
                }
            }
        }

        OnPropertyChanged(nameof(MatchSummaryText));
    }

    private bool IsHiddenByMode(DuplicateMatch match)
    {
        if (HideClonedFiles && IsClonePair(match))
            return true;

        if (HideSameFileNames && IsSameFileNamePair(match))
            return true;

        return false;
    }

    private bool IsSameFileNamePair(DuplicateMatch match)
    {
        if (!m_filesById.TryGetValue(match.FileAId, out var fileA) ||
            !m_filesById.TryGetValue(match.FileBId, out var fileB))
            return false;

        return string.Equals(fileA.Path.Name, fileB.Path.Name, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsClonePair(DuplicateMatch match)
    {
        if (!m_filesById.TryGetValue(match.FileAId, out var fileA) ||
            !m_filesById.TryGetValue(match.FileBId, out var fileB))
            return false;

        if (!IsSameFileNamePair(match))
            return false;

        if (fileA.Category != fileB.Category)
            return false;

        if (fileA.LineHashes.Length != fileB.LineHashes.Length)
            return false;

        return fileA.ContentHash == fileB.ContentHash;
    }

    private async Task LoadDiffAsync()
    {
        var matchVm = SelectedMatch;
        if (matchVm?.Model == null)
        {
            DiffTitleA = DiffTitleB = null;
            DiffToolTipA = DiffToolTipB = null;
            DiffLines.Clear();
            return;
        }

        if (!m_filesById.TryGetValue(matchVm.Model.FileAId, out var fileA) ||
            !m_filesById.TryGetValue(matchVm.Model.FileBId, out var fileB))
            return;

        var contextNormLines = 3;
        var pre = Math.Min(contextNormLines, Math.Min(matchVm.Model.AStartNormLine, matchVm.Model.BStartNormLine));
        var post = Math.Min(contextNormLines,
            Math.Min(fileA.LineHashes.Length - 1 - matchVm.Model.AEndNormLine, fileB.LineHashes.Length - 1 - matchVm.Model.BEndNormLine));

        var aNormStart = matchVm.Model.AStartNormLine - pre;
        var bNormStart = matchVm.Model.BStartNormLine - pre;
        var lineCount = matchVm.Model.MatchedLineCount + pre + post;

        DiffTitleA = fileA.Path.Name;
        DiffTitleB = fileB.Path.Name;
        var aStartOrig = fileA.OriginalLineNumbers[aNormStart];
        var aEndOrig = fileA.OriginalLineNumbers[aNormStart + lineCount - 1];
        var bStartOrig = fileB.OriginalLineNumbers[bNormStart];
        var bEndOrig = fileB.OriginalLineNumbers[bNormStart + lineCount - 1];

        DiffToolTipA = $"{fileA.Path.FullName}{Environment.NewLine}Showing lines {aStartOrig}-{aEndOrig} ({Math.Max(0, aEndOrig - aStartOrig + 1)} lines)";
        DiffToolTipB = $"{fileB.Path.FullName}{Environment.NewLine}Showing lines {bStartOrig}-{bEndOrig} ({Math.Max(0, bEndOrig - bStartOrig + 1)} lines)";

        var token = m_scanCts?.Token ?? CancellationToken.None;

        var rows = await Task.Run(() =>
            BuildDiffRows(fileA, fileB, aNormStart, bNormStart, lineCount, pre, matchVm.Model.MatchedLineCount, m_scanSettings.IgnoreStringContent, m_scanSettings.IgnoreComments, token), token);

        DiffLines.Clear();
        for (var i = 0; i < rows.Count; i++)
            DiffLines.Add(rows[i]);
    }

    private async Task CopyMatchInfoAsync(DuplicateMatchViewModel? match)
    {
        if (match?.Model == null)
            return;

        SelectedMatch = match;

        var info = BuildMatchInfo(match);
        var mainWindow = Application.Current?.GetMainWindow();
        var clipboard = mainWindow != null ? TopLevel.GetTopLevel(mainWindow)?.Clipboard : null;
        if (clipboard == null)
            return;

        await clipboard.SetTextAsync(info);
    }

    private string BuildMatchInfo(DuplicateMatchViewModel match)
    {
        var model = match.Model;
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Duplicate to review:");
        builder.AppendLine($"- A: {match.FileAFullPath} (lines {model.AStartOriginalLineNumber}-{model.AEndOriginalLineNumber})");
        builder.AppendLine($"- B: {match.FileBFullPath} (lines {model.BStartOriginalLineNumber}-{model.BEndOriginalLineNumber})");
        builder.AppendLine($"- Matched lines: {model.MatchedLineCount}");

        return builder.ToString().TrimEnd();
    }

    private void RevealFile(DuplicateMatchViewModel? match, bool isFileA)
    {
        if (match?.Model == null)
            return;

        var fileId = isFileA ? match.Model.FileAId : match.Model.FileBId;
        if (!m_filesById.TryGetValue(fileId, out var file))
            return;

        file.Path.Explore();
    }

    private static List<DiffLineViewModel> BuildDiffRows(
        ProcessedFile fileA,
        ProcessedFile fileB,
        int aNormStart,
        int bNormStart,
        int lineCount,
        int preContext,
        int matchedCount,
        bool ignoreStringContent,
        bool ignoreComments,
        CancellationToken cancellationToken)
    {
        try
        {
            var aLines = File.ReadAllLines(fileA.Path.FullName);
            var bLines = File.ReadAllLines(fileB.Path.FullName);

            var rawRows = new List<(int AOrig, int BOrig, string AText, string BText, DiffLineViewModel.MarkerKind Kind, string MarkerText, string MarkerToolTip, bool InBlock)>(capacity: lineCount);

            for (var i = 0; i < lineCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var aNormIndex = aNormStart + i;
                var bNormIndex = bNormStart + i;

                var aOrigLine = fileA.OriginalLineNumbers[aNormIndex];
                var bOrigLine = fileB.OriginalLineNumbers[bNormIndex];

                var aText = aOrigLine >= 1 && aOrigLine <= aLines.Length ? aLines[aOrigLine - 1] : string.Empty;
                var bText = bOrigLine >= 1 && bOrigLine <= bLines.Length ? bLines[bOrigLine - 1] : string.Empty;

                var exactMatch = string.Equals(aText, bText, StringComparison.Ordinal);
                var whitespaceMatch = !exactMatch && string.Equals(NormalizeWhitespace(aText), NormalizeWhitespace(bText), StringComparison.Ordinal);
                var normalizedMatch = fileA.LineHashes[aNormIndex] == fileB.LineHashes[bNormIndex];
                var normalizedA = LineNormalizer.NormalizeLine(aText, fileA.Category, ignoreStringContent, ignoreComments);
                var normalizedB = LineNormalizer.NormalizeLine(bText, fileB.Category, ignoreStringContent, ignoreComments);

                string markerText;
                string markerToolTip;
                DiffLineViewModel.MarkerKind kind;
                if (exactMatch)
                {
                    markerText = "≡";
                    markerToolTip = $"Exact line match.{Environment.NewLine}{Environment.NewLine}A: {aText}{Environment.NewLine}B: {bText}";
                    kind = DiffLineViewModel.MarkerKind.Exact;
                }
                else if (whitespaceMatch)
                {
                    markerText = "≡";
                    markerToolTip =
                        $"Matches ignoring whitespace.{Environment.NewLine}{Environment.NewLine}" +
                        $"A: {aText}{Environment.NewLine}B: {bText}";
                    kind = DiffLineViewModel.MarkerKind.Exact;
                }
                else if (normalizedMatch)
                {
                    markerText = "~";
                    markerToolTip =
                        $"Matches after normalization.{Environment.NewLine}" +
                        $"(Strings ignored: {ignoreStringContent}; comments ignored: {ignoreComments}; numbers => 123; whitespace collapsed){Environment.NewLine}{Environment.NewLine}" +
                        $"A: {aText}{Environment.NewLine}B: {bText}{Environment.NewLine}{Environment.NewLine}" +
                        $"A(norm): {normalizedA}{Environment.NewLine}B(norm): {normalizedB}";
                    kind = DiffLineViewModel.MarkerKind.Normalized;
                }
                else
                {
                    markerText = "×";
                    markerToolTip =
                        $"Different lines.{Environment.NewLine}{Environment.NewLine}" +
                        $"A: {aText}{Environment.NewLine}B: {bText}{Environment.NewLine}{Environment.NewLine}" +
                        $"A(norm): {normalizedA}{Environment.NewLine}B(norm): {normalizedB}";
                    kind = DiffLineViewModel.MarkerKind.Different;
                }

                var isInMatchedBlock = i >= preContext && i < preContext + matchedCount;

                rawRows.Add((aOrigLine, bOrigLine, aText, bText, kind, markerText, markerToolTip, isInMatchedBlock));
            }

            var aIndent = GetSharedIndent(rawRows.Select(o => o.AText));
            var bIndent = GetSharedIndent(rawRows.Select(o => o.BText));

            var rows = new List<DiffLineViewModel>(capacity: rawRows.Count + 2);

            if (rawRows.Count > 0)
            {
                var first = rawRows[0];
                if (first.AOrig > 1 || first.BOrig > 1)
                    rows.Add(CreateEllipsisRow());
            }

            for (var i = 0; i < rawRows.Count; i++)
            {
                var raw = rawRows[i];
                rows.Add(new DiffLineViewModel
                {
                    ALineNumber = raw.AOrig,
                    BLineNumber = raw.BOrig,
                    AText = TrimIndent(raw.AText, aIndent),
                    BText = TrimIndent(raw.BText, bIndent),
                    AToolTip = raw.AText,
                    BToolTip = raw.BText,
                    Kind = raw.Kind,
                    MarkerText = raw.MarkerText,
                    MarkerToolTip = raw.MarkerToolTip,
                    IsInMatchedBlock = raw.InBlock,
                    IsEllipsis = false
                });
            }

            if (rawRows.Count > 0)
            {
                var last = rawRows[^1];
                if (last.AOrig < aLines.Length || last.BOrig < bLines.Length)
                    rows.Add(CreateEllipsisRow());
            }

            return rows;
        }
        catch
        {
            return
            [
                new DiffLineViewModel
                {
                    ALineNumber = null,
                    BLineNumber = null,
                    AText = "<Unable to load diff>",
                    BText = "<Unable to load diff>",
                    AToolTip = "<Unable to load diff>",
                    BToolTip = "<Unable to load diff>",
                    Kind = DiffLineViewModel.MarkerKind.Error,
                    MarkerText = "!",
                    MarkerToolTip = "Unable to load diff.",
                    IsInMatchedBlock = false,
                    IsEllipsis = false
                }
            ];
        }
    }

    private static DiffLineViewModel CreateEllipsisRow() =>
        new DiffLineViewModel
        {
            ALineNumber = null,
            BLineNumber = null,
            AText = "...",
            BText = "...",
            AToolTip = "Lines omitted.",
            BToolTip = "Lines omitted.",
            Kind = DiffLineViewModel.MarkerKind.Exact,
            MarkerText = string.Empty,
            MarkerToolTip = "Lines omitted.",
            IsInMatchedBlock = false,
            IsEllipsis = true
        };

    private static int GetSharedIndent(IEnumerable<string> lines)
    {
        var min = int.MaxValue;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var count = 0;
            while (count < line.Length && char.IsWhiteSpace(line[count]))
                count++;

            if (count < min)
                min = count;
            if (min == 0)
                break;
        }

        return min == int.MaxValue ? 0 : min;
    }

    private static string TrimIndent(string s, int count)
    {
        if (string.IsNullOrEmpty(s) || count <= 0 || s.Length <= count)
            return s;

        return s.Substring(count);
    }

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        var builder = new System.Text.StringBuilder(s.Length);
        var lastWasWhitespace = false;

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsWhiteSpace(ch))
            {
                lastWasWhitespace = true;
                continue;
            }

            if (lastWasWhitespace && builder.Length > 0)
                builder.Append(' ');

            lastWasWhitespace = false;
            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }
}
