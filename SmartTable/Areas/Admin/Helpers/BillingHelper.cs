using System;

namespace SmartTable.Helpers
{
    public static class BillingHelper
    {
        public const decimal BASIC_FEE = 500000m; // Gói Bắt đầu
        public const decimal PRO_FEE = 700000m; // Gói Cốt lõi

        public static decimal GetPlanFee(string servicePackage)
        {
            var s = (servicePackage ?? "").Trim();

            if (s.Equals("Gói Cốt lõi", StringComparison.OrdinalIgnoreCase)) return PRO_FEE;
            if (s.Equals("Gói Bắt đầu", StringComparison.OrdinalIgnoreCase)) return BASIC_FEE;

            return 0m;
        }
    }
}
