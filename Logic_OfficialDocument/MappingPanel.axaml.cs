using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Ava.Logic.OfficialDocument;

public partial class MappingPanel : UserControl
{
    public MappingPanel()
    {
        InitializeComponent();
        SetupEvents();
    }

    private void SetupEvents()
    {
        var targetBox = this.FindControl<TextBox>("TargetPathBox");
        var folderBox = this.FindControl<TextBox>("FolderPathBox");

        // 目标文件：粘贴时直接从剪贴板读取路径
        if (targetBox != null)
        {
            targetBox.AddHandler(TextBox.PastingFromClipboardEvent, async (s, e) =>
            {
                e.Handled = true; // 阻止 TextBox 默认粘贴（反正它也不更新 Text）

                // 先尝试以文本形式读取（用户可能复制了路径字符串）
                var text = await ReadClipboardText();
                if (!string.IsNullOrEmpty(text))
                {
                    var clean = MappingPanelViewModel.CleanPath(text);
                    if (File.Exists(clean))
                    {
                        var vm = DataContext as MappingPanelViewModel;
                        if (vm != null) vm.TargetFilePath = clean;
                        targetBox.Text = clean; // 手动设置显示
                        return;
                    }
                }

                // 再尝试以文件格式读取（用户直接 Ctrl+C 复制文件）
                var files = await ReadClipboardFiles();
                if (files != null && files.Count > 0)
                {
                    var first = files[0];
                    foreach (var ext in new[] { ".docx", ".xlsx" })
                    {
                        if (first.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            var vm = DataContext as MappingPanelViewModel;
                            if (vm != null) vm.TargetFilePath = first;
                            targetBox.Text = first;
                            break;
                        }
                    }
                }
            });
        }

        // 源文件夹：粘贴时直接增量添加 + Enter 添加
        if (folderBox != null)
        {
            folderBox.AddHandler(TextBox.PastingFromClipboardEvent, async (s, e) =>
            {
                e.Handled = true;

                // 先尝试以文本形式读取（用户可能复制了路径字符串）
                var text = await ReadClipboardText();
                if (!string.IsNullOrEmpty(text))
                {
                    var vm = DataContext as MappingPanelViewModel;
                    if (vm != null) AddParsedFolders(vm, text);
                    folderBox.Text = "";
                    (DataContext as MappingPanelViewModel)?.TriggerScan();
                    return;
                }

                // 再尝试以文件格式读取（用户直接 Ctrl+C 复制文件夹）
                var files = await ReadClipboardFiles();
                if (files != null)
                {
                    var vm = DataContext as MappingPanelViewModel;
                    if (vm == null) return;

                    foreach (var path in files)
                    {
                        if (Directory.Exists(path))
                            vm.AddFolderPath(path);
                    }
                    vm.TriggerScan();
                }
            });
            folderBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    var vm = DataContext as MappingPanelViewModel;
                    if (vm != null && !string.IsNullOrEmpty(folderBox?.Text))
                    {
                        AddParsedFolders(vm, folderBox.Text);
                        folderBox.Text = "";
                        vm.TriggerScan();
                    }
                    e.Handled = true;
                }
            };
        }

        // DataContext 连接后，同步目标文件路径到文本框
        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is MappingPanelViewModel vm)
            {
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MappingPanelViewModel.TargetFilePath) && targetBox != null)
                        targetBox.Text = vm.TargetFilePath;
                };
            }
        };

        // 拖放事件
        var targetBorder = this.FindControl<Border>("TargetDropBorder");
        if (targetBorder != null)
        {
            DragDrop.SetAllowDrop(targetBorder, true);
            targetBorder.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            targetBorder.AddHandler(DragDrop.DropEvent, OnTargetDrop);
        }

        var folderBorder = this.FindControl<Border>("FolderDropBorder");
        if (folderBorder != null)
        {
            DragDrop.SetAllowDrop(folderBorder, true);
            folderBorder.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            folderBorder.AddHandler(DragDrop.DropEvent, OnFolderDrop);
        }
    }

    /// <summary>点击空白/静态区域时：清空焦点 + 启动窗口拖拽</summary>
    private void OnRootPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // 只对非交互控件生效（TextBox/Button/Scroll 等不触发拖拽）
        if (e.Source is Avalonia.Controls.TextBox or Avalonia.Controls.Button)
            return;

        // 清除任何 TextBox 的焦点
        if (sender is Avalonia.Controls.Control c)
        {
            c.Focusable = true;
            c.Focus(NavigationMethod.Pointer, KeyModifiers.None);
        }

        // 启动窗口拖拽（单击不移动则窗口不动，不影响其他操作）
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.BeginMoveDrag(e);
    }

    private async Task<string?> ReadClipboardText()
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            var cb = top?.Clipboard;
            if (cb == null) return null;
            return await cb.TryGetTextAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从剪贴板读取文件/文件夹路径（对应 Windows 文件管理器 Ctrl+C 复制的文件）
    /// </summary>
    private async Task<List<string>?> ReadClipboardFiles()
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            var cb = top?.Clipboard;
            if (cb == null) return null;

            var items = await cb.TryGetFilesAsync();
            if (items == null || items.Length == 0) return null;

            var result = new List<string>();
            foreach (var item in items)
            {
                var localPath = item.TryGetLocalPath();
                if (!string.IsNullOrEmpty(localPath))
                    result.Add(localPath);
            }
            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnTargetDrop(object? sender, DragEventArgs e)
    {
        var files = TryGetFiles(e);
        if (files == null || files.Count == 0) return;

        var first = files[0];
        foreach (var ext in new[] { ".docx", ".xlsx" })
        {
            if (first.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                var vm = DataContext as MappingPanelViewModel;
                vm?.SetTargetFilePath(first);
                break;
            }
        }
    }

    private void OnFolderDrop(object? sender, DragEventArgs e)
    {
        var files = TryGetFiles(e);
        if (files == null) return;

        var vm = DataContext as MappingPanelViewModel;
        if (vm == null) return;

        foreach (var path in files)
        {
            if (Directory.Exists(path))
                vm.AddFolderPath(path);
        }
        vm.TriggerScan();
    }

    /// <summary>解析文本中的路径（支持分号/换行分隔），增量添加到 ViewModel，自动去重</summary>
    private static void AddParsedFolders(MappingPanelViewModel vm, string input)
    {
        foreach (var p in input.Split(new[] { '\r', '\n', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = p.Trim();
            if (Directory.Exists(t))
                vm.AddFolderPath(t);  // AddFolderPath 内部已去重
        }
    }

    private static List<string>? TryGetFiles(DragEventArgs e)
    {
        try
        {
            var transfer = e.DataTransfer;
            if (transfer == null) return null;

            var result = new List<string>();
            foreach (var item in transfer.Items)
            {
                if (!item.Formats.Contains(DataFormat.File)) continue;

                var raw = item.TryGetRaw(DataFormat.File);
                if (raw is IStorageItem storageItem)
                {
                    var localPath = storageItem.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(localPath))
                        result.Add(localPath);
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }
}
