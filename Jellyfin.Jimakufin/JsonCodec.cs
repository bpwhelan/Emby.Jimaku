using System.Text.Json;
using Jimaku.Shared;

namespace Jellyfin.Jimakufin
{
    public sealed class JsonCodec : IJsonCodec
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        public T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
        public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    }
}
