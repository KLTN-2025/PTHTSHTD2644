using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SmartTable.Filters
{
    public class AuthorizeUserAttribute : ActionFilterAttribute
    {
        private bool IsAjaxOrJson(HttpRequestBase request)
        {
            if (request == null) return false;

            var xrw = request.Headers["X-Requested-With"];
            if (!string.IsNullOrWhiteSpace(xrw) &&
                xrw.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return true;

            var accept = request.Headers["Accept"] ?? "";
            if (accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var ct = request.ContentType ?? "";
            if (ct.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var sess = filterContext.HttpContext.Session;

            // bạn đang dùng session "user_id" => giữ đúng key này
            if (sess == null || sess["user_id"] == null)
            {
                // ✅ Ajax/JSON: trả JSON, không redirect HTML
                if (IsAjaxOrJson(filterContext.HttpContext.Request))
                {
                    filterContext.HttpContext.Response.StatusCode = 401;
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng tải lại trang và đăng nhập lại." },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }

                // ✅ Request thường: redirect
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" }
                    });
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
