using System.Web.Mvc;
using System.Web.Routing;

namespace SmartTable.Filters // <-- Dòng này tạo ra namespace 'SmartTable.Filters'
{
    public class AuthorizeUserAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Kiểm tra xem session "user_id" có tồn tại không
            if (filterContext.HttpContext.Session["user_id"] == null)
            {
                // Nếu không, chuyển hướng người dùng về trang Đăng nhập
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" }
                    });
            }

            base.OnActionExecuting(filterContext);
        }
    }
}