using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class TenantManagerViewModel
    {
        public int ContractId { get; set; }
        public int RoomId { get; set; }
        public string ContractCode { get; set; }
        public string RoomName { get; set; }
        public string CompanyName { get; set; }
        public int? CompanyId { get; set; }
        public List<TenantViewModel> ContractTenants { get; set; }
        public List<Tenant> Tenants { get; set; } // For form binding

        public TenantManagerViewModel()
        {
            ContractTenants = new List<TenantViewModel>();
        }
    }

    public class SaveTenantsViewModel
    {
        public int ContractId { get; set; }
        public int RoomId { get; set; }
        public List<TenantDataViewModel> Tenants { get; set; }
    }

    public class TenantDataViewModel
    {
        public int? TenantId { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string PermanentAddress { get; set; }
        public string Ethnicity { get; set; }
        public string VehiclePlate { get; set; }
        public string Photo { get; set; }
        public int? CompanyId { get; set; }
    }
}