using SmartTable.Models; // Thêm (để dùng model 'Users')
using System.Web.Mvc;
using System.Web.Routing;

namespace SmartTable.Filters // Namespace này phải khớp với 'using'
{
    public class AuthorizeAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // 1. Kiểm tra xem đã đăng nhập chưa
            if (filterContext.HttpContext.Session["user_id"] == null)
            {
                // Nếu chưa, đá về trang Login
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "area", "" } // Quay về khu vực chính
                    });
                return;
            }

            // 2. Kiểm tra xem có phải Admin không
            var userRole = filterContext.HttpContext.Session["role"]?.ToString();
            if (userRole != "Admin")
            {
                // Nếu là User hoặc Business, đá về Trang chủ
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Home" },
                        { "action", "Index" },
                        { "area", "" } // Quay về khu vực chính
                    });
            }

            base.OnActionExecuting(filterContext);
        }
    }
}