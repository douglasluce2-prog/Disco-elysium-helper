using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoDictionary.Core;

public sealed class WordSense
{
    public string PartOfSpeech { get; set; } = "";
    public string Definition { get; set; } = "";
    public string Example { get; set; } = "";
}

public sealed class WordDefinition
{
    public string Word { get; set; } = "";
    public string Phonetic { get; set; } = "";
    public List<WordSense> Senses { get; set; } = new();
}

public enum LookupStatus
{
    Found,
    NotFound,
    Error,
    Disabled,
}

public sealed class LookupResult
{
    public LookupResult(string query, LookupStatus status, WordDefinition? definition = null, string message = "")
    {
        Query = query;
        Status = status;
        Definition = definition;
        Message = message;
    }

    public string Query { get; }
    public LookupStatus Status { get; }
    public WordDefinition? Definition { get; }
    public string Message { get; }
}

/// <summary>
/// Looks up ordinary English words with the free dictionaryapi.dev service (no account or key needed).
/// Answers are cached on disk so each word is only ever downloaded once.
/// </summary>
public sealed class EnglishDictionaryClient : IDisposable
{
    public const string DefaultBaseUrl = "https://api.dictionaryapi.dev/api/v2/entries/en/";
    public const int MaxSensesKept = 8;

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string? _cachePath;
    private readonly ConcurrentDictionary<string, WordDefinition?> _cache = new(StringComparer.Ordinal);
    private readonly object _saveLock = new();
    private int _dirty;

    public EnglishDictionaryClient(string? cachePath = null, HttpMessageHandler? handler = null, string baseUrl = DefaultBaseUrl)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DiscoDictionary/1.0 (Disco Elysium mod)");
        _baseUrl = baseUrl;
        _cachePath = cachePath;
        LoadCache();
    }

    public int CachedCount => _cache.Count;

    /// <summary>
    /// Turns whatever the player pointed at into something worth looking up, or null.
    /// "Ineffable," -> "ineffable"; "Revachol's" -> "revachol".
    /// </summary>
    public static string? NormalizeQuery(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var tokens = Tokenizer.Tokenize(raw);
        if (tokens.Count == 0 || tokens.Count > 4)
            return null;

        var parts = new List<string>();
        foreach (var t in tokens)
            parts.Add(t.Key);
        var word = string.Join(" ", parts);
        if (word.Length > 40)
            return null;
        foreach (char c in word)
        {
            if (char.IsDigit(c))
                return null;
        }
        return word;
    }

    /// <summary>Simple fallbacks for inflected forms the service doesn't know ("vicissitudes" -> "vicissitude").</summary>
    public static List<string> CandidateForms(string word)
    {
        var forms = new List<string> { word };
        void Add(string f)
        {
            if (f.Length >= 3 && !forms.Contains(f))
                forms.Add(f);
        }

        if (word.Contains(' '))
            return forms;
        if (word.EndsWith("ies"))
            Add(word.Substring(0, word.Length - 3) + "y");
        if (word.EndsWith("s") && !word.EndsWith("ss"))
            Add(word.Substring(0, word.Length - 1));
        if (word.EndsWith("es"))
            Add(word.Substring(0, word.Length - 2));
        if (word.EndsWith("ied"))
            Add(word.Substring(0, word.Length - 3) + "y");
        if (word.EndsWith("ed"))
        {
            Add(word.Substring(0, word.Length - 2));
            Add(word.Substring(0, word.Length - 1));
        }
        if (word.EndsWith("ing"))
        {
            Add(word.Substring(0, word.Length - 3));
            Add(word.Substring(0, word.Length - 3) + "e");
        }
        if (word.EndsWith("ly"))
            Add(word.Substring(0, word.Length - 2));
        return forms;
    }

    public async Task<LookupResult> LookupAsync(string rawWord, CancellationToken cancellationToken = default)
    {
        var word = NormalizeQuery(rawWord);
        if (word == null)
            return new LookupResult(rawWord ?? "", LookupStatus.NotFound, message: "That doesn't look like a word that can be looked up.");

        foreach (var form in CandidateForms(word))
        {
            if (_cache.TryGetValue(form, out var cached))
            {
                if (cached != null)
                    return new LookupResult(word, LookupStatus.Found, cached);
                continue;
            }

            HttpResponseMessage response;
            string body;
            try
            {
                response = await _http.GetAsync(_baseUrl + Uri.EscapeDataString(form), cancellationToken).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new LookupResult(word, LookupStatus.Error, message: "The online dictionary took too long to answer.");
            }
            catch (HttpRequestException ex)
            {
                return new LookupResult(word, LookupStatus.Error, message: "Couldn't reach the online dictionary: " + ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new LookupResult(word, LookupStatus.Error, message: "Online lookup failed: " + ex.Message);
            }

            var result = ParseResponse(form, response.StatusCode, body);
            if (result.Status == LookupStatus.Error)
                return result;

            _cache[form] = result.Definition;
            Interlocked.Exchange(ref _dirty, 1);
            if (result.Status == LookupStatus.Found)
                return new LookupResult(word, LookupStatus.Found, result.Definition);
        }

        return new LookupResult(word, LookupStatus.NotFound, message: "No definition found.");
    }

    /// <summary>Parses a dictionaryapi.dev response. Public for testing.</summary>
    public static LookupResult ParseResponse(string word, HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound)
            return new LookupResult(word, LookupStatus.NotFound, message: "No definition found.");
        if ((int)status == 429)
            return new LookupResult(word, LookupStatus.Error, message: "The online dictionary is busy. Try again in a minute.");
        if ((int)status < 200 || (int)status >= 300)
            return new LookupResult(word, LookupStatus.Error, message: $"The online dictionary answered with an error ({(int)status}).");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return new LookupResult(word, LookupStatus.NotFound, message: "No definition found.");

            var def = new WordDefinition { Word = word };
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (def.Phonetic.Length == 0)
                    def.Phonetic = GetString(entry, "phonetic");
                if (def.Phonetic.Length == 0 && entry.TryGetProperty("phonetics", out var phonetics) && phonetics.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in phonetics.EnumerateArray())
                    {
                        var text = GetString(p, "text");
                        if (text.Length > 0)
                        {
                            def.Phonetic = text;
                            break;
                        }
                    }
                }
                if (entry.TryGetProperty("word", out var w) && w.ValueKind == JsonValueKind.String && def.Word == word)
                    def.Word = w.GetString() ?? word;

                if (!entry.TryGetProperty("meanings", out var meanings) || meanings.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var meaning in meanings.EnumerateArray())
                {
                    var pos = GetString(meaning, "partOfSpeech");
                    if (!meaning.TryGetProperty("definitions", out var defs) || defs.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var d in defs.EnumerateArray())
                    {
                        var text = GetString(d, "definition");
                        if (text.Length == 0 || def.Senses.Count >= MaxSensesKept)
                            continue;
                        def.Senses.Add(new WordSense { PartOfSpeech = pos, Definition = text, Example = GetString(d, "example") });
                    }
                }
            }

            return def.Senses.Count == 0
                ? new LookupResult(word, LookupStatus.NotFound, message: "No definition found.")
                : new LookupResult(word, LookupStatus.Found, def);
        }
        catch (JsonException)
        {
            return new LookupResult(word, LookupStatus.Error, message: "The online dictionary sent something unreadable.");
        }
    }

    private static string GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";

    private void LoadCache()
    {
        if (_cachePath == null || !File.Exists(_cachePath))
            return;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, WordDefinition?>>(File.ReadAllText(_cachePath, Encoding.UTF8));
            if (data == null)
                return;
            foreach (var kv in data)
                _cache[kv.Key] = kv.Value;
        }
        catch (Exception)
        {
            // A corrupt cache is not worth crashing over; it will be rewritten.
        }
    }

    /// <summary>Writes the cache if anything new was looked up. Safe to call often.</summary>
    public void SaveCache()
    {
        if (_cachePath == null || Interlocked.Exchange(ref _dirty, 0) == 0)
            return;
        lock (_saveLock)
        {
            var snapshot = new SortedDictionary<string, WordDefinition?>(StringComparer.Ordinal);
            foreach (var kv in _cache)
                snapshot[kv.Key] = kv.Value;
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var tmp = _cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            File.Copy(tmp, _cachePath, overwrite: true);
            File.Delete(tmp);
        }
    }

    public void Dispose() => _http.Dispose();
}
