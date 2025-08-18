using Microsoft.AspNet.Identity;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
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

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
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
                        ReferenceNumber = $"DEPOSIT-{id}",
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
                    // Tạo hợp đồng
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

                    // Tạo ContractRoom cho phòng chính
                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = vm.RoomId,
                        PriceAgreed = vm.PriceAgreed,
                        Notes = vm.Note
                    };
                    db.ContractRooms.Add(contractRoom);

                    // Xử lý photos theo tenant
                    var photosByTenant = GroupPhotosByTenant(TenantPhotos, vm.Tenants.Count);

                    // Tạo tenants và ContractTenant
                    for (int i = 0; i < vm.Tenants.Count; i++)
                    {
                        var tenantData = vm.Tenants[i];
                        var tenant = new Tenant
                        {
                            FullName = tenantData.FullName,
                            IdentityCard = tenantData.IdentityCard,
                            PhoneNumber = tenantData.PhoneNumber,
                            BirthDate = tenantData.BirthDate,
                            Gender = tenantData.Gender,
                            PermanentAddress = tenantData.PermanentAddress,
                            Photo = photosByTenant.ContainsKey(i)
                                ? ProcessAndSavePhotos(photosByTenant[i])
                                : null,
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
                            RoomId = vm.RoomId,  // Gán RoomId cho tenant
                            CreatedAt = DateTime.Now
                        };
                        db.ContractTenants.Add(contractTenant);
                    }

                    // Cập nhật trạng thái phòng
                    var room = db.Rooms.Find(vm.RoomId);
                    if (room != null)
                        room.IsOccupied = true;

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

        public ActionResult Edit(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            // Lấy phòng đầu tiên làm phòng chính
            var primaryRoom = contract.ContractRooms.FirstOrDefault();
            if (primaryRoom == null) return HttpNotFound();

            var vm = new ContractEditViewModel
            {
                Id = contract.Id,
                RoomId = primaryRoom.RoomId,
                MoveInDate = contract.MoveInDate,
                Months = ((contract.EndDate.Year - contract.StartDate.Year) * 12 + contract.EndDate.Month - contract.StartDate.Month),
                PriceAgreed = contract.PriceAgreed,
                ElectricityPrice = contract.ElectricityPrice,
                WaterPrice = contract.WaterPrice,
                Tenants = contract.ContractTenants.Select(ct => new TenantEditModel
                {
                    Id = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Photo = ct.Tenant.Photo,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate
                }).ToList()
            };

            ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name", vm.RoomId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel vm)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .FirstOrDefault(c => c.Id == vm.Id);

            if (contract == null)
                return HttpNotFound();

            // Update contract information
            contract.MoveInDate = vm.MoveInDate;
            contract.StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day);
            contract.EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months);
            contract.PriceAgreed = vm.PriceAgreed;
            contract.ElectricityPrice = vm.ElectricityPrice;
            contract.WaterPrice = vm.WaterPrice;

            // Update ContractRoom if room changed
            var existingContractRoom = contract.ContractRooms.FirstOrDefault();
            if (existingContractRoom != null && existingContractRoom.RoomId != vm.RoomId)
            {
                // Update old room status
                var oldRoom = db.Rooms.Find(existingContractRoom.RoomId);
                if (oldRoom != null)
                    oldRoom.IsOccupied = false;

                // Update ContractRoom
                existingContractRoom.RoomId = vm.RoomId;
                existingContractRoom.PriceAgreed = vm.PriceAgreed;

                // Update new room status
                var newRoom = db.Rooms.Find(vm.RoomId);
                if (newRoom != null)
                    newRoom.IsOccupied = true;
            }

            // Process file uploads
            var allFiles = new List<HttpPostedFileBase>();
            var filesByTenant = new Dictionary<int, List<HttpPostedFileBase>>();

            for (int i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];
                if (file != null && file.ContentLength > 0)
                {
                    allFiles.Add(file);
                }
            }

            // Distribute files among tenants
            foreach (var file in allFiles)
            {
                int tenantIndex = ExtractTenantIndexFromFileName(file.FileName);
                if (tenantIndex == -1)
                {
                    tenantIndex = DetermineTargetTenant(file, allFiles, vm.Tenants.Count);
                }

                if (!filesByTenant.ContainsKey(tenantIndex))
                {
                    filesByTenant[tenantIndex] = new List<HttpPostedFileBase>();
                }
                filesByTenant[tenantIndex].Add(file);
            }

            // Update existing tenants and add new tenants
            for (int i = 0; i < vm.Tenants.Count; i++)
            {
                var t = vm.Tenants[i];
                var tenantPhotos = new List<string>();

                // Process photos for this tenant
                if (filesByTenant.ContainsKey(i))
                {
                    foreach (var file in filesByTenant[i])
                    {
                        try
                        {
                            string savedPath = SaveTenantPhoto(file);
                            if (!string.IsNullOrEmpty(savedPath))
                            {
                                tenantPhotos.Add(savedPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving photo: {ex.Message}");
                        }
                    }
                }

                if (t.Id > 0)
                {
                    // Update existing tenant
                    var tenant = db.Tenants.Find(t.Id);
                    if (tenant != null)
                    {
                        tenant.FullName = t.FullName;
                        tenant.IdentityCard = t.IdentityCard;
                        tenant.PhoneNumber = t.PhoneNumber;
                        tenant.BirthDate = t.BirthDate;
                        tenant.Gender = t.Gender;
                        tenant.PermanentAddress = t.PermanentAddress;
                        tenant.Ethnicity = t.Ethnicity;
                        tenant.VehiclePlate = t.VehiclePlate;

                        if (tenantPhotos.Any())
                        {
                            if (!string.IsNullOrEmpty(tenant.Photo))
                            {
                                DeleteOldPhotos(tenant.Photo);
                            }
                            tenant.Photo = string.Join(";", tenantPhotos);
                        }

                        // Update ContractTenant RoomId if needed
                        var contractTenant = contract.ContractTenants.FirstOrDefault(ct => ct.TenantId == tenant.Id);
                        if (contractTenant != null && contractTenant.RoomId != vm.RoomId)
                        {
                            contractTenant.RoomId = vm.RoomId;
                        }
                    }
                }
                else
                {
                    // Add new tenant
                    var tenant = new Tenant
                    {
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Photo = tenantPhotos.Any() ? string.Join(";", tenantPhotos) : null,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate
                    };
                    db.Tenants.Add(tenant);
                    db.SaveChanges();

                    // Add ContractTenant with RoomId
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

            db.SaveChanges();
            return RedirectToAction("Details", "Rooms", new { id = vm.RoomId });
        }

        [HttpPost]
        public ActionResult ExtendContract(int contractId, int extendMonths, string note)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null || contract.Status != "Active")
                return HttpNotFound();

            if (extendMonths <= 0)
                return Json(new { success = false, message = "Số tháng gia hạn phải lớn hơn 0." });

            DateTime? oldEndDate = contract.EndDate;
            if (oldEndDate.HasValue)
                contract.EndDate = oldEndDate.Value.AddMonths(extendMonths);
            else
                contract.EndDate = DateTime.Now.AddMonths(extendMonths);

            // Save extension history
            var log = new ContractExtensionHistory
            {
                ContractId = contract.Id,
                ExtendedAt = DateTime.Now,
                OldEndDate = oldEndDate,
                NewEndDate = contract.EndDate,
                ExtendMonths = extendMonths,
                Note = note
            };
            db.ContractExtensionHistories.Add(log);

            db.SaveChanges();
            return Json(new { success = true, newEndDate = contract.EndDate.ToString("dd/MM/yyyy") });
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
                string uniqueName = $"contractscan_{contractId}_{DateTime.Now.Ticks}{ext}";
                string folder = Server.MapPath("~/Uploads/ContractScans/");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, uniqueName);
                scanFile.SaveAs(filePath);
                contract.ContractScanFilePath = "/Uploads/ContractScans/" + uniqueName;
                db.SaveChanges();
            }

            // Redirect to first room in contract
            var firstRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId ?? 0;
            return RedirectToAction("Details", "Rooms", new { id = firstRoomId });
        }

        // Helper methods
        private string ProcessAndSavePhotos(IEnumerable<HttpPostedFileBase> photos)
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

        private Dictionary<int, List<HttpPostedFileBase>> GroupPhotosByTenant(IEnumerable<HttpPostedFileBase> photos, int tenantCount)
        {
            var result = new Dictionary<int, List<HttpPostedFileBase>>();

            if (photos == null)
                return result;

            var photoList = photos.Where(p => p != null && p.ContentLength > 0).ToList();
            if (!photoList.Any())
                return result;

            // Initialize lists
            for (int i = 0; i < tenantCount; i++)
                result[i] = new List<HttpPostedFileBase>();

            // Distribute photos evenly
            int photosPerTenant = Math.Max(1, photoList.Count / tenantCount);
            int currentTenant = 0;

            foreach (var photo in photoList)
            {
                result[currentTenant].Add(photo);
                if (result[currentTenant].Count >= photosPerTenant && currentTenant < tenantCount - 1)
                    currentTenant++;
            }

            return result;
        }

        private void DeleteOldPhotos(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
                return;

            var oldPhotos = photoPath.Split(';');
            foreach (var oldPhoto in oldPhotos.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                try
                {
                    var physicalPath = Server.MapPath(oldPhoto);
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting photo {oldPhoto}: {ex.Message}");
                }
            }
        }

        private int ExtractTenantIndexFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return -1;

            var patterns = new[] { @"tenant_(\d+)_", @"tenant(\d+)_" };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                {
                    return index;
                }
            }

            return -1;
        }

        private int DetermineTargetTenant(HttpPostedFileBase file, List<HttpPostedFileBase> allFiles, int tenantCount)
        {
            int fileIndex = allFiles.IndexOf(file);
            if (fileIndex == -1) return 0;

            int filesPerTenant = Math.Max(1, allFiles.Count / tenantCount);
            int targetTenant = fileIndex / filesPerTenant;

            return Math.Min(targetTenant, tenantCount - 1);
        }

        private string SaveTenantPhoto(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                string ext = Path.GetExtension(file.FileName);
                string uniqueName = $"{fileName}_{DateTime.Now.Ticks}_{Guid.NewGuid():N}{ext}";

                string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");
                if (!Directory.Exists(serverPath))
                {
                    Directory.CreateDirectory(serverPath);
                }

                string savePath = Path.Combine(serverPath, uniqueName);
                file.SaveAs(savePath);

                return "/Uploads/TenantPhotos/" + uniqueName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving file {file.FileName}: {ex.Message}");
                return null;
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