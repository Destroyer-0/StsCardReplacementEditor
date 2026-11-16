using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WinForms = System.Windows.Forms;

namespace CardArtTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => _vm.Refresh();

    // 失焦即应用路径，不用额外按刷新
    private void PathBox_OnLostFocus(object sender, RoutedEventArgs e) => _vm.Refresh();

    private void BrowseOriginalRoot_OnClick(object sender, RoutedEventArgs e)
    {
        if (PickFolder(_vm.OriginalRoot, "选择原版卡图目录") is not { } path) return;
        _vm.OriginalRoot = path;
        _vm.Refresh();
    }

    private void BrowseOursRoot_OnClick(object sender, RoutedEventArgs e)
    {
        if (PickFolder(_vm.OursRoot, "选择本 mod 卡图目录") is { } path)
        {
            _vm.OursRoot = path;
            _vm.Refresh();
        }
    }

    private void BrowseCardsJson_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog { Filter = "cards.json|cards.json|JSON|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        _vm.CardsJson = dialog.FileName;
        _vm.Refresh();
    }

    private void ClearCardsJson_OnClick(object sender, RoutedEventArgs e)
    {
        _vm.CardsJson = "";
        _vm.Refresh();
    }

    private void AddImages_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Multiselect = true,
            Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|所有文件|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;
        _vm.AddPending(dialog.FileNames);
    }

    private void ClearPending_OnClick(object sender, RoutedEventArgs e) => _vm.ClearPending();

    private void Import_OnClick(object sender, RoutedEventArgs e) => _vm.ImportSelected();

    private void ImportAll_OnClick(object sender, RoutedEventArgs e) => _vm.ImportAllMatched();

    private void Restore_OnClick(object sender, RoutedEventArgs e) => _vm.RestoreSelected();

    private void CardList_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_vm.SelectedCard?.Entry.OursPath is not { } path) return;
        OpenFolder(Path.GetDirectoryName(path));
    }

    private void DropZone_OnDragEnter(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_OnDrop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] dropped) return;

        var files = new List<string>();
        foreach (var item in dropped)
        {
            if (Directory.Exists(item)) files.AddRange(Directory.EnumerateFiles(item));
            else files.Add(item);
        }

        _vm.AddPending(files);
    }

    private async void Export_OnClick(object sender, RoutedEventArgs e)
    {
        var root = ExportRunner.FindRepoRoot(_vm.OursRoot);
        if (root == null)
        {
            WpfMessageBox.Show(this, "找不到 导出pck.bat（应从仓库内启动本工具）", "导出失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _vm.IsBusy = true;
        _vm.StatusMessage = "正在导出 pck（会先跑一次 Godot 资源导入）...";
        try
        {
            var (ok, log) = await ExportRunner.RunAsync(root);
            _vm.StatusMessage = ok ? "导出完成" : "导出失败";
            WpfMessageBox.Show(this, log, ok ? "导出完成" : "导出失败",
                MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = "导出失败：" + ex.Message;
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }

    private static string? PickFolder(string current, string title)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(current) ? current : "",
        };
        return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
