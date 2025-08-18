using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class TenantsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Tenants - Danh sách tất cả tenants
        public ActionResult Index(string searchName = null, string searchCard = null, string filterStatus = "All")
        {
            var query = db.Tenants.AsQueryable();

            // Search filters
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(t => t.FullName.Contains(searchName));
            }
            if (!string.IsNullOrEmpty(searchCard))
            {
                query = query.Where(t => t.IdentityCard.Contains(searchCard));
            }

            var tenants = query.ToList();

            // Process tenant status based on contracts
            var tenantViewModels = new List<TenantViewModel>();

            foreach (var tenant in tenants)
            {
                var activeContract = db.ContractTenants
                    .Include(ct => ct.Contract)
                    .Include(ct => ct.Room)
                    .Where(ct => ct.TenantId == tenant.Id && ct.Contract.Status == "Active")
                    .FirstOrDefault();

                var viewModel = new TenantViewModel
                {
                    Tenant = tenant,
                    HasActiveContract = activeContract != null,
                    CurrentRoom = activeContract?.Room.Name,
                    CurrentContractId = activeContract?.ContractId,
                    MoveInDate = activeContract?.Contract.MoveInDate,
                    ContractEndDate = activeContract?.Contract.EndDate
                };

                // Apply filter
                if (filterStatus == "All" ||
                    (filterStatus == "Active" && viewModel.HasActiveContract) ||
                    (filterStatus == "Inactive" && !viewModel.HasActiveContract))
                {
                    tenantViewModels.Add(viewModel);
                }
            }

            ViewBag.SearchName = searchName;
            ViewBag.SearchCard = searchCard;
            ViewBag.FilterStatus = filterStatus;

            return View(tenantViewModels);
        }

        // GET: /Tenants/Details/5
        public ActionResult Details(int id)
        {
            var tenant = db.Tenants.Find(id);
            if (tenant == null)
            {
                return HttpNotFound();
            }

            // Lấy lịch sử hợp đồng
            var contractHistory = db.ContractTenants
                .Include(ct => ct.Contract)
                .Include(ct => ct.Room)
                .Where(ct => ct.TenantId == id)
                .OrderByDescending(ct => ct.Contract.StartDate)
                .ToList();

            ViewBag.ContractHistory = contractHistory;

            return View(tenant);
        }

        // GET: /Tenants/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Tenant tenant, HttpPostedFileBase photoFile)
        {
            if (ModelState.IsValid)
            {
                // Check duplicate IdentityCard
                var existingTenant = db.Tenants.FirstOrDefault(t => t.IdentityCard == tenant.IdentityCard);
                if (existingTenant != null)
                {
                    ModelState.AddModelError("IdentityCard", "Số CCCD này đã tồn tại trong hệ thống");
                    return View(tenant);
                }

                // Handle photo upload
                if (photoFile != null && photoFile.ContentLength > 0)
                {
                    tenant.Photo = SaveTenantPhoto(photoFile);
                }

                db.Tenants.Add(tenant);
                db.SaveChanges();

                TempData["Success"] = "Đã thêm người thuê mới thành công!";
                return RedirectToAction("Index");
            }

            return View(tenant);
        }

        // GET: /Tenants/Edit/5
        public ActionResult Edit(int id)
        {
            var tenant = db.Tenants.Find(id);
            if (tenant == null)
            {
                return HttpNotFound();
            }
            return View(tenant);
        }

        // POST: /Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tenant tenant, HttpPostedFileBase photoFile)
        {
            if (ModelState.IsValid)
            {
                // Check duplicate IdentityCard (excluding current tenant)
                var existingTenant = db.Tenants
                    .FirstOrDefault(t => t.IdentityCard == tenant.IdentityCard && t.Id != tenant.Id);

                if (existingTenant != null)
                {
                    ModelState.AddModelError("IdentityCard", "Số CCCD này đã tồn tại trong hệ thống");
                    return View(tenant);
                }

                var tenantInDb = db.Tenants.Find(tenant.Id);
                if (tenantInDb == null)
                {
                    return HttpNotFound();
                }

                // Update fields
                tenantInDb.FullName = tenant.FullName;
                tenantInDb.IdentityCard = tenant.IdentityCard;
                tenantInDb.PhoneNumber = tenant.PhoneNumber;
                tenantInDb.BirthDate = tenant.BirthDate;
                tenantInDb.Gender = tenant.Gender;
                tenantInDb.PermanentAddress = tenant.PermanentAddress;
                tenantInDb.Ethnicity = tenant.Ethnicity;
                tenantInDb.VehiclePlate = tenant.VehiclePlate;

                // Handle photo upload
                if (photoFile != null && photoFile.ContentLength > 0)
                {
                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(tenantInDb.Photo))
                    {
                        DeleteOldPhoto(tenantInDb.Photo);
                    }
                    tenantInDb.Photo = SaveTenantPhoto(photoFile);
                }

                db.SaveChanges();

                TempData["Success"] = "Đã cập nhật thông tin người thuê!";
                return RedirectToAction("Details", new { id = tenant.Id });
            }

            return View(tenant);
        }

        // GET: /Tenants/Delete/5
        public ActionResult Delete(int id)
        {
            var tenant = db.Tenants.Find(id);
            if (tenant == null)
            {
                return HttpNotFound();
            }

            // Check if tenant has active contracts
            var hasActiveContract = db.ContractTenants
                .Any(ct => ct.TenantId == id && ct.Contract.Status == "Active");

            if (hasActiveContract)
            {
                ViewBag.CannotDelete = true;
                ViewBag.Message = "Không thể xóa người thuê đang có hợp đồng active.";
            }

            return View(tenant);
        }

        // POST: /Tenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var tenant = db.Tenants.Find(id);
            if (tenant == null)
            {
                return HttpNotFound();
            }

            // Check again for active contracts
            var hasActiveContract = db.ContractTenants
                .Any(ct => ct.TenantId == id && ct.Contract.Status == "Active");

            if (hasActiveContract)
            {
                TempData["Error"] = "Không thể xóa người thuê đang có hợp đồng active!";
                return RedirectToAction("Index");
            }

            // Delete photo if exists
            if (!string.IsNullOrEmpty(tenant.Photo))
            {
                DeleteOldPhoto(tenant.Photo);
            }

            // Delete all contract tenant records
            var contractTenants = db.ContractTenants.Where(ct => ct.TenantId == id);
            db.ContractTenants.RemoveRange(contractTenants);

            // Delete tenant
            db.Tenants.Remove(tenant);
            db.SaveChanges();

            TempData["Success"] = "Đã xóa người thuê thành công!";
            return RedirectToAction("Index");
        }

        // Helper methods
        private string SaveTenantPhoto(HttpPostedFileBase photo)
        {
            if (photo == null || photo.ContentLength == 0)
                return null;

            string fileName = Path.GetFileNameWithoutExtension(photo.FileName);
            string ext = Path.GetExtension(photo.FileName);
            string uniqueName = $"tenant_{DateTime.Now.Ticks}_{Guid.NewGuid():N}{ext}";
            string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");

            if (!Directory.Exists(serverPath))
                Directory.CreateDirectory(serverPath);

            string savePath = Path.Combine(serverPath, uniqueName);
            photo.SaveAs(savePath);

            return $"/Uploads/TenantPhotos/{uniqueName}";
        }

        private void DeleteOldPhoto(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
                return;

            try
            {
                var physicalPath = Server.MapPath(photoPath);
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting photo: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ViewModels
    public class TenantViewModel
    {
        public Tenant Tenant { get; set; }
        public bool HasActiveContract { get; set; }
        public string CurrentRoom { get; set; }
        public int? CurrentContractId { get; set; }
        public DateTime? MoveInDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
    }
}