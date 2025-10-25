using System.Web;
using System.Web.Optimization;

namespace SmartTable
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            // Vẫn giữ lại Bootstrap JS nếu bạn cần các tính năng JS của nó
            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            // Bundle CSS bây giờ CHỈ còn lại site.css (nếu bạn có dùng)
            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/site.css")); // ĐÃ XÓA bootstrap.css
        }
    }
}