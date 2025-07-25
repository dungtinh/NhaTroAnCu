using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class CardData
    {
        public string id { get; set; }
        public string id_prob { get; set; }
        public string name { get; set; }
        public string name_prob { get; set; }
        public string dob { get; set; }
        public string dob_prob { get; set; }
        public string sex { get; set; }
        public string sex_prob { get; set; }
        public string nationality { get; set; }
        public string nationality_prob { get; set; }
        public string home { get; set; }
        public string home_prob { get; set; }
        public string address { get; set; }
        public string address_prob { get; set; }
        public AddressEntities address_entities { get; set; }
        public string doe { get; set; } // Date of Expiry
        public string doe_prob { get; set; }
        public string ethnicity { get; set; }
        public string ethnicity_prob { get; set; }
        public string religion { get; set; }
        public string religion_prob { get; set; }
        public string features { get; set; }
        public string features_prob { get; set; }
        public string issue_date { get; set; }
        public string issue_date_prob { get; set; }
        public string issue_loc { get; set; }
        public string issue_loc_prob { get; set; }
        public string type { get; set; }
        public string type_new { get; set; }
    }
}