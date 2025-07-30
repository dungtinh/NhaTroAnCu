using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class ContractCreateViewModel
    {
        public int RoomId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime MoveInDate { get; set; }
        public int Months { get; set; }
        public decimal PriceAgreed { get; set; }
        public decimal DepositAmount { get; set; }
        public string Note { get; set; }

        public decimal ElectricityPrice { get; set; }
        public decimal WaterPrice { get; set; }



        // Danh sách người thuê
        public List<TenantInputModel> Tenants { get; set; } = new List<TenantInputModel>();
    }

    public class TenantInputModel
    {
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string PermanentAddress { get; set; }
        public string Ethnicity { get; set; }        // Thêm mới
        public string VehiclePlate { get; set; }     // Thêm mới
    }

}