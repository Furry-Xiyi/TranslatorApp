using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using TranslatorApp.Models;
using Windows.Storage;
using System;

namespace TranslatorApp.Services;

public static class FavoritesService
{
    private const string Key = "FavoritesJson";
    private static readonly ApplicationDataContainer Local = ApplicationData.Current.LocalSettings;

    private static ObservableCollection<FavoriteItem>? _cache;

    public static ObservableCollection<FavoriteItem> Items
    {
        get
        {
            if (_cache is not null) return _cache;
            var json = (string?)Local.Values[Key];
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<ObservableCollection<FavoriteItem>>(json);
                    _cache = list ?? new();
                }
                catch { _cache = new(); }
            }
            else _cache = new();
            return _cache;
        }
    }

    public static void Add(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;
        if (!Items.Any(i => i.Term.Equals(term, StringComparison.OrdinalIgnoreCase)))
        {
            Items.Add(new FavoriteItem { Term = term });
            Save();
        }
    }

    public static void Remove(FavoriteItem item)
    {
        if (Items.Remove(item)) Save();
    }

    public static void Save()
    {
        var json = JsonSerializer.Serialize(Items);
        Local.Values[Key] = json;
    }
}