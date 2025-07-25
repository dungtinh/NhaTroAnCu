using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class TenantsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Tenants
        public ActionResult Index()
        {
            return View(db.Tenants.ToList());
        }

        // GET: /Tenants/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /Tenants/Create
        [HttpPost]
        public ActionResult Create(Tenant tenant)
        {
            if (ModelState.IsValid)
            {
                db.Tenants.Add(tenant);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(tenant);
        }
    }

}