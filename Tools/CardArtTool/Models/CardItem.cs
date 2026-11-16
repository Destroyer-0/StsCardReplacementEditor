using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace CardArtTool.Models;

public sealed class CardItem
{
    public CardItem(CardEntry entry) => Entry = entry;

    public CardEntry Entry { get; }

    public string Status => Entry.IsReplaced ? "✔" : "—";

    public string Pool => Entry.Pool;

    public string PoolDisplay => PoolNames.Get(Entry.Pool);

    public string FileName => Entry.FileName;

    public string ChineseName => Entry.ChineseName;

    public bool IsReplaced => Entry.IsReplaced;

    public string SearchText => Entry.FileName + " " + Entry.ChineseName;
}

public sealed class PendingItem : INotifyPropertyChanged
{
    private CardItem? _matched;

    public PendingItem(string path) => FilePath = path;

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>自动匹配到的卡牌，null 表示没找到。</summary>
    public CardItem? Matched
    {
        get => _matched;
        set
        {
            _matched = value;
            OnPropertyChanged(nameof(Matched));
            OnPropertyChanged(nameof(MatchText));
            OnPropertyChanged(nameof(IsMatched));
        }
    }

    public bool IsMatched => _matched != null;

    public string MatchText => _matched == null
        ? "未找到对应卡牌"
        : $"{_matched.ChineseName}  ({_matched.PoolDisplay} / {_matched.FileName})";

    public override string ToString() => FileName;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
