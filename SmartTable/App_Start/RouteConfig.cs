using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SmartTable
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // ROUTE MẶC ĐỊNH (Home/Index)
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                // FIX: Chỉ định namespace gốc để phân giải HomeController và PublicRestaurantController
                namespaces: new[] { "SmartTable.Controllers" }
            );

            // Route cho Area Admin được xử lý tự động bởi AdminAreaRegistration.cs
        }
    }
}