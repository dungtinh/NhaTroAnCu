using NhaTroAnCu.Helpers;
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

                };

                // Apply filter
                if (filterStatus == "All"
                    )
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

                // Sử dụng TenantPhotoHelper để handle photo upload
                if (photoFile != null && photoFile.ContentLength > 0)
                {
                    try
                    {
                        tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                    }
                    catch (InvalidOperationException ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                        return View(tenant);
                    }
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tenant tenant, HttpPostedFileBase photoFile)
        {
            if (ModelState.IsValid)
            {
                var existingTenant = db.Tenants.Find(tenant.Id);
                if (existingTenant == null)
                {
                    return HttpNotFound();
                }

                // Cập nhật thông tin
                existingTenant.FullName = tenant.FullName;
                existingTenant.PhoneNumber = tenant.PhoneNumber;
                existingTenant.BirthDate = tenant.BirthDate;
                existingTenant.Gender = tenant.Gender;
                existingTenant.PermanentAddress = tenant.PermanentAddress;
                existingTenant.Ethnicity = tenant.Ethnicity;
                existingTenant.VehiclePlate = tenant.VehiclePlate;

                // Xử lý upload ảnh mới
                if (photoFile != null && photoFile.ContentLength > 0)
                {
                    try
                    {
                        // Xóa ảnh cũ nếu có
                        if (!string.IsNullOrEmpty(existingTenant.Photo))
                        {
                            TenantPhotoHelper.DeleteTenantPhoto(existingTenant.Photo);
                        }

                        // Lưu ảnh mới
                        existingTenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, existingTenant.IdentityCard);
                    }
                    catch (InvalidOperationException ex)
                    {
                        ModelState.AddModelError("", ex.Message);
                        return View(tenant);
                    }
                }

                db.Entry(existingTenant).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Success"] = "Đã cập nhật thông tin người thuê!";
                return RedirectToAction("Details", new { id = tenant.Id });
            }

            return View(tenant);
        }

        // POST: /Tenants/DeletePhoto/5
        [HttpPost]
        public ActionResult DeletePhoto(int id)
        {
            var tenant = db.Tenants.Find(id);
            if (tenant != null && !string.IsNullOrEmpty(tenant.Photo))
            {
                // Xóa file ảnh
                TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);

                // Cập nhật database
                tenant.Photo = null;
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa ảnh thành công" });
            }

            return Json(new { success = false, message = "Không tìm thấy ảnh" });
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
}