using System.Collections.Generic;
using SmartTable.Models; 

namespace SmartTable.Models.ViewModels
{
    public class SmartTableDetailViewModel
    {
        public Restaurants Restaurant { get; set; } 
        public List<MenuItems> MenuItems { get; set; }
        public List<Reviews> Reviews { get; set; }
        public List<Restaurants> RelatedRestaurants { get; set; } = new List<Restaurants>();

    }
}