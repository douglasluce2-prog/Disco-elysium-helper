using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DiscoDictionary.Core;

/// <summary>
/// Remembers which entries the player has already come across in game, so spoiler-sensitive
/// entries stay out of the dictionary list until they've been earned.
/// </summary>
public sealed class SeenStore
{
    private readonly string? _path;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public SeenStore(string? path = null)
    {
        _path = path;
    }

    public bool IsDirty { get; private set; }

    public int Count => _seen.Count;

    public bool IsSeen(string id) => _seen.Contains(id);

    /// <summary>Returns true the first time an id is marked.</summary>
    public bool MarkSeen(string id)
    {
        if (!_seen.Add(id))
            return false;
        IsDirty = true;
        return true;
    }

    public void Load()
    {
        if (_path == null || !File.Exists(_path))
            return;
        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path, Encoding.UTF8));
            if (ids != null)
            {
                foreach (var id in ids)
                    _seen.Add(id);
            }
        }
        catch (Exception)
        {
            // Ignore a damaged file; it will be overwritten on the next save.
        }
        IsDirty = false;
    }

    public void Save()
    {
        if (_path == null || !IsDirty)
            return;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var ids = _seen.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        File.WriteAllText(_path, JsonSerializer.Serialize(ids, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        IsDirty = false;
    }
}
