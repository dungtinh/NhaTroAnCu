using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class UtilityBillCreateViewModel
    {
        public int RoomId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int ContractId { get; set; }
        public int WaterIndexStart { get; set; }
        public int WaterIndexEnd { get; set; }
        public decimal ElectricityAmount { get; set; }
        public decimal WaterPrice { get; set; }
        public decimal RentAmount { get; set; }
        public decimal ExtraCharge { get; set; }
        public decimal Discount { get; set; }
        public string BillNote { get; set; }
        public string BillStatus { get; set; }
        public decimal TotalAmount { get; set; }        
    }
}