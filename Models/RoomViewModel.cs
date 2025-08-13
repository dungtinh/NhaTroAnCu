using System;

namespace NhaTroAnCu.Models
{
    public class RoomViewModel
    {
        public Room Room { get; set; }
        public string ColorClass { get; set; }
        public string TenantName { get; set; }
        public bool IsContractNearingEnd { get; set; }
        public bool IsContractExpired { get; set; } // Thêm property mới
        public DateTime? ContractEndDate { get; set; }
    }
}