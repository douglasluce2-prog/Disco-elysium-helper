using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscoDictionary.Core;
using Xunit;

namespace DiscoDictionary.Core.Tests;

public class EnglishDictionaryTests
{
    // Trimmed real response shape from https://api.dictionaryapi.dev/api/v2/entries/en/ineffable
    private const string IneffableJson = @"[{""word"":""ineffable"",""phonetic"":""/ɪnˈɛfəbl̩/"",""phonetics"":[{""text"":""/ɪnˈɛfəbl̩/"",""audio"":""""}],
      ""meanings"":[{""partOfSpeech"":""adjective"",""definitions"":[
        {""definition"":""Beyond expression; indescribable or unspeakable."",""synonyms"":[],""antonyms"":[],""example"":""ineffable joy""},
        {""definition"":""Incapable of being expressed in words."",""synonyms"":[],""antonyms"":[]}]},
        {""partOfSpeech"":""noun"",""definitions"":[{""definition"":""Something that cannot be expressed in words."",""synonyms"":[],""antonyms"":[]}]}]}]";

    private const string NotFoundJson = @"{""title"":""No Definitions Found"",""message"":""Sorry pal, we couldn't find definitions for the word you were looking for."",""resolution"":""You can try the search again at later time or head to the web instead.""}";

    [Fact]
    public void ParsesDefinitions()
    {
        var r = EnglishDictionaryClient.ParseResponse("ineffable", HttpStatusCode.OK, IneffableJson);
        Assert.Equal(LookupStatus.Found, r.Status);
        Assert.Equal("/ɪnˈɛfəbl̩/", r.Definition!.Phonetic);
        Assert.Equal(3, r.Definition.Senses.Count);
        Assert.Equal("adjective", r.Definition.Senses[0].PartOfSpeech);
        Assert.Equal("ineffable joy", r.Definition.Senses[0].Example);
        Assert.Equal("noun", r.Definition.Senses[2].PartOfSpeech);
    }

    [Fact]
    public void ParsesNotFoundAndErrors()
    {
        Assert.Equal(LookupStatus.NotFound, EnglishDictionaryClient.ParseResponse("zzz", HttpStatusCode.NotFound, NotFoundJson).Status);
        Assert.Equal(LookupStatus.Error, EnglishDictionaryClient.ParseResponse("x", (HttpStatusCode)429, "").Status);
        Assert.Equal(LookupStatus.Error, EnglishDictionaryClient.ParseResponse("x", HttpStatusCode.OK, "<html>").Status);
    }

    [Theory]
    [InlineData("Ineffable,", "ineffable")]
    [InlineData("  Revachol's ", "revachol")]
    [InlineData("esprit de corps", "esprit de corps")]
    [InlineData("1984", null)]
    [InlineData("", null)]
    public void NormalizesQueries(string raw, string? expected)
    {
        Assert.Equal(expected, EnglishDictionaryClient.NormalizeQuery(raw));
    }

    [Fact]
    public void CandidateFormsCoverCommonInflections()
    {
        Assert.Contains("vicissitude", EnglishDictionaryClient.CandidateForms("vicissitudes"));
        Assert.Contains("party", EnglishDictionaryClient.CandidateForms("parties"));
        Assert.Contains("dance", EnglishDictionaryClient.CandidateForms("dancing"));
        Assert.Equal("ineffable", EnglishDictionaryClient.CandidateForms("ineffable")[0]);
    }

    [Fact]
    public async Task LooksUpFallsBackToStemAndCaches()
    {
        var handler = new FakeHandler(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["vicissitudes"] = (HttpStatusCode.NotFound, NotFoundJson),
            ["vicissitude"] = (HttpStatusCode.OK, IneffableJson.Replace("ineffable", "vicissitude")),
        });
        var cache = Path.Combine(Path.GetTempPath(), "dd-cache-" + System.Guid.NewGuid() + ".json");
        try
        {
            using (var client = new EnglishDictionaryClient(cache, handler, "https://example.test/"))
            {
                var r = await client.LookupAsync("Vicissitudes");
                Assert.Equal(LookupStatus.Found, r.Status);
                Assert.Equal(2, handler.Requests);

                r = await client.LookupAsync("vicissitudes");
                Assert.Equal(LookupStatus.Found, r.Status);
                Assert.Equal(2, handler.Requests); // served from memory
                client.SaveCache();
            }

            using (var again = new EnglishDictionaryClient(cache, handler, "https://example.test/"))
            {
                var r = await again.LookupAsync("vicissitudes");
                Assert.Equal(LookupStatus.Found, r.Status);
                Assert.Equal(2, handler.Requests); // served from the file
            }
        }
        finally
        {
            File.Delete(cache);
        }
    }

    [Fact]
    public async Task NetworkFailureIsReportedNotThrown()
    {
        var handler = new FakeHandler(new Dictionary<string, (HttpStatusCode, string)>(), fail: true);
        using var client = new EnglishDictionaryClient(null, handler, "https://example.test/");
        var r = await client.LookupAsync("word");
        Assert.Equal(LookupStatus.Error, r.Status);
        Assert.Contains("Couldn't reach", r.Message);
    }

    [Fact]
    public void FormatsEnglishResult()
    {
        var r = EnglishDictionaryClient.ParseResponse("ineffable", HttpStatusCode.OK, IneffableJson);
        var text = EntryFormatter.FormatEnglish(r);
        Assert.Contains("ineffable", text);
        Assert.Contains("adjective", text);
        Assert.Contains("1. Beyond expression", text);
        Assert.Contains("2. Incapable", text);
        Assert.Contains("noun", text);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Code, string Body)> _responses;
        private readonly bool _fail;

        public FakeHandler(Dictionary<string, (HttpStatusCode, string)> responses, bool fail = false)
        {
            _responses = responses;
            _fail = fail;
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (_fail)
                throw new HttpRequestException("no network");
            var word = WebUtility.UrlDecode(request.RequestUri!.AbsolutePath.Trim('/'));
            var (code, body) = _responses.TryGetValue(word, out var r) ? r : (HttpStatusCode.NotFound, NotFoundJson);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
