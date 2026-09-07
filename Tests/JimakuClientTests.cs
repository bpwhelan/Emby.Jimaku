using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Jimaku.Model;
using Jimaku.Shared;
using Xunit;

public class JimakuClientTests
{
    private sealed class Codec : IJsonCodec
    {
        public T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        public string Serialize<T>(T value) => JsonSerializer.Serialize(value);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Respond(request, cancellationToken);
    }

    private static HttpResponseMessage Json(string value) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) };

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

