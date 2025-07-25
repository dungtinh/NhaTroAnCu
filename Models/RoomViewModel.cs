using System;

namespace NhaTroAnCu.Models
{
    public class RoomViewModel
    {
        public Room Room { get; set; }
        public string ColorClass { get; set; }
        public string TenantName { get; set; }
        public bool IsContractNearingEnd { get; set; }
        public DateTime? ContractEndDate { get; set; }
    }
}