#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using TranslatorApp.Models;
using Windows.Storage;
using System;

namespace TranslatorApp.Services
{
    public static class FavoritesService
    {
        private const string Key = "FavoritesJson";
        private static readonly ApplicationDataContainer Local = ApplicationData.Current.LocalSettings;

        // 缓存集合，保证永远不为 null
        private static ObservableCollection<FavoriteItem> _cache = new();

        public static ObservableCollection<FavoriteItem> Items
        {
            get => _cache;
            set => _cache = value ?? new ObservableCollection<FavoriteItem>();
        }

        /// <summary>
        /// 从本地存储加载收藏数据到缓存
        /// </summary>
        public static void Load()
        {
            if (Local.Values.ContainsKey(Key))
            {
                var json = Local.Values[Key] as string;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<ObservableCollection<FavoriteItem>>(json);
                        _cache = list ?? new ObservableCollection<FavoriteItem>();
                    }
                    catch
                    {
                        _cache = new ObservableCollection<FavoriteItem>();
                    }
                }
                else
                {
                    _cache = new ObservableCollection<FavoriteItem>();
                }
            }
            else
            {
                _cache = new ObservableCollection<FavoriteItem>();
            }
        }

        public static void Add(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;

            if (!_cache.Any(i => i.Term.Equals(term, StringComparison.OrdinalIgnoreCase)))
            {
                _cache.Add(new FavoriteItem { Term = term });
                Save();
            }
        }

        public static void Remove(FavoriteItem item)
        {
            if (_cache.Remove(item))
                Save();
        }

        public static void Save()
        {
            var json = JsonSerializer.Serialize(_cache);
            Local.Values[Key] = json;
        }
    }
}