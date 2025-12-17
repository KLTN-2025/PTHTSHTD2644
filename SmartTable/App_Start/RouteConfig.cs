using System.Web.Mvc;
using System.Web.Routing;

namespace SmartTable
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ThongBaoBusinessIndex",
                url: "ThongBao/BusinessIndex/{id}",
                defaults: new { controller = "ThongBao", action = "BusinessIndex", id = UrlParameter.Optional },
                namespaces: new[] { "SmartTable.Controllers" }
            );

            routes.MapRoute(
                name: "ThongBaoBusiness",
                url: "ThongBao/Business/{id}",
                defaults: new { controller = "ThongBao", action = "Business", id = UrlParameter.Optional },
                namespaces: new[] { "SmartTable.Controllers" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "SmartTable.Controllers" }
            );
        }
    }
}
