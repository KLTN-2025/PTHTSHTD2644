using System;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace SmartTable.Filters
{
    /// <summary>
    /// Validate Anti-Forgery cho request JSON/AJAX.
    /// - Ưu tiên đọc token từ HEADER: RequestVerificationToken (hoặc X-RequestVerificationToken)
    /// - Fallback đọc từ FORM: __RequestVerificationToken
    /// - Cookie token lấy từ cookie __RequestVerificationToken
    /// </summary>
    public class ValidateAntiForgeryTokenJsonAttribute : FilterAttribute, IAuthorizationFilter
    {
        private static bool IsAjaxOrJsonRequest(HttpRequestBase request)
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

        private static string GetCookieToken(HttpRequestBase request)
        {
            var cookie = request?.Cookies["__RequestVerificationToken"];
            return cookie != null ? cookie.Value : null;
        }

        private static string GetRequestToken(HttpRequestBase request)
        {
            if (request == null) return null;

            // Ưu tiên token từ header (chuẩn cho fetch)
            var headerToken = request.Headers["RequestVerificationToken"];
            if (!string.IsNullOrWhiteSpace(headerToken)) return headerToken;

            headerToken = request.Headers["X-RequestVerificationToken"];
            if (!string.IsNullOrWhiteSpace(headerToken)) return headerToken;

            // Fallback token từ form field
            var formToken = request.Form["__RequestVerificationToken"];
            if (!string.IsNullOrWhiteSpace(formToken)) return formToken;

            return null;
        }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var request = filterContext?.HttpContext?.Request;

            try
            {
                // Nếu có đủ cookie token + request token => validate theo cặp
                var cookieToken = GetCookieToken(request);
                var requestToken = GetRequestToken(request);

                if (!string.IsNullOrWhiteSpace(cookieToken) && !string.IsNullOrWhiteSpace(requestToken))
                {
                    AntiForgery.Validate(cookieToken, requestToken);
                }
                else
                {
                    // Fallback: cơ chế mặc định (form post truyền thống)
                    AntiForgery.Validate();
                }
            }
            catch (HttpAntiForgeryException)
            {
                if (IsAjaxOrJsonRequest(request))
                {
                    filterContext.HttpContext.Response.StatusCode = 400;
                    filterContext.Result = new JsonResult
                    {
                        Data = new
                        {
                            success = false,
                            message = "Thiếu hoặc hết hạn mã bảo mật. Hãy F5 tải lại trang rồi thao tác lại."
                        },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }

                throw;
            }
        }
    }
}
