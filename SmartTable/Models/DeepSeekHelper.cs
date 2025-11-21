using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SmartTable.Models
{
    // HTTP helper to call OpenRouter / DeepSeek. Does NOT declare model classes or use MVC types.
    public static class DeepSeekHelper
    {
        private static readonly HttpClient client = new HttpClient();

        private const int MaxRetries = 4;
        private const int BaseDelayMs = 1000;
        private const int MaxJitterMs = 1000;

        public static async Task<DeepSeekResult> GetResponseAsync(DeepSeekRequest request)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var apiUrl = ConfigurationManager.AppSettings["DeepSeekApiUrl"];
                var apiKey = ConfigurationManager.AppSettings["DeepSeekApiKey"];
                var defaultModel = ConfigurationManager.AppSettings["DeepSeekModel"] ?? "deepseek-r1";

                if (string.IsNullOrWhiteSpace(apiUrl))
                        return new DeepSeekResult { Success = false, Reply = "Cấu hình DeepSeekApiUrl chưa được thiết lập." };

                if (string.IsNullOrWhiteSpace(apiKey))
                    return new DeepSeekResult { Success = false, Reply = "Cấu hình DeepSeekApiKey chưa được thiết lập." };

                if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri))
                    return new DeepSeekResult { Success = false, Reply = "DeepSeekApiUrl không hợp lệ." };

                if (request == null) request = new DeepSeekRequest();
                request.Model = string.IsNullOrWhiteSpace(request.Model) ? defaultModel : request.Model;

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var req = new HttpRequestMessage(HttpMethod.Post, apiUri))
                {
                    req.Content = content;
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    var referer = ConfigurationManager.AppSettings["OpenRouterReferer"];
                    var siteTitle = ConfigurationManager.AppSettings["OpenRouterTitle"];
                    if (!string.IsNullOrWhiteSpace(referer)) req.Headers.Add("HTTP-Referer", referer);
                    if (!string.IsNullOrWhiteSpace(siteTitle)) req.Headers.Add("X-Title", siteTitle);

                    var retry = await SendWithRetriesAsync(req);
                    Debug.WriteLine("DeepSeekHelper: final status: " + retry.Status);

                    if (!retry.Success)
                        return new DeepSeekResult { Success = false, Reply = retry.Body, Raw = retry.Body, Status = retry.Status };

                    // try parse response
                    try
                    {
                        var dsResp = JsonConvert.DeserializeObject<DeepSeekResponse>(retry.Body);
                        if (dsResp?.Choices != null && dsResp.Choices.Count > 0)
                        {
                            var first = dsResp.Choices[0];
                            string reply = null;
                            if (first?.Message != null)
                                reply = first.Message.Content ?? first.Message.Text;
                            else if (!string.IsNullOrWhiteSpace(first?.Text))
                                reply = first.Text;

                            reply = string.IsNullOrWhiteSpace(reply) ? retry.Body : reply;
                            return new DeepSeekResult { Success = true, Reply = reply, Raw = retry.Body, Status = retry.Status };
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("DeepSeekHelper: deserialize failed: " + ex);
                    }

                    var fallback = string.IsNullOrWhiteSpace(retry.Body) ? "AI trả về kết quả rỗng" : retry.Body;
                    return new DeepSeekResult { Success = true, Reply = fallback, Raw = retry.Body, Status = retry.Status };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DeepSeekHelper: Unhandled: " + ex);
                return new DeepSeekResult { Success = false, Reply = "Lỗi khi xử lý yêu cầu AI: " + ex.GetBaseException().Message, Status = "Exception" };
            }
        }

        // internal result class to avoid tuples / ValueTuple dependency
        private class RetryResult
        {
            public bool Success { get; set; }
            public string Body { get; set; }
            public string Status { get; set; }
        }

        private static async Task<RetryResult> SendWithRetriesAsync(HttpRequestMessage req)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                HttpResponseMessage resp = null;
                string respText = null;
                try
                {
                    resp = await client.SendAsync(CloneHttpRequestMessage(req));
                    respText = await resp.Content.ReadAsStringAsync();

                    Debug.WriteLine($"DeepSeekHelper: attempt {attempt}, status {(int)resp.StatusCode} {resp.StatusCode}");
                    if (resp.Headers.Contains("Retry-After"))
                        Debug.WriteLine("DeepSeekHelper: Retry-After header = " + string.Join(",", resp.Headers.GetValues("Retry-After")));

                    int code = (int)resp.StatusCode;
                    if (code == 429 || code == 502 || code == 503 || code == 504)
                    {
                        if (attempt == MaxRetries)
                        {
                            var msg = !string.IsNullOrWhiteSpace(respText) ? respText : $"Dịch vụ AI trả lỗi {code}";
                            return new RetryResult { Success = false, Body = $"Dịch vụ AI trả lỗi quá nhiều yêu cầu: {code}. Body: {msg}", Status = resp.StatusCode.ToString() };
                        }

                        TimeSpan wait = TimeSpan.Zero;
                        if (resp.Headers.TryGetValues("Retry-After", out var values))
                        {
                            var raw = System.Linq.Enumerable.FirstOrDefault(values);
                            if (int.TryParse(raw, out int seconds))
                                wait = TimeSpan.FromSeconds(seconds);
                            else if (DateTimeOffset.TryParse(raw, out var dt))
                                wait = dt - DateTimeOffset.UtcNow;
                        }

                        if (wait <= TimeSpan.Zero)
                        {
                            var backoff = BaseDelayMs * Math.Pow(2, attempt);
                            var jitter = new Random().Next(0, MaxJitterMs);
                            wait = TimeSpan.FromMilliseconds(Math.Min(backoff + jitter, 30000));
                        }

                        Debug.WriteLine($"DeepSeekHelper: Received {code}, waiting {wait.TotalMilliseconds}ms before retry #{attempt + 1}");
                        await Task.Delay(wait);
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        var msg = !string.IsNullOrWhiteSpace(respText) ? respText : $"Dịch vụ AI trả lỗi {code}";
                        return new RetryResult { Success = false, Body = msg, Status = resp.StatusCode.ToString() };
                    }

                    return new RetryResult { Success = true, Body = respText, Status = resp.StatusCode.ToString() };
                }
                catch (HttpRequestException httpEx)
                {
                    Debug.WriteLine("DeepSeekHelper: HttpRequestException: " + httpEx);
                    if (attempt == MaxRetries)
                        return new RetryResult { Success = false, Body = "Lỗi khi gửi yêu cầu tới dịch vụ AI: " + httpEx.GetBaseException().Message, Status = "HttpRequestException" };

                    var backoff = BaseDelayMs * Math.Pow(2, attempt);
                    var jitter = new Random().Next(0, MaxJitterMs);
                    var wait = TimeSpan.FromMilliseconds(Math.Min(backoff + jitter, 30000));
                    Debug.WriteLine($"DeepSeekHelper: network error, waiting {wait.TotalMilliseconds}ms before retry #{attempt + 1}");
                    await Task.Delay(wait);
                    continue;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("DeepSeekHelper: Unexpected exception: " + ex);
                    return new RetryResult { Success = false, Body = "Lỗi nội bộ khi gọi AI: " + ex.GetBaseException().Message, Status = "Exception" };
                }
            }

            return new RetryResult { Success = false, Body = "Exceeded retry attempts", Status = "RetriesExceeded" };
        }

        private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);

            if (req.Content != null)
            {
                var ms = new System.IO.MemoryStream();
                req.Content.CopyToAsync(ms).GetAwaiter().GetResult();
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                if (req.Content.Headers != null)
                {
                    foreach (var h in req.Content.Headers)
                        clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            foreach (var header in req.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }
    }
}