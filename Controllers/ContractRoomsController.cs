
using NhaTroAnCu.Models;
using System.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class ContractRoomsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();
        // GET: ContractRooms
        public ActionResult Index(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }
            var model = new ContractRoomsViewModel
            {
                Contract = contract,
            };
            model.SelectedRooms = contract.ContractRooms.Select(cr => new RoomSelectionModel
            {
                RoomId = cr.RoomId,
                RoomName = cr.Room.Name,
                DefaultPrice = cr.Room.DefaultPrice,
                AgreedPrice = cr.PriceAgreed,
                IsSelected = true,
                Notes = cr.Notes
            }).ToList();
            return View(model);
        }
    }
}