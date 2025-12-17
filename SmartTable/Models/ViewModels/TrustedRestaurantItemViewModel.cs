namespace SmartTable.Models.ViewModels
{
    public class TrustedRestaurantItemViewModel
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Image { get; set; }
        public decimal AvgRating { get; set; }
        public int ReviewCount { get; set; }
        public string City { get; set; }
        public string Area { get; set; }
    }
}
