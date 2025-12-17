using System;

namespace SmartTable.Models
{
    public partial class ReviewReply
    {
        public int reply_id { get; set; }
        public int review_id { get; set; }
        public int restaurant_id { get; set; }
        public string reply_text { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }

        public virtual Reviews Reviews { get; set; }
        public virtual Restaurants Restaurants { get; set; }
    }
}