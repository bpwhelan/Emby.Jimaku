using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Codec = Jellyfin.Jimakufin.JsonCodec;
using System.Threading;
using System.Threading.Tasks;
using Emby.Jimaku.Model;
using Jimaku.Shared;
using Xunit;

public class JimakuClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Respond(request, cancellationToken);
    }

    private static HttpResponseMessage Json(string value) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) };

    [Fact]
    public async Task UnrelatedMultiValueIdsDoNotBlockRentAGirlfriendSeasonFour()
    {
        // Record 6367 from Anime-IDs contains a comma-separated MAL ID, unrelated to this series.
        // Set JIMAKU_TEST_MAPPINGS to a downloaded dataset to run the same scenario against it.
        var datasetPath = Environment.GetEnvironmentVariable("JIMAKU_TEST_MAPPINGS");
        var mappings = string.IsNullOrEmpty(datasetPath)
            ? "{\"6367\":{\"tvdb_id\":79414,\"tvdb_season\":1,\"tvdb_epoffset\":0,\"anilist_id\":4382,\"mal_id\":\"849,4382\"},\"rent4\":{\"tvdb_id\":380654,\"tvdb_season\":4,\"tvdb_epoffset\":0,\"mal_id\":59277,\"anilist_id\":179344}}"
            : File.ReadAllText(datasetPath);
        var apiCalls = 0;
        using var http = new HttpClient(new Handler { Respond = (request, _) =>
        {
            if (request.RequestUri.Host == "raw.githubusercontent.com") return Task.FromResult(Json(mappings));
            apiCalls++;
            if (apiCalls == 1)
            {
                Assert.EndsWith("entries/search?anilist_id=179344", request.RequestUri.AbsoluteUri);
                return Task.FromResult(Json("[{\"id\":123}]"));
            }
            Assert.EndsWith("entries/123/files?episode=5", request.RequestUri.AbsoluteUri);
            return Task.FromResult(Json("[{\"name\":\"episode05.ass\",\"url\":\"https://jimaku.cc/episode05.ass\"}]"));
        }});
        var files = await new JimakuClient(http, new Codec(), () => "test-key").SearchAsync("380654", 4, 5, "jpn", default);
        Assert.Equal("episode05.ass", Assert.Single(files).Name);
        Assert.Equal(2, apiCalls);
    }

    [Fact]
    public async Task SearchMapsSeriesAndAdjustsEpisodeOffsetAndUsesCurrentKey()
    {
        var key = "first";
        var calls = 0;
        using var http = new HttpClient(new Handler { Respond = (request, token) =>
        {
            calls++;
            var url = request.RequestUri.AbsoluteUri;
            if (url.Contains("anime_ids.json"))
            {
                Assert.False(request.Headers.Contains("Authorization"));
                return Task.FromResult(Json("{\"1\":{\"tvdb_id\":42,\"tvdb_season\":1,\"tvdb_epoffset\":0,\"anilist_id\":10},\"2\":{\"tvdb_id\":42,\"tvdb_season\":1,\"tvdb_epoffset\":12,\"anilist_id\":20}}"));
            }
            Assert.Equal(key, string.Join("", request.Headers.GetValues("Authorization")));
            if (url.EndsWith("search?anilist_id=20")) return Task.FromResult(Json("[{\"id\":123}]"));
            Assert.EndsWith("entries/123/files?episode=1", url);
            return Task.FromResult(Json("[{\"name\":\"日本語.ass\",\"url\":\"https://jimaku.cc/file.ass\"}]"));
        }});
        var client = new JimakuClient(http, new Codec(), () => key);
        Assert.Single(await client.SearchAsync("42", 1, 13, "jpn", default));
        key = "updated";
        Assert.Single(await client.SearchAsync("42", 1, 13, "ja", default));
        Assert.Equal(6, calls);
    }

    [Theory]
    [InlineData(null, 1, 1, "jpn", "key")]
    [InlineData("42", null, 1, "jpn", "key")]
    [InlineData("42", 1, null, "jpn", "key")]
    [InlineData("42", 1, 1, "eng", "key")]
    [InlineData("42", 1, 1, "jpn", "")]
    public async Task IncompleteOrUnsupportedRequestsDoNotUseNetwork(string tvdb, int? season, int? episode, string language, string key)
    {
        using var http = new HttpClient(new Handler { Respond = (_, _) => throw new Exception("Unexpected HTTP request") });
        Assert.Empty(await new JimakuClient(http, new Codec(), () => key).SearchAsync(tvdb, season, episode, language, default));
    }

    [Fact]
    public void MappingPrefersExactSeasonAndHighestApplicableOffset()
    {
        var exact = new MediaMapping { tvdb_id = 42, tvdb_season = 2, tvdb_epoffset = 12, anilist_id = 20 };
        var mappings = new[] {
            new MediaMapping { tvdb_id = 42, tvdb_season = -1, anilist_id = 1 },
            new MediaMapping { tvdb_id = 42, tvdb_season = 2, anilist_id = 10 }, exact,
            new MediaMapping { tvdb_id = 42, tvdb_season = 2, tvdb_epoffset = 24, anilist_id = 30 }
        };
        Assert.Same(exact, JimakuClient.SelectMapping(mappings, 42, 2, 13));
        Assert.Equal(10, JimakuClient.SelectMapping(mappings, 42, 2, 12).anilist_id);
        Assert.Null(JimakuClient.SelectMapping(mappings, 99, 2, 13));
    }

    [Fact]
    public async Task DownloadReturnsReadableStreamWithoutLeakingKey()
    {
        using var http = new HttpClient(new Handler { Respond = (request, token) =>
        {
            Assert.False(request.Headers.Contains("Authorization"));
            return Task.FromResult(Json("subtitle text"));
        }});
        var client = new JimakuClient(http, new Codec(), () => "secret");
        using var stream = await client.DownloadAsync("https://files.example/sub.ASS?download=1", default);
        Assert.Equal(0, stream.Position);
        using var reader = new StreamReader(stream);
        Assert.Equal("subtitle text", await reader.ReadToEndAsync());
        Assert.Equal("ass", JimakuClient.GetFormat("https://files.example/sub.ASS?download=1"));
    }

    [Fact]
    public void LegacySubtitleIdsStillDecode()
    {
        using var http = new HttpClient();
        var client = new JimakuClient(http, new Codec(), () => "key");
        const string url = "https://jimaku.cc/日本語.ass";
        var legacy = Convert.ToBase64String(Encoding.Unicode.GetBytes("\"" + url + "\""));
        Assert.Equal(url, client.DecodeId(legacy));
        Assert.Equal(url, client.DecodeId(client.EncodeId(url)));
    }

    [Fact]
    public async Task HttpErrorsArePropagatedInsteadOfReturningBrokenSubtitles()
    {
        using var http = new HttpClient(new Handler { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)) });
        var client = new JimakuClient(http, new Codec(), () => "key");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("42", 1, 1, "jpn", default));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.DownloadAsync("https://jimaku.cc/file.ass", default));
    }

    [Fact]
    public async Task UnmappedEntriesWithNullSeasonsDoNotBreakSearch()
    {
        using var http = new HttpClient(new Handler { Respond = (_, _) => Task.FromResult(Json("{\"unmapped\":{\"tvdb_id\":null,\"tvdb_season\":null,\"tvdb_epoffset\":0,\"anilist_id\":123}}")) });
        var client = new JimakuClient(http, new Codec(), () => "key");
        Assert.Empty(await client.SearchAsync("42", 1, 1, "jpn", default));
    }

    [Fact]
    public async Task DiagnosticsExplainMissingSeriesIdWithoutExposingApiKey()
    {
        var messages = new List<string>();
        using var http = new HttpClient(new Handler { Respond = (_, _) => throw new Exception("Unexpected network call") });
        var client = new JimakuClient(http, new Codec(), () => "secret-api-key", messages.Add);
        Assert.Empty(await client.SearchAsync(null, 1, 1, "jpn", default));
        Assert.Contains(messages, message => message.Contains("parent series has no valid TVDB ID"));
        Assert.DoesNotContain(messages, message => message.Contains("secret-api-key"));
    }

    [Fact]
    public async Task DiagnosticsReportHttpStatusBeforeAuthenticationFailure()
    {
        var messages = new List<string>();
        using var http = new HttpClient(new Handler { Respond = (request, _) => Task.FromResult(
            request.RequestUri.Host == "jimaku.cc"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json("{\"1\":{\"tvdb_id\":42,\"tvdb_season\":1,\"tvdb_epoffset\":0,\"anilist_id\":10}}")) });
        var client = new JimakuClient(http, new Codec(), () => "secret-api-key", messages.Add);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("42", 1, 1, "jpn", default));
        Assert.Contains(messages, message => message.Contains("Mapped TVDB 42"));
        Assert.Contains(messages, message => message.Contains("HTTP 401"));
        Assert.DoesNotContain(messages, message => message.Contains("secret-api-key"));
    }

    [Fact]
    public async Task DiagnosticsDistinguishNoMappingFromNoJimakuEntries()
    {
        var messages = new List<string>();
        using var http = new HttpClient(new Handler { Respond = (request, _) => Task.FromResult(Json(
            request.RequestUri.Host == "jimaku.cc" ? "[]" : "{\"1\":{\"tvdb_id\":42,\"tvdb_season\":1,\"tvdb_epoffset\":0,\"anilist_id\":10}}")) });
        var client = new JimakuClient(http, new Codec(), () => "key", messages.Add);
        Assert.Empty(await client.SearchAsync("99", 1, 1, "jpn", default));
        Assert.Contains(messages, message => message.Contains("No AniList mapping"));
        messages.Clear();
        Assert.Empty(await client.SearchAsync("42", 1, 1, "jpn", default));
        Assert.Contains(messages, message => message.Contains("Jimaku returned 0 entries"));
        Assert.DoesNotContain(messages, message => message.Contains("No AniList mapping"));
    }

    [Fact]
    public async Task CancellationReachesPendingHttpRequest()
    {
        using var cts = new CancellationTokenSource();
        using var http = new HttpClient(new Handler { Respond = async (_, token) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        }});
        var client = new JimakuClient(http, new Codec(), () => "key");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SearchAsync("42", 1, 1, "jpn", cts.Token));
    }
}

