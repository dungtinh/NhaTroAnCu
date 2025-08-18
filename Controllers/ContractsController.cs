using Microsoft.AspNet.Identity;
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
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Contracts/Create?roomId=5
        public ActionResult Create(int roomId)
        {
            var room = db.Rooms.Find(roomId);
            if (room == null) return HttpNotFound();

            ViewBag.RoomId = roomId;
            ViewBag.RoomName = room.Name;
            ViewBag.AvailableRooms = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");

            return View(new ContractCreateViewModel()
            {
                RoomId = roomId,
                MoveInDate = DateTime.Now,
                PriceAgreed = room.DefaultPrice,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel vm, IEnumerable<HttpPostedFileBase> TenantPhotos)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RoomId = vm.RoomId;
                ViewBag.AvailableRooms = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
                return View(vm);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Tạo hợp đồng mới
                    var contract = new Contract
                    {
                        MoveInDate = vm.MoveInDate,
                        StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day),
                        EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months),
                        PriceAgreed = vm.PriceAgreed,
                        Note = vm.Note,
                        Status = "Active",
                        ElectricityPrice = vm.ElectricityPrice,
                        WaterPrice = vm.WaterPrice
                    };

                    db.Contracts.Add(contract);
                    db.SaveChanges();

                    // Tạo ContractRoom cho phòng
                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = vm.RoomId,
                        PriceAgreed = vm.PriceAgreed,
                        Notes = vm.Note
                    };
                    db.ContractRooms.Add(contractRoom);

                    // Xử lý photos theo tenant
                    var photosByTenant = GroupPhotosByTenant(TenantPhotos, vm.Tenants?.Count ?? 0);

                    // Tạo tenants và ContractTenant
                    if (vm.Tenants != null && vm.Tenants.Any())
                    {
                        for (int i = 0; i < vm.Tenants.Count; i++)
                        {
                            var tenantData = vm.Tenants[i];

                            // Tạo tenant mới
                            var tenant = new Tenant
                            {
                                FullName = tenantData.FullName,
                                IdentityCard = tenantData.IdentityCard,
                                PhoneNumber = tenantData.PhoneNumber,
                                BirthDate = tenantData.BirthDate,
                                Gender = tenantData.Gender,
                                PermanentAddress = tenantData.PermanentAddress,
                                Photo = photosByTenant.ContainsKey(i) ?
                                    ProcessAndSavePhotos(photosByTenant[i]) : null,
                                Ethnicity = tenantData.Ethnicity,
                                VehiclePlate = tenantData.VehiclePlate
                            };

                            db.Tenants.Add(tenant);
                            db.SaveChanges();

                            // Tạo ContractTenant với RoomId
                            var contractTenant = new ContractTenant
                            {
                                ContractId = contract.Id,
                                TenantId = tenant.Id,
                                RoomId = vm.RoomId,
                                CreatedAt = DateTime.Now
                            };
                            db.ContractTenants.Add(contractTenant);
                        }
                    }

                    // Cập nhật trạng thái phòng
                    var room = db.Rooms.Find(vm.RoomId);
                    if (room != null)
                        room.IsOccupied = true;

                    // Ghi nhận thu tiền cọc nếu có
                    if (vm.DepositAmount > 0)
                    {
                        var incomeCategory = db.IncomeExpenseCategories
                            .FirstOrDefault(c => c.Name == "Thu tiền cọc" && c.IsSystem);

                        if (incomeCategory != null)
                        {
                            var incomeExpense = new IncomeExpense
                            {
                                CategoryId = incomeCategory.Id,
                                ContractId = contract.Id,
                                Amount = vm.DepositAmount,
                                TransactionDate = DateTime.Now.Date,
                                Description = $"Thu tiền cọc phòng {room.Name} - HĐ #{contract.Id}",
                                ReferenceNumber = $"DEPOSIT-{contract.Id}",
                                CreatedBy = User.Identity.GetUserId(),
                                CreatedAt = DateTime.Now
                            };
                            db.IncomeExpenses.Add(incomeExpense);
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return RedirectToAction("Details", "Rooms", new { id = vm.RoomId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    ViewBag.RoomId = vm.RoomId;
                    ViewBag.AvailableRooms = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
                    return View(vm);
                }
            }
        }

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            ViewBag.Contract = contract;
            return View();
        }

        [HttpPost]
        public ActionResult EndConfirm(int id, decimal? refundAmount, string refundNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            contract.Status = "Ended";
            contract.EndDate = DateTime.Now;

            // Cập nhật tất cả các phòng trong hợp đồng
            foreach (var contractRoom in contract.ContractRooms)
            {
                contractRoom.Room.IsOccupied = false;
            }

            // Ghi nhận trả cọc nếu có
            if (refundAmount.HasValue && refundAmount.Value > 0)
            {
                var expenseCategory = db.IncomeExpenseCategories
                    .FirstOrDefault(c => c.Name == "Trả tiền cọc" && c.IsSystem);

                if (expenseCategory != null)
                {
                    var incomeExpense = new IncomeExpense
                    {
                        CategoryId = expenseCategory.Id,
                        ContractId = id,
                        Amount = refundAmount.Value,
                        TransactionDate = DateTime.Now.Date,
                        Description = refundNote ?? $"Trả tiền cọc khi kết thúc hợp đồng",
                        ReferenceNumber = $"REFUND-{id}",
                        CreatedBy = User.Identity.GetUserId(),
                        CreatedAt = DateTime.Now
                    };

                    db.IncomeExpenses.Add(incomeExpense);
                }
            }

            db.SaveChanges();

            // Redirect về phòng đầu tiên trong hợp đồng
            var firstRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId ?? 0;
            return RedirectToAction("Details", "Rooms", new { id = firstRoomId });
        }

        [HttpPost]
        public ActionResult ExtendContract(int contractId, int extendMonths, string note)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null || contract.Status != "Active")
                return HttpNotFound();

            if (extendMonths <= 0)
                return Json(new { success = false, message = "Số tháng gia hạn phải lớn hơn 0." });

            DateTime oldEndDate = contract.EndDate;
            contract.EndDate = oldEndDate.AddMonths(extendMonths);

            // Lưu lịch sử gia hạn
            var extensionHistory = new ContractExtensionHistory
            {
                ContractId = contract.Id,
                ExtendedAt = DateTime.Now,
                OldEndDate = oldEndDate,
                NewEndDate = contract.EndDate,
                ExtendMonths = extendMonths,
                Note = note
            };
            db.ContractExtensionHistories.Add(extensionHistory);

            db.SaveChanges();

            return Json(new
            {
                success = true,
                newEndDate = contract.EndDate.ToString("dd/MM/yyyy")
            });
        }

        [HttpPost]
        public ActionResult UploadContractScan(int contractId, HttpPostedFileBase scanFile)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null) return HttpNotFound();

            if (scanFile != null && scanFile.ContentLength > 0)
            {
                string fileName = Path.GetFileNameWithoutExtension(scanFile.FileName);
                string ext = Path.GetExtension(scanFile.FileName);
                string uniqueName = $"contract_{contractId}_{DateTime.Now.Ticks}{ext}";
                string folder = Server.MapPath("~/Uploads/ContractScans/");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, uniqueName);
                scanFile.SaveAs(filePath);

                contract.ContractScanFilePath = "/Uploads/ContractScans/" + uniqueName;
                db.SaveChanges();
            }

            // Redirect về phòng đầu tiên trong hợp đồng
            var firstRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId ?? 0;
            return RedirectToAction("Details", "Rooms", new { id = firstRoomId });
        }

        // Helper methods
        private string ProcessAndSavePhotos(List<HttpPostedFileBase> photos)
        {
            if (photos == null || !photos.Any(p => p != null && p.ContentLength > 0))
                return null;

            var photoPaths = new List<string>();
            string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");

            if (!Directory.Exists(serverPath))
                Directory.CreateDirectory(serverPath);

            foreach (var photo in photos.Where(p => p != null && p.ContentLength > 0))
            {
                string fileName = Path.GetFileNameWithoutExtension(photo.FileName);
                string ext = Path.GetExtension(photo.FileName);
                string uniqueName = $"{fileName}_{Guid.NewGuid():N}{ext}";
                string savePath = Path.Combine(serverPath, uniqueName);

                photo.SaveAs(savePath);
                photoPaths.Add($"/Uploads/TenantPhotos/{uniqueName}");
            }

            return string.Join(";", photoPaths);
        }

        private Dictionary<int, List<HttpPostedFileBase>> GroupPhotosByTenant(
            IEnumerable<HttpPostedFileBase> photos, int tenantCount)
        {
            var result = new Dictionary<int, List<HttpPostedFileBase>>();

            if (photos == null || tenantCount == 0)
                return result;

            var photoList = photos.Where(p => p != null && p.ContentLength > 0).ToList();
            if (!photoList.Any())
                return result;

            // Initialize lists for each tenant
            for (int i = 0; i < tenantCount; i++)
                result[i] = new List<HttpPostedFileBase>();

            // Distribute photos evenly among tenants
            for (int i = 0; i < photoList.Count; i++)
            {
                int tenantIndex = i % tenantCount;
                result[tenantIndex].Add(photoList[i]);
            }

            return result;
        }

        // GET: /Contracts/Details/5
        public ActionResult Details(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            return View(contract);
        }

        // GET: /Contracts/
        public ActionResult Index()
        {
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(contracts);
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