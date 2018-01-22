using System;

namespace Prometheus
{
    using System.Net.Http;

    internal class NetworkClient : IDisposable
    {
        // HttpClient is designed to be reused, maintain only a single instance
        static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() => new HttpClient());

        public void UploadData(Uri endPoint, byte[] data)
        {
            httpClient.Value.PostAsync(endPoint, new ByteArrayContent(data)).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            // No need to do anything, HttpClient is reused
        }
    }
}
