using System.Collections.Generic;
using SmartTable.Models; // <-- BẠN CẦN THÊM DÒNG NÀY

namespace SmartTable.Models.ViewModels
{
    public class SmartTableDetailViewModel
    {
        public Restaurants Restaurant { get; set; } // Giả sử tên lớp Model là 'Restaurants' (số nhiều)
        public List<MenuItems> MenuItems { get; set; } // Giả sử tên lớp Model là 'MenuItems' (số nhiều)
        public List<Reviews> Reviews { get; set; } // Giả sử tên lớp Model là 'Reviews' (số nhiều)
    }
}