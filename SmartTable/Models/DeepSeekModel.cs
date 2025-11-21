using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartTable.Models
{
    // Tin nhắn (message) trong payload
    public class DeepSeekMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        // Một số API dùng "content", một số dùng "text"
        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string Content { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
    }

    // Request gửi tới Deepseek
    public class DeepSeekRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<DeepSeekMessage> Messages { get; set; }

        // Tùy chọn (nếu cần)
        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
        public double? Temperature { get; set; }

        [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxTokens { get; set; }
    }

    // Choice (một phần của response)
    public class DeepSeekChoice
    {
        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public DeepSeekMessage Message { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
    }

    // Response chung từ Deepseek / OpenAI-like APIs
    public class DeepSeekResponse
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("object", NullValueHandling = NullValueHandling.Ignore)]
        public string Object { get; set; }

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public long? Created { get; set; }

        [JsonProperty("choices", NullValueHandling = NullValueHandling.Ignore)]
        public List<DeepSeekChoice> Choices { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public object Error { get; set; }

        // Giữ raw nếu cần debug
        [JsonProperty("raw", NullValueHandling = NullValueHandling.Ignore)]
        public object Raw { get; set; }
    }

    // Helper result to avoid MVC types / tuples
    public class DeepSeekResult
    {
        public bool Success { get; set; }
        public string Reply { get; set; }
        public string Raw { get; set; }
        public string Status { get; set; }
    }
}