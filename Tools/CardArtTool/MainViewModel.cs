using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CardArtTool.Models;

namespace CardArtTool;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly CardIndex _index = new();
    private readonly ToolSettings _settings = ToolSettings.Load();

    private string _originalRoot = "";
    private string _oursRoot = "";
    private string _cardsJson = "";
    private PoolOption? _selectedPool;
    private int _stateFilter;
    private string _searchText = "";
    private CardItem? _selectedCard;
    private PendingItem? _selectedPending;
    private string _statusMessage = "就绪";
    private string _summaryMessage = "";
    private bool _isBusy;
    private ImageSource? _originalPreview;
    private ImageSource? _oursPreview;
    private string _originalInfo = "";
    private string _oursInfo = "";

    public MainViewModel()
    {
        CardsView = CollectionViewSource.GetDefaultView(Cards);
        CardsView.Filter = FilterCard;

        Nodes = new ObservableCollection<PoolOption>();
        StateFilters = new ObservableCollection<string> { "全部", "已替换", "未替换" };

        LoadSettings();
        Refresh();
    }

    public ObservableCollection<CardItem> Cards { get; } = new();

    public ICollectionView CardsView { get; }

    public ObservableCollection<PoolOption> Nodes { get; }

    public ObservableCollection<string> StateFilters { get; }

    public ObservableCollection<PendingItem> Pending { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string OriginalRoot
    {
        get => _originalRoot;
        set => Set(ref _originalRoot, value);
    }

    public string OursRoot
    {
        get => _oursRoot;
        set => Set(ref _oursRoot, value);
    }

    public string CardsJson
    {
        get => _cardsJson;
        set => Set(ref _cardsJson, value);
    }

    public PoolOption? SelectedPool
    {
        get => _selectedPool;
        set
        {
            if (!Set(ref _selectedPool, value)) return;
            CardsView.Refresh();
        }
    }

    public int StateFilter
    {
        get => _stateFilter;
        set
        {
            if (!Set(ref _stateFilter, value)) return;
            CardsView.Refresh();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? "")) return;
            CardsView.Refresh();
        }
    }

    public CardItem? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (!Set(ref _selectedCard, value)) return;
            UpdatePreview();
        }
    }

    public PendingItem? SelectedPending
    {
        get => _selectedPending;
        set
        {
            if (!Set(ref _selectedPending, value)) return;
            // 选中待处理图片时同步选中它匹配的卡牌，预览区跟着更新
            if (value?.Matched != null) SelectCard(value.Matched.Pool, value.Matched.FileName);
            UpdatePreview();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public string SummaryMessage
    {
        get => _summaryMessage;
        set => Set(ref _summaryMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public ImageSource? OriginalPreview
    {
        get => _originalPreview;
        private set => Set(ref _originalPreview, value);
    }

    public ImageSource? OursPreview
    {
        get => _oursPreview;
        private set => Set(ref _oursPreview, value);
    }

    public string OriginalInfo
    {
        get => _originalInfo;
        private set => Set(ref _originalInfo, value);
    }

    public string OursInfo
    {
        get => _oursInfo;
        private set => Set(ref _oursInfo, value);
    }

    public void Refresh()
    {
        // 用户可能选了上层目录，这里向下探查到真正可用的位置
        var resolvedOriginal = PathResolver.ResolveOriginalRoot(OriginalRoot);
        var resolvedJson = PathResolver.ResolveCardsJson(CardsJson);
        if (!string.Equals(resolvedOriginal, OriginalRoot)) OriginalRoot = resolvedOriginal;
        if (!string.Equals(resolvedJson, CardsJson)) CardsJson = resolvedJson;

        _settings.OriginalRoot = OriginalRoot.Trim();
        _settings.OursRoot = OursRoot.Trim();
        _settings.CardsJson = CardsJson.Trim();
        _settings.Save();

        _index.Rebuild(_settings.OriginalRoot, _settings.OursRoot, _settings.CardsJson);

        var current = SelectedPool?.Value ?? "";
        Nodes.Clear();
        Nodes.Add(new PoolOption { Value = "", Display = "全部" });
        foreach (var pool in CardIndex.PoolOrder) Nodes.Add(new PoolOption { Value = pool, Display = PoolNames.Get(pool) });
        foreach (var pool in _index.Cards.Select(c => c.Pool).Distinct())
            if (Nodes.All(n => !string.Equals(n.Value, pool, StringComparison.OrdinalIgnoreCase)))
                Nodes.Add(new PoolOption { Value = pool, Display = PoolNames.Get(pool) });

        var keep = Nodes.FirstOrDefault(n => string.Equals(n.Value, current, StringComparison.OrdinalIgnoreCase))
                   ?? Nodes[0];
        SetProperty(ref _selectedPool, keep, nameof(SelectedPool));

        var keepFile = SelectedCard?.Entry.FileName;
        var keepPool = SelectedCard?.Entry.Pool;

        Cards.Clear();
        foreach (var entry in _index.Cards) Cards.Add(new CardItem(entry));
        CardsView.Refresh();

        if (keepFile != null) SelectCard(keepPool!, keepFile);

        var replaced = _index.Cards.Count(c => c.IsReplaced);
        SummaryMessage = $"共 {_index.Cards.Count} 张，已替换 {replaced} 张，未替换 {_index.Cards.Count - replaced} 张"
                         + BuildPoolProgress();
        if (_index.Warnings.Count > 0)
            SummaryMessage += "\n" + string.Join("；", _index.Warnings.Take(2));

        StatusMessage = _index.Cards.Count == 0
            ? "未找到卡牌数据，请检查路径"
            : "列表已刷新";
        if (_index.Cards.Count == 0 && string.IsNullOrWhiteSpace(OriginalRoot))
            StatusMessage = "请先在「原图目录」中选择游戏解包后的 card_portraits 目录";
        UpdatePreview();
    }

    public void AddPending(IEnumerable<string> files)
    {
        var added = new List<PendingItem>();
        foreach (var file in files)
        {
            if (!ImageOps.SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            if (Pending.Any(p => string.Equals(p.FilePath, file, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new PendingItem(file) { Matched = MatchCard(file) };
            Pending.Add(item);
            added.Add(item);
        }

        if (added.Count > 0) SelectedPending = added[0];

        var unmatched = Pending.Where(p => !p.IsMatched).Select(p => p.FileName).ToList();
        if (unmatched.Count > 0)
        {
            StatusMessage = $"待处理 {Pending.Count} 张；未找到匹配：{string.Join("、", unmatched.Take(6))}"
                            + (unmatched.Count > 6 ? $" 等 {unmatched.Count} 张" : "");
        }
        else
        {
            StatusMessage = added.Count > 0
                ? $"待处理 {Pending.Count} 张，全部匹配成功"
                : $"待处理 {Pending.Count} 张";
        }
    }

    public void ClearPending()
    {
        Pending.Clear();
        SelectedPending = null;
        StatusMessage = "已清空待处理列表";
    }

    // 优先级：中文名 → 归一化后的卡牌文件（忽略大小写/下划线/空格）→ 目录名限定的卡池
    private CardItem? MatchCard(string filePath)
    {
        var raw = Path.GetFileNameWithoutExtension(filePath);
        var normalized = CardMatcher.Normalize(raw);

        var poolHint = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
        IEnumerable<CardItem> scope = Cards;

        if (!string.IsNullOrEmpty(poolHint) && Cards.Any(c => string.Equals(c.Pool, poolHint, StringComparison.OrdinalIgnoreCase)))
            scope = Cards.Where(c => string.Equals(c.Pool, poolHint, StringComparison.OrdinalIgnoreCase));

        var byChinese = scope.FirstOrDefault(c => c.ChineseName == raw);
        if (byChinese != null) return byChinese;

        return scope.FirstOrDefault(c => CardMatcher.Normalize(c.FileName) == normalized)
               ?? Cards.FirstOrDefault(c => CardMatcher.Normalize(c.FileName) == normalized);
    }

    /// <summary>单张导入：待处理列表选中项 → 卡牌列表选中项。</summary>
    public void ImportSelected()
    {
        if (Pending.Count == 0)
        {
            StatusMessage = "请先添加待处理图片";
            return;
        }

        var pending = SelectedPending ?? Pending.FirstOrDefault();
        if (pending == null) return;

        var target = SelectedCard ?? pending.Matched;
        if (target == null)
        {
            StatusMessage = "请先选中目标卡牌（该图片也没能自动匹配）";
            return;
        }

        if (!ImportOne(pending, target)) return;

        Pending.Remove(pending);
        Refresh();
        SelectCard(target.Entry.Pool, CardIndex.TargetFileName(target.Entry));
    }

    /// <summary>批量导入：所有自动匹配成功的待处理项。</summary>
    public void ImportAllMatched()
    {
        if (Pending.Count == 0)
        {
            StatusMessage = "请先添加待处理图片";
            return;
        }

        var targets = Pending.Where(p => p.Matched != null).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "没有可导入的项：拖入的图片都没匹配到卡牌";
            return;
        }

        var ok = 0;
        var failed = new List<PendingItem>();
        foreach (var pending in targets)
        {
            if (ImportOne(pending, pending.Matched!)) ok++;
            else failed.Add(pending);
        }

        // 未匹配的、以及导入失败的，都留在待处理区
        var remaining = Pending.Where(p => p.Matched == null || failed.Contains(p))
            .Select(p => p.FilePath)
            .ToList();

        Refresh();

        Pending.Clear();
        SelectedPending = null;
        foreach (var path in remaining) Pending.Add(new PendingItem(path) { Matched = MatchCard(path) });
        if (Pending.Count > 0) SelectedPending = Pending[0];

        var kept = Pending.Count > 0
            ? $"；保留 {Pending.Count} 张：{string.Join("、", Pending.Select(p => p.FileName).Take(6))}"
              + (Pending.Count > 6 ? " 等" : "")
            : "";
        StatusMessage = $"已导入 {ok} 张{kept}";
    }

    private bool ImportOne(PendingItem pending, CardItem target)
    {
        if (string.IsNullOrWhiteSpace(OursRoot))
        {
            StatusMessage = "卡图目录未设置";
            return false;
        }

        var entry = target.Entry;
        var fileName = CardIndex.TargetFileName(entry);
        var targetDir = Path.Combine(OursRoot, entry.Pool);
        var targetPath = Path.Combine(targetDir, fileName + ".png");

        try
        {
            Directory.CreateDirectory(targetDir);

            var bytes = File.ReadAllBytes(pending.FilePath);
            ImageOps.SaveAsPng(pending.FilePath, bytes, targetPath);

            // 两种命名（带/不带下划线）不能并存，否则会命中旧图
            var twinName = fileName.Contains('_') ? fileName.Replace("_", "") : entry.FileName;
            var twin = Path.Combine(targetDir, twinName + ".png");
            if (!string.Equals(twin, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(twin))
            {
                DeleteImportFile(twin);
                File.Delete(twin);
            }

            StatusMessage = $"已导入：{pending.FileName} → {PoolNames.Get(entry.Pool)}/{Path.GetFileName(targetPath)}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败（{pending.FileName}）：{ex.Message}";
            return false;
        }
    }

    /// <summary>还原：删掉我们的替换图，界面上即回到游戏原图。</summary>
    public void RestoreSelected()
    {
        var card = SelectedCard;
        if (card?.Entry.OursPath == null)
        {
            StatusMessage = "当前选中的卡没有替换图";
            return;
        }

        if (!Confirm($"还原为原版卡图？\n{card.Entry.OursPath}")) return;

        try
        {
            DeleteImportFile(card.Entry.OursPath);
            File.Delete(card.Entry.OursPath);
            StatusMessage = "已还原：" + Path.GetFileName(card.Entry.OursPath);
            var pool = card.Entry.Pool;
            var file = card.Entry.FileName;
            Refresh();
            SelectCard(pool, file);
        }
        catch (Exception ex)
        {
            StatusMessage = "还原失败：" + ex.Message;
        }
    }

    private bool Confirm(string message) =>
        System.Windows.MessageBox.Show(message, "确认", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    private bool FilterCard(object obj)
    {
        if (obj is not CardItem card) return false;
        var pool = SelectedPool?.Value ?? "";
        if (pool.Length > 0 && card.Pool != pool) return false;
        if (StateFilter == 1 && !card.IsReplaced) return false;
        if (StateFilter == 2 && card.IsReplaced) return false;
        var search = SearchText.Trim();
        return search.Length == 0 || card.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    // 形如「铁甲战士：100/199」，只列有卡的卡池
    private string BuildPoolProgress()
    {
        var parts = new List<string>();
        foreach (var pool in CardIndex.PoolOrder)
        {
            var total = _index.Cards.Count(c => string.Equals(c.Pool, pool, StringComparison.OrdinalIgnoreCase));
            if (total == 0) continue;
            var done = _index.Cards.Count(c => string.Equals(c.Pool, pool, StringComparison.OrdinalIgnoreCase) && c.IsReplaced);
            parts.Add($"{PoolNames.Get(pool)}：{done}/{total}");
        }

        return parts.Count == 0 ? "" : "\n" + string.Join("　　", parts);
    }

    private void SelectCard(string pool, string fileName)
    {
        var compact = fileName.Replace("_", "");
        foreach (var card in Cards)
        {
            if (card.Pool != pool) continue;
            var name = card.FileName;
            if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name.Replace("_", ""), compact, StringComparison.OrdinalIgnoreCase)) continue;
            SelectedCard = card;
            return;
        }
    }

    private void UpdatePreview()
    {
        var entry = SelectedCard?.Entry;
        OriginalPreview = LoadImage(entry?.OriginalPath, out var originalSize);
        OriginalInfo = Describe(entry?.OriginalPath, originalSize, "原版卡图缺失") + "   原版";

        // 待处理项与其匹配卡牌同时选中时，替换图栏显示即将导入的新图
        if (PendingOverridesPreview())
        {
            OursPreview = LoadImage(SelectedPending!.FilePath, out var pendingSize);
            OursInfo = Describe(SelectedPending.FilePath, pendingSize, "读取失败") + "   待导入";
            return;
        }

        OursPreview = LoadImage(entry?.OursPath, out var oursSize);
        OursInfo = Describe(entry?.OursPath, oursSize, "尚未替换") + "   替换";
    }

    // 按卡池 + 文件名比对，Refresh 重建过实例也不会误判
    private bool PendingOverridesPreview()
    {
        var pending = SelectedPending;
        var card = SelectedCard;
        if (pending?.Matched == null || card == null) return false;
        return pending.Matched.Pool == card.Pool
               && string.Equals(pending.Matched.FileName, card.FileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Describe(string? path, string size, string missing)
    {
        if (path == null || !File.Exists(path)) return missing;
        return $"{Path.GetFileName(path)}   {size}";
    }

    private static ImageSource? LoadImage(string? path, out string size)
    {
        size = "";
        if (path == null || !File.Exists(path)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            size = $"{bitmap.PixelWidth}×{bitmap.PixelHeight}";
            return bitmap;
        }
        catch
        {
            size = "读取失败";
            return null;
        }
    }

    // Godot 的 .import 跟着 png 走，不删会留下孤儿记录
    private static void DeleteImportFile(string pngPath)
    {
        var importFile = pngPath + ".import";
        if (File.Exists(importFile)) File.Delete(importFile);
    }

    private void LoadSettings()
    {
        // 原图目录只认用户自己选的，不自动探测
        OursRoot = ResolveUsable(_settings.OursRoot) ?? Path.Combine(FindRepoRoot(), "images");
        OriginalRoot = ResolveUsable(_settings.OriginalRoot) ?? "";
        CardsJson = ResolveUsable(_settings.CardsJson) ?? "";
    }

    // 别的机器上旧配置里的路径可能已不存在，按空处理
    private static string? ResolveUsable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)) ? path : null;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0 && Directory.Exists(Path.Combine(dir.FullName, "images")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void SetProperty<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
