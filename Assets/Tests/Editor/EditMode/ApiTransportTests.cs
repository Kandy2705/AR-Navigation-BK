using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// HTTP GET/POST JSON qua <see cref="ApiHelper"/> (mock server cục bộ).
    /// </summary>
    [Category("TestAR")]
    public sealed class ApiTransportTests
    {
        [Serializable]
        private class ValueResponse
        {
            public int value;
        }

        [Serializable]
        private class EchoRequest
        {
            public string message;
        }

        [Serializable]
        private class EchoResponse
        {
            public string message;
            public string method;
        }

        [Test]
        public async Task ApiHelper_Get_ParsesJson()
        {
            using var server = new LocalHttpServer(async ctx =>
            {
                if (ctx.Request.HttpMethod != "GET")
                {
                    ctx.Response.StatusCode = 405;
                    ctx.Response.Close();
                    return;
                }

                await WriteJson(ctx, 200, "{\"value\":123}");
            });

            var url = server.BaseUrl + "/value";
            var res = await ApiHelper.Get<ValueResponse>(url);

            Assert.NotNull(res);
            Assert.AreEqual(123, res.value);
        }

        [Test]
        public async Task ApiHelper_Post_SendsJsonAndParsesResponse()
        {
            using var server = new LocalHttpServer(async ctx =>
            {
                if (ctx.Request.HttpMethod != "POST")
                {
                    ctx.Response.StatusCode = 405;
                    ctx.Response.Close();
                    return;
                }

                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    body = await reader.ReadToEndAsync();

                Assert.IsTrue(body.Contains("\"message\"", StringComparison.Ordinal));

                await WriteJson(ctx, 200, "{\"message\":\"ok\",\"method\":\"POST\"}");
            });

            var url = server.BaseUrl + "/echo";
            var res = await ApiHelper.Post<EchoResponse>(url, new EchoRequest { message = "hello" });

            Assert.NotNull(res);
            Assert.AreEqual("ok", res.message);
            Assert.AreEqual("POST", res.method);
        }

        private static async Task WriteJson(HttpListenerContext ctx, int statusCode, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = payload.Length;
            await ctx.Response.OutputStream.WriteAsync(payload, 0, payload.Length);
            ctx.Response.OutputStream.Close();
            ctx.Response.Close();
        }

        private sealed class LocalHttpServer : IDisposable
        {
            private readonly HttpListener listener;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task loopTask;
            private readonly Func<HttpListenerContext, Task> handler;

            public string BaseUrl { get; }

            public LocalHttpServer(Func<HttpListenerContext, Task> handler)
            {
                this.handler = handler ?? throw new ArgumentNullException(nameof(handler));

                int port = GetFreeTcpPort();
                BaseUrl = $"http://127.0.0.1:{port}";

                listener = new HttpListener();
                listener.Prefixes.Add(BaseUrl + "/");
                listener.Start();

                loopTask = Task.Run(Loop);
            }

            private async Task Loop()
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var ctx = await listener.GetContextAsync();
                        _ = Task.Run(async () =>
                        {
                            try { await handler(ctx); }
                            catch
                            {
                                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { /* ignore */ }
                            }
                        }, cts.Token);
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (HttpListenerException) { break; }
                }
            }

            public void Dispose()
            {
                cts.Cancel();
                try { listener.Stop(); } catch { /* ignore */ }
                try { listener.Close(); } catch { /* ignore */ }
                try { loopTask.Wait(1000); } catch { /* ignore */ }
                cts.Dispose();
            }

            private static int GetFreeTcpPort()
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
        }
    }
}
