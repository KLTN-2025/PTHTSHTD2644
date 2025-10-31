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

            // bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
            //           "~/Scripts/bootstrap.js"));

            // THAY ĐỔI Ở DÒNG NÀY: Đổi 'ScriptBundle' thành 'Bundle'
            bundles.Add(new Bundle("~/bundles/main").Include(
                      "~/Scripts/main.js")); // <-- Đảm bảo tên file này đúng

            // STYLE BUNDLE CẦN CÓ OUTPUT.CSS (NẾU DÙNG BUILD)
            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/site.css",
                      "~/Content/output.css")); // <-- Thêm output.css vào đây
        }
    }
}