using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class ApiResponse
    {
        public int errorCode { get; set; }
        public string errorMessage { get; set; }
        public List<CardData> data { get; set; }
        public string filename { get; set; }
    }
}