using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class ContractRoomsViewModel
    {
        public Contract Contract { get; set; }
        public List<RoomSelectionModel> SelectedRooms { get; set; }
        public ContractRoomsViewModel()
        {
            Contract = new Contract();
            SelectedRooms = new List<RoomSelectionModel>();
        }

    }
}