using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class TenantContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: TenantContracts - Danh sách tất cả tenant đang có hợp đồng
        public ActionResult Index(
            string searchName = null,
            string searchCard = null,
            string searchRoom = null,
            string filterStatus = "Active")
        {
            var query = db.ContractTenants
                .Include(ct => ct.Contract)
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .AsQueryable();

            // Filter by contract status
            if (!string.IsNullOrEmpty(filterStatus))
            {
                query = query.Where(ct => ct.Contract.Status == filterStatus);
            }

            // Search filters
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(ct => ct.Tenant.FullName.Contains(searchName));
            }
            if (!string.IsNullOrEmpty(searchCard))
            {
                query = query.Where(ct => ct.Tenant.IdentityCard.Contains(searchCard));
            }
            if (!string.IsNullOrEmpty(searchRoom))
            {
                query = query.Where(ct => ct.Room.Name.Contains(searchRoom));
            }

            var result = query
                .OrderByDescending(ct => ct.Contract.StartDate)
                .Select(ct => new TenantContractViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    TenantName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    RoomId = ct.RoomId,
                    RoomName = ct.Room.Name,
                    ContractId = ct.ContractId,
                    ContractStatus = ct.Contract.Status,
                    StartDate = ct.Contract.StartDate,
                    EndDate = ct.Contract.EndDate,
                    MoveInDate = ct.Contract.MoveInDate,
                    Photo = ct.Tenant.Photo
                })
                .ToList();

            ViewBag.SearchName = searchName;
            ViewBag.SearchCard = searchCard;
            ViewBag.SearchRoom = searchRoom;
            ViewBag.FilterStatus = filterStatus;

            return View(result);
        }

        // GET: TenantContracts/AddTenant/5 - Thêm tenant vào hợp đồng có sẵn
        public ActionResult AddTenant(int contractId)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null || contract.Status != "Active")
            {
                return HttpNotFound();
            }

            var model = new AddTenantToContractViewModel
            {
                ContractId = contractId,
                Contract = contract,
                AvailableRooms = contract.ContractRooms.Select(cr => new SelectListItem
                {
                    Value = cr.RoomId.ToString(),
                    Text = cr.Room.Name
                }).ToList()
            };

            return View(model);
        }

        // POST: TenantContracts/AddTenant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTenant(AddTenantToContractViewModel model, HttpPostedFileBase photoFile)
        {
            if (!ModelState.IsValid)
            {
                // Reload contract data
                model.Contract = db.Contracts
                    .Include(c => c.ContractRooms.Select(cr => cr.Room))
                    .FirstOrDefault(c => c.Id == model.ContractId);

                model.AvailableRooms = model.Contract.ContractRooms.Select(cr => new SelectListItem
                {
                    Value = cr.RoomId.ToString(),
                    Text = cr.Room.Name
                }).ToList();

                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Tạo tenant mới
                    var tenant = new Tenant
                    {
                        FullName = model.FullName,
                        IdentityCard = model.IdentityCard,
                        PhoneNumber = model.PhoneNumber,
                        BirthDate = model.BirthDate,
                        Gender = model.Gender,
                        PermanentAddress = model.PermanentAddress,
                        Ethnicity = model.Ethnicity,
                        VehiclePlate = model.VehiclePlate
                    };

                    // Xử lý upload ảnh
                    if (photoFile != null && photoFile.ContentLength > 0)
                    {
                        tenant.Photo = SaveTenantPhoto(photoFile);
                    }

                    db.Tenants.Add(tenant);
                    db.SaveChanges();

                    // Tạo ContractTenant
                    var contractTenant = new ContractTenant
                    {
                        ContractId = model.ContractId,
                        TenantId = tenant.Id,
                        RoomId = model.RoomId,
                        CreatedAt = DateTime.Now
                    };

                    db.ContractTenants.Add(contractTenant);
                    db.SaveChanges();

                    transaction.Commit();

                    TempData["Success"] = "Đã thêm người thuê vào hợp đồng thành công!";
                    return RedirectToAction("Details", "Contracts", new { id = model.ContractId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);

                    // Reload data for view
                    model.Contract = db.Contracts
                        .Include(c => c.ContractRooms.Select(cr => cr.Room))
                        .FirstOrDefault(c => c.Id == model.ContractId);

                    model.AvailableRooms = model.Contract.ContractRooms.Select(cr => new SelectListItem
                    {
                        Value = cr.RoomId.ToString(),
                        Text = cr.Room.Name
                    }).ToList();

                    return View(model);
                }
            }
        }

        // GET: TenantContracts/RemoveTenant/5
        public ActionResult RemoveTenant(int id)
        {
            var contractTenant = db.ContractTenants
                .Include(ct => ct.Contract)
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .FirstOrDefault(ct => ct.Id == id);

            if (contractTenant == null)
            {
                return HttpNotFound();
            }

            return View(contractTenant);
        }

        // POST: TenantContracts/RemoveTenantConfirm/5
        [HttpPost, ActionName("RemoveTenant")]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveTenantConfirm(int id)
        {
            var contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }

            var contractId = contractTenant.ContractId;

            // Check if this is the last tenant in the contract
            var remainingTenants = db.ContractTenants
                .Count(ct => ct.ContractId == contractId && ct.Id != id);

            if (remainingTenants == 0)
            {
                TempData["Error"] = "Không thể xóa người thuê cuối cùng trong hợp đồng. Vui lòng kết thúc hợp đồng nếu cần.";
                return RedirectToAction("Details", "Contracts", new { id = contractId });
            }

            db.ContractTenants.Remove(contractTenant);
            db.SaveChanges();

            TempData["Success"] = "Đã xóa người thuê khỏi hợp đồng!";
            return RedirectToAction("Details", "Contracts", new { id = contractId });
        }

        // GET: TenantContracts/ChangeTenantRoom/5
        public ActionResult ChangeTenantRoom(int id)
        {
            var contractTenant = db.ContractTenants
                .Include(ct => ct.Contract.ContractRooms.Select(cr => cr.Room))
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .FirstOrDefault(ct => ct.Id == id);

            if (contractTenant == null)
            {
                return HttpNotFound();
            }

            var model = new ChangeTenantRoomViewModel
            {
                ContractTenantId = id,
                TenantName = contractTenant.Tenant.FullName,
                CurrentRoomId = contractTenant.RoomId,
                CurrentRoomName = contractTenant.Room.Name,
                ContractId = contractTenant.ContractId,
                AvailableRooms = contractTenant.Contract.ContractRooms
                    .Where(cr => cr.RoomId != contractTenant.RoomId)
                    .Select(cr => new SelectListItem
                    {
                        Value = cr.RoomId.ToString(),
                        Text = cr.Room.Name
                    }).ToList()
            };

            return View(model);
        }

        // POST: TenantContracts/ChangeTenantRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeTenantRoom(ChangeTenantRoomViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var contractTenant = db.ContractTenants.Find(model.ContractTenantId);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }

            contractTenant.RoomId = model.NewRoomId;
            db.SaveChanges();

            TempData["Success"] = "Đã chuyển phòng cho người thuê thành công!";
            return RedirectToAction("Details", "Contracts", new { id = contractTenant.ContractId });
        }

        // Helper method to save tenant photo
        private string SaveTenantPhoto(HttpPostedFileBase photo)
        {
            if (photo == null || photo.ContentLength == 0)
                return null;

            string fileName = Path.GetFileNameWithoutExtension(photo.FileName);
            string ext = Path.GetExtension(photo.FileName);
            string uniqueName = $"{fileName}_{Guid.NewGuid():N}{ext}";
            string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");

            if (!Directory.Exists(serverPath))
                Directory.CreateDirectory(serverPath);

            string savePath = Path.Combine(serverPath, uniqueName);
            photo.SaveAs(savePath);

            return $"/Uploads/TenantPhotos/{uniqueName}";
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
    public class TenantContractViewModel
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int ContractId { get; set; }
        public string ContractStatus { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime MoveInDate { get; set; }
        public string Photo { get; set; }
    }

    public class AddTenantToContractViewModel
    {
        public int ContractId { get; set; }
        public Contract Contract { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số CCCD")]
        public string IdentityCard { get; set; }

        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string PermanentAddress { get; set; }
        public string Ethnicity { get; set; }
        public string VehiclePlate { get; set; }

        public List<SelectListItem> AvailableRooms { get; set; }
    }

    public class ChangeTenantRoomViewModel
    {
        public int ContractTenantId { get; set; }
        public string TenantName { get; set; }
        public int CurrentRoomId { get; set; }
        public string CurrentRoomName { get; set; }
        public int ContractId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng mới")]
        public int NewRoomId { get; set; }

        public List<SelectListItem> AvailableRooms { get; set; }
    }
}