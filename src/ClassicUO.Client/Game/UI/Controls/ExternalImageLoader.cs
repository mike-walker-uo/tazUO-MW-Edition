using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI.Controls
{
    /// <summary>Shares in-flight downloads without retaining GPU textures or URL data indefinitely.</summary>
    internal static class ExternalImageLoader
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _downloads =
            new ConcurrentDictionary<string, Lazy<Task<byte[]>>>(StringComparer.OrdinalIgnoreCase);

        public static async Task<byte[]> DownloadAsync(string url)
        {
            Lazy<Task<byte[]>> download = _downloads.GetOrAdd(
                url,
                key => new Lazy<Task<byte[]>>(
                    () => _httpClient.GetByteArrayAsync(key),
                    true
                )
            );

            try
            {
                return await download.Value.ConfigureAwait(false);
            }
            finally
            {
                _downloads.TryRemove(url, out _);
            }
        }
    }
}
