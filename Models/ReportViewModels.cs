using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NhaTroAnCu.Models
{
    public class TenantReportViewModel
    {
        public int Id { get; set; }

        // Tenant Info
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string Ethnicity { get; set; }
        public string PermanentAddress { get; set; }
        public string VehiclePlate { get; set; }
        public string Photo { get; set; }

        // Room Info
        public int RoomId { get; set; }
        public string RoomName { get; set; }

        // Contract Info
        public int ContractId { get; set; }
        public string ContractType { get; set; }
        public string ContractStatus { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? MoveInDate { get; set; }

        // Company Info (if applicable)
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }

        public bool IsPrimary { get; set; }
    }
}