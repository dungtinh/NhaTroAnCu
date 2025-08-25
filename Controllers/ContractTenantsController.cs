using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class ContractTenantsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult TenantManager(int contractId, int roomId)
        {
            // Validate contract and room exist
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng!";
                return RedirectToAction("Index", "Contracts");
            }

            // Check if room belongs to this contract
            var contractRoom = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId);

            if (contractRoom == null)
            {
                TempData["Error"] = "Phòng không thuộc hợp đồng này!";
                return RedirectToAction("Details", "Contracts", new { id = contractId });
            }

            // Get existing tenants for this room and contract
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .ToList();

            var model = new TenantManagerViewModel
            {
                ContractId = contractId,
                RoomId = roomId,
                ContractCode = $"HD-{contract.Id:D6}",
                RoomName = contractRoom.Room.Name,
                CompanyName = contract.Company?.CompanyName ?? "Cá nhân",
                CompanyId = contract.CompanyId,
                ContractTenants = contractTenants.Select(ct => new TenantViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    Gender = ct.Tenant.Gender,
                    BirthDate = ct.Tenant.BirthDate,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,
                    CreatedAt = ct.CreatedAt
                }).ToList()
            };

            return View(model);
        }

        // POST: TenantContracts/SaveTenants
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTenants(TenantManagerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadViewData(model);
                return View("TenantManager", model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .FirstOrDefault(c => c.Id == model.ContractId);

                    if (contract == null)
                    {
                        throw new InvalidOperationException("Không tìm thấy hợp đồng");
                    }

                    // Validate room belongs to contract
                    if (!contract.ContractRooms.Any(cr => cr.RoomId == model.RoomId))
                    {
                        throw new InvalidOperationException("Phòng không thuộc hợp đồng này");
                    }

                    // Convert Tenants to TenantViewModel for processing
                    var tenantViewModels = model.Tenants?.Select(t => new TenantViewModel
                    {
                        TenantId = t.Id,
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate,
                        Photo = t.Photo
                    }).ToList();

                    // Process tenants based on contract type
                    if (contract.ContractType == "Company")
                    {
                        // Với hợp đồng công ty, truyền roomId cụ thể
                        TenantContractHelper.ProcessCompanyContractTenants(
                            db,
                            contract,
                            model.RoomId,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }
                    else
                    {
                        // Với hợp đồng cá nhân, không cần roomId vì chỉ có 1 phòng
                        TenantContractHelper.ProcessIndividualContractTenants(
                            db,
                            contract,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }

                    transaction.Commit();
                    TempData["Success"] = "Đã cập nhật thông tin người thuê thành công!";
                    return RedirectToAction("Details", "Contracts", new { id = model.ContractId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    LoadViewData(model);
                    return View("TenantManager", model);
                }
            }
        }

        private void LoadViewData(TenantManagerViewModel model)
        {
            // Reload contract and room info for display
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == model.ContractId);

            if (contract != null)
            {
                var room = contract.ContractRooms
                    .FirstOrDefault(cr => cr.RoomId == model.RoomId)?.Room;

                if (room != null)
                {
                    model.ContractCode = $"HD-{contract.Id:D6}";
                    model.RoomName = room.Name;
                    model.CompanyName = contract.Company?.CompanyName ?? "Cá nhân";
                    model.CompanyId = contract.CompanyId;
                }
            }
        }

        // POST: TenantContracts/RemoveTenant
        [HttpPost]
        public ActionResult RemoveTenant(int contractTenantId)
        {
            try
            {
                var contractTenant = db.ContractTenants.Find(contractTenantId);
                if (contractTenant == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người thuê" });
                }

                db.ContractTenants.Remove(contractTenant);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa người thuê" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: TenantContracts/GetTenants
        public ActionResult GetTenants(int contractId, int roomId)
        {
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .Select(ct => new
                {
                    id = ct.Id,
                    tenantId = ct.TenantId,
                    contractTenantId = ct.Id,
                    fullName = ct.Tenant.FullName,
                    identityCard = ct.Tenant.IdentityCard,
                    phoneNumber = ct.Tenant.PhoneNumber,
                    gender = ct.Tenant.Gender,
                    birthDate = ct.Tenant.BirthDate,
                    permanentAddress = ct.Tenant.PermanentAddress,
                    ethnicity = ct.Tenant.Ethnicity,
                    vehiclePlate = ct.Tenant.VehiclePlate,
                    photo = ct.Tenant.Photo,
                    createdAt = ct.CreatedAt
                })
                .ToList();

            return Json(contractTenants, JsonRequestBehavior.AllowGet);
        }

        // GET: TenantContracts/GetRoomContractInfo
        public ActionResult GetRoomContractInfo(int contractId, int roomId)
        {
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            var room = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId)?.Room;

            if (room == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                roomName = room.Name,
                contractCode = $"HD-{contract.Id:D6}",
                companyName = contract.Company?.CompanyName ?? "Cá nhân"
            }, JsonRequestBehavior.AllowGet);
        }

        private void UpdateTenantInfo(Tenant tenant, TenantDataViewModel data)
        {
            tenant.FullName = data.FullName;
            tenant.IdentityCard = data.IdentityCard;
            tenant.PhoneNumber = data.PhoneNumber;
            tenant.Gender = data.Gender;
            tenant.BirthDate = data.BirthDate;
            tenant.PermanentAddress = data.PermanentAddress;
            tenant.Ethnicity = data.Ethnicity;
            tenant.VehiclePlate = data.VehiclePlate;

            // Handle photo if provided (base64 or file path)
            if (!string.IsNullOrEmpty(data.Photo))
            {
                tenant.Photo = data.Photo;
            }
        }


        // GET: ContractTenants
        public ActionResult Index()
        {
            var contractTenants = db.ContractTenants.Include(c => c.Contract).Include(c => c.Room).Include(c => c.Tenant);
            return View(contractTenants.ToList());
        }

        // GET: ContractTenants/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // GET: ContractTenants/Create
        public ActionResult Create()
        {
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status");
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name");
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName");
            return View();
        }

        // POST: ContractTenants/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.ContractTenants.Add(contractTenant);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // POST: ContractTenants/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.Entry(contractTenant).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // POST: ContractTenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            db.ContractTenants.Remove(contractTenant);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
