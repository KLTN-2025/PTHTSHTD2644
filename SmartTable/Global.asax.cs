using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Configuration;
using SmartTable.Models;
using System.Threading.Tasks;

namespace SmartTable
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Bật TLS1.2 cho .NET Framework 4.6.1 nếu cần
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }

    public class ChatController : Controller
    {
        // Add near controller class (simple in-memory throttle)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastCall = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();
        private const int MinSecondsBetweenCalls = 3;

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> SendMessage(string userMessage)
        {
            // lấy systemPrompt từ đâu đó, ví dụ từ ứng dụng cấu hình hoặc cơ sở dữ liệu
            string systemPrompt = "Bạn là một trợ lý ảo.";

            // trước khi tạo requestData
            var modelName = ConfigurationManager.AppSettings["DeepSeekModel"] ?? "deepseek-r1";

            var requestData = new DeepSeekRequest
            {
                Model = modelName,
                Messages = new List<DeepSeekMessage>
                {
                    new DeepSeekMessage { Role = "system", Content = systemPrompt },
                    new DeepSeekMessage { Role = "user", Content = userMessage }
                }
            };

            // Throttle: check time between calls
            var clientKey = (User?.Identity?.Name) ?? Request.UserHostAddress ?? "anon";
            if (_lastCall.TryGetValue(clientKey, out var last) && (DateTime.UtcNow - last).TotalSeconds < MinSecondsBetweenCalls)
            {
                return Json(new { success = false, reply = $"Bạn gửi quá nhanh. Vui lòng chờ {(MinSecondsBetweenCalls - (int)(DateTime.UtcNow - last).TotalSeconds)} giây." });
            }
            _lastCall[clientKey] = DateTime.UtcNow;

            // Gọi helper async và map DeepSeekResult sang ActionResult (JSON)
            var result = await DeepSeekHelper.GetResponseAsync(requestData);
            if (result == null)
                return Json(new { success = false, reply = "Lỗi nội bộ khi gọi AI." });

            // detect rate-limit
            if (!result.Success && (result.Status?.Contains("429") == true || (result.Reply ?? "").ToLower().Contains("rate-limited")))
            {
                return Json(new {
                    success = false,
                    reply = "Dịch vụ AI đang bị giới hạn (429). Vui lòng thử lại sau vài giây. " +
                            "Hoặc thêm API key OpenRouter của bạn tại https://openrouter.ai/settings/integrations để tăng hạn mức."
                });
            }

            if (!result.Success)
                return Json(new { success = false, reply = result.Reply });

            return Json(new { success = true, reply = result.Reply });
        }
    }
}
