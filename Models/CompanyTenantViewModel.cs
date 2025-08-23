using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class CompanyTenantViewModel
    {
        public int? Id { get; set; }
        public int RoomId { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string Ethnicity { get; set; }
        public string PermanentAddress { get; set; }
        public string VehiclePlate { get; set; }
        public string Photo { get; set; }
        public bool IsNew { get; set; }
    }
}