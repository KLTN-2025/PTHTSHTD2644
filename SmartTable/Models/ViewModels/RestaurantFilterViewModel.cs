using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class RestaurantFilterViewModel
    {
        public string City { get; set; } 

        public string Area { get; set; }
        public string RestaurantType { get; set; }
        public string AveragePrice { get; set; }
        public string MainDish { get; set; }
        public string SuitableFor { get; set; }
        public string CuisineTag { get; set; }
        public string SearchKeyword { get; set; }

        public List<Restaurants> Results { get; set; }

        public List<string> AvailableAreas { get; set; }

        public RestaurantFilterViewModel()
        {
            Results = new List<Restaurants>();
            AvailableAreas = new List<string>();
        }
    }
}
