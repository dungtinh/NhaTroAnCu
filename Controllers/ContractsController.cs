using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: Contracts/Extend/5
        [HttpGet]
        public ActionResult Extend(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra contract đã kết thúc chưa
            if (contract.Status == "Ended")
            {
                TempData["Error"] = "Không thể gia hạn hợp đồng đã kết thúc.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.ContractId = id;
            ViewBag.CurrentEndDate = contract.EndDate;

            return View(contract);
        }

        // POST: Contracts/Extend/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Extend(int id, DateTime newEndDate, string extendNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Validation
            if (newEndDate <= contract.EndDate)
            {
                TempData["Error"] = "Ngày gia hạn mới phải sau ngày kết thúc hiện tại.";
                return View(contract);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lưu lịch sử gia hạn
                    var extensionHistory = new ContractExtensionHistory
                    {
                        ContractId = contract.Id,
                        OldEndDate = contract.EndDate,
                        NewEndDate = newEndDate,
                        Note = extendNote,
                        ExtendedAt = DateTime.Now,
                    };

                    db.ContractExtensionHistories.Add(extensionHistory);

                    // Cập nhật ngày kết thúc mới cho hợp đồng
                    contract.EndDate = newEndDate;

                    // Thêm ghi chú vào Note của contract
                    var noteEntry = $"\n[{DateTime.Now:dd/MM/yyyy HH:mm}] Gia hạn từ {contract.EndDate:dd/MM/yyyy} đến {newEndDate:dd/MM/yyyy}";
                    if (!string.IsNullOrEmpty(extendNote))
                    {
                        noteEntry += $" - {extendNote}";
                    }
                    contract.Note = (contract.Note ?? "") + noteEntry;

                    // Lưu thay đổi
                    db.SaveChanges();

                    // Commit transaction
                    transaction.Commit();

                    TempData["Success"] = $"Đã gia hạn hợp đồng đến ngày {newEndDate:dd/MM/yyyy} thành công.";

                    return RedirectToAction("Details", "Contracts", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra khi gia hạn hợp đồng. Vui lòng thử lại.";
                    return View(contract);
                }
            }
        }

        // Action hỗ trợ: Lấy lịch sử gia hạn
        [HttpGet]
        public JsonResult GetExtensionHistory(int id)
        {
            var history = db.ContractExtensionHistories
                .Where(h => h.ContractId == id)
                .OrderByDescending(h => h.ExtendedAt)
                .ToList();

            return Json(history, JsonRequestBehavior.AllowGet);
        }


        // GET: Contracts/End/5
        [HttpGet]
        public ActionResult End(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.IncomeExpenses.Select(ci => ci.IncomeExpenseCategory))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra contract đã kết thúc chưa
            if (contract.Status == "Ended")
            {
                TempData["Error"] = "Hợp đồng này đã được kết thúc trước đó.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.ContractId = id;
            return View(contract);
        }

        // POST: Contracts/End/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult End(int id, DateTime? endDate, decimal? refundAmount, string refundNote)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Cập nhật trạng thái hợp đồng
                    contract.Status = "Ended";
                    contract.EndDate = endDate ?? DateTime.Now;
                    // Cập nhật trạng thái tất cả các phòng trong hợp đồng
                    foreach (var contractRoom in contract.ContractRooms)
                    {
                        contractRoom.Room.IsOccupied = false;
                    }

                    // Ghi nhận trả cọc nếu có
                    if (refundAmount.HasValue && refundAmount.Value > 0)
                    {
                        // Tìm hoặc tạo category "Trả tiền cọc"
                        var expenseCategory = db.IncomeExpenseCategories
                            .FirstOrDefault(c => c.Name == "Trả tiền cọc" && c.IsSystem);

                        if (expenseCategory == null)
                        {
                            expenseCategory = new IncomeExpenseCategory
                            {
                                Name = "Trả tiền cọc",
                                Type = "Expense",
                                IsSystem = true,
                                IsActive = true,
                                CreatedAt = DateTime.Now,
                            };
                            db.IncomeExpenseCategories.Add(expenseCategory);
                            db.SaveChanges();
                        }

                        // Tạo phiếu chi trả cọc
                        var incomeExpense = new IncomeExpense
                        {
                            CategoryId = expenseCategory.Id,
                            ContractId = id,
                            Amount = refundAmount.Value,
                            TransactionDate = endDate?.Date ?? DateTime.Now.Date,
                            Description = !string.IsNullOrEmpty(refundNote)
                                ? refundNote
                                : $"Trả tiền cọc khi kết thúc hợp đồng {contract.Id}",
                            ReferenceNumber = $"REFUND-{contract.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                            CreatedBy = User.Identity.GetUserId(),
                            CreatedAt = DateTime.Now
                        };

                        db.IncomeExpenses.Add(incomeExpense);
                    }

                    // Lưu tất cả thay đổi
                    db.SaveChanges();

                    // Commit transaction
                    transaction.Commit();

                    TempData["Success"] = $"Đã kết thúc hợp đồng {contract.Id} thành công.";

                    // Redirect về phòng đầu tiên trong hợp đồng hoặc danh sách hợp đồng

                    return RedirectToAction("Details", "Contracts", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    // Log error nếu có hệ thống logging
                    // Logger.Error($"Error ending contract {id}: {ex.Message}", ex);

                    TempData["Error"] = "Có lỗi xảy ra khi kết thúc hợp đồng. Vui lòng thử lại.";
                    return RedirectToAction("Details", new { id = id });
                }
            }
        }

        // GET: Contracts
        public ActionResult Index()
        {
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.Company)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(contracts);
        }


        // GET: Contracts/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            return View(contract);
        }

        // GET: Contracts/Create
        public ActionResult Create(int? roomId)
        {
            var model = new ContractCreateViewModel
            {
                MoveInDate = DateTime.Now,
                StartDate = DateTime.Now,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
                Months = 12,
                ContractType = roomId.HasValue ? "Individual" : null,
                SelectedRooms = new List<RoomSelectionModel>()
            };

            if (roomId.HasValue)
            {
                var room = db.Rooms.Find(roomId.Value);
                if (room != null)
                {
                    model.SingleRoomId = roomId;
                    model.SingleRoomPrice = room.DefaultPrice;
                }
            }

            LoadCreateViewData();
            return View(model);
        }

        // POST: Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadCreateViewData();
                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = new Contract
                    {
                        ContractType = model.ContractType,
                        StartDate = model.StartDate,
                        EndDate = model.StartDate.AddMonths(model.Months),
                        MoveInDate = model.MoveInDate,
                        ElectricityPrice = model.ElectricityPrice,
                        WaterPrice = model.WaterPrice,
                        Note = model.Note,
                        Status = "Active"
                    };

                    if (model.ContractType == "Company")
                    {
                        contract = CreateCompanyContract(model, contract);
                    }
                    else
                    {
                        contract = CreateIndividualContract(model, contract);
                    }

                    transaction.Commit();
                    TempData["Success"] = "Đã tạo hợp đồng thành công!";
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    LoadCreateViewData();
                    return View(model);
                }
            }
        }

        // GET: Contracts/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null)
            {
                return HttpNotFound();
            }

            var model = new ContractEditViewModel
            {
                Id = contract.Id,
                ContractType = contract.ContractType,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                MoveInDate = contract.MoveInDate,
                ElectricityPrice = contract.ElectricityPrice,
                WaterPrice = contract.WaterPrice,
                Note = contract.Note,
                Status = contract.Status,
                ContractScanFilePath = contract.ContractScanFilePath,
                CompanyId = contract.CompanyId,
                CompanyName = contract.Company?.CompanyName
            };

            if (contract.ContractType == "Company")
            {
                model.SelectedRooms = contract.ContractRooms.Select(cr => new RoomSelectionModel
                {
                    RoomId = cr.RoomId,
                    RoomName = cr.Room.Name,
                    DefaultPrice = cr.Room.DefaultPrice,
                    AgreedPrice = cr.PriceAgreed,
                    IsSelected = true,
                    Notes = cr.Notes
                }).ToList();

                // Load tenant counts cho mỗi phòng
                var roomTenantCounts = db.ContractTenants
                    .Where(ct => ct.ContractId == contract.Id)
                    .GroupBy(ct => ct.RoomId)
                    .Select(g => new { RoomId = g.Key, Count = g.Count() })
                    .ToDictionary(x => x.RoomId, x => x.Count);

                ViewBag.RoomTenantCounts = roomTenantCounts;

                if (contract.Company != null)
                {
                    ViewBag.CompanyTaxCode = contract.Company.TaxCode;
                }
            }
            else
            {
                var contractRoom = contract.ContractRooms.FirstOrDefault();
                if (contractRoom != null)
                {
                    model.RoomId = contractRoom.RoomId;
                    model.PriceAgreed = contractRoom.PriceAgreed;
                }

                model.Tenants = contract.ContractTenants.Select(ct => new TenantViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,
                    CreatedAt = ct.CreatedAt
                }).ToList();
            }

            LoadEditViewData(model.Id);
            return View(model);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel model, HttpPostedFileBase ContractScanFile)
        {
            if (!ModelState.IsValid)
            {
                LoadEditViewData(model.Id);
                return View(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .Include(c => c.ContractTenants)
                        .FirstOrDefault(c => c.Id == model.Id);

                    if (contract == null)
                    {
                        return HttpNotFound();
                    }

                    // Update basic info
                    contract.StartDate = model.StartDate;
                    contract.EndDate = model.EndDate;
                    contract.MoveInDate = model.MoveInDate;
                    contract.ElectricityPrice = model.ElectricityPrice;
                    contract.WaterPrice = model.WaterPrice;
                    contract.Note = model.Note;
                    contract.Status = model.Status;

                    // Process contract scan file
                    if (ContractScanFile != null && ContractScanFile.ContentLength > 0)
                    {
                        ProcessContractScanFile(contract, ContractScanFile);
                    }

                    // Process based on type
                    if (contract.ContractType == "Individual")
                    {
                        UpdateIndividualContract(contract, model);
                    }
                    else if (contract.ContractType == "Company")
                    {
                        UpdateCompanyContract(contract, model);
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["Success"] = "Đã cập nhật hợp đồng thành công!";
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                    LoadEditViewData(model.Id);
                    return View(model);
                }
            }
        }

        // DELETE: Contracts/Delete/5
        [HttpPost]
        public ActionResult Delete(int id)
        {
            try
            {
                var contract = db.Contracts
                    .Include(c => c.ContractRooms)
                    .Include(c => c.ContractTenants)
                    .FirstOrDefault(c => c.Id == id);

                if (contract == null)
                {
                    return Json(new { success = false, message = "Hợp đồng không tồn tại" });
                }

                // Update room status
                foreach (var cr in contract.ContractRooms)
                {
                    var room = db.Rooms.Find(cr.RoomId);
                    if (room != null)
                    {
                        room.IsOccupied = false;
                    }
                }

                // Remove contract tenants
                db.ContractTenants.RemoveRange(contract.ContractTenants);

                // Remove contract rooms
                db.ContractRooms.RemoveRange(contract.ContractRooms);

                // Remove contract
                db.Contracts.Remove(contract);

                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa hợp đồng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

        private Contract CreateIndividualContract(ContractCreateViewModel model, Contract contract)
        {
            if (!model.SingleRoomId.HasValue)
            {
                throw new InvalidOperationException("Vui lòng chọn phòng");
            }

            var room = db.Rooms.Find(model.SingleRoomId.Value);
            if (room == null || room.IsOccupied)
            {
                throw new InvalidOperationException("Phòng không tồn tại hoặc đã được thuê");
            }

            contract.PriceAgreed = model.SingleRoomPrice ?? room.DefaultPrice;
            db.Contracts.Add(contract);
            db.SaveChanges();

            var contractRoom = new ContractRoom
            {
                ContractId = contract.Id,
                RoomId = room.Id,
                PriceAgreed = contract.PriceAgreed,
                Notes = model.Note
            };
            db.ContractRooms.Add(contractRoom);
            db.SaveChanges();

            room.IsOccupied = true;
            db.SaveChanges();

            return contract;
        }

        private Contract CreateCompanyContract(ContractCreateViewModel model, Contract contract)
        {
            // Process company
            if (model.Company != null && !string.IsNullOrEmpty(model.Company.TaxCode))
            {
                var existingCompany = db.Companies
                    .FirstOrDefault(c => c.TaxCode == model.Company.TaxCode);

                Company company;
                if (existingCompany != null)
                {
                    company = existingCompany;
                    UpdateCompanyInfo(company, model.Company);
                }
                else
                {
                    company = CreateNewCompany(model.Company);
                    db.Companies.Add(company);
                }

                db.SaveChanges();
                contract.CompanyId = company.Id;
            }

            db.Contracts.Add(contract);
            db.SaveChanges();

            // Process rooms
            decimal totalPrice = 0;
            if (model.SelectedRooms != null)
            {
                foreach (var selectedRoom in model.SelectedRooms)
                {
                    if (!selectedRoom.IsSelected) continue;

                    var room = db.Rooms.Find(selectedRoom.RoomId);
                    if (room != null)
                    {
                        var contractRoom = new ContractRoom
                        {
                            ContractId = contract.Id,
                            RoomId = selectedRoom.RoomId,
                            PriceAgreed = selectedRoom.AgreedPrice,
                            Notes = selectedRoom.Notes
                        };
                        db.ContractRooms.Add(contractRoom);
                        room.IsOccupied = true;
                        totalPrice += selectedRoom.AgreedPrice;
                    }
                }
            }

            contract.PriceAgreed = totalPrice;
            db.SaveChanges();

            return contract;
        }

        private void UpdateIndividualContract(Contract contract, ContractEditViewModel model)
        {
            if (model.RoomId.HasValue && model.PriceAgreed.HasValue)
            {
                var contractRoom = contract.ContractRooms.FirstOrDefault();
                if (contractRoom != null)
                {
                    if (contractRoom.RoomId != model.RoomId.Value)
                    {
                        var oldRoom = db.Rooms.Find(contractRoom.RoomId);
                        if (oldRoom != null) oldRoom.IsOccupied = false;

                        var contractTenants = db.ContractTenants
                            .Where(ct => ct.ContractId == contract.Id)
                            .ToList();

                        foreach (var ct in contractTenants)
                        {
                            ct.RoomId = model.RoomId.Value;
                        }

                        contractRoom.RoomId = model.RoomId.Value;

                        var newRoom = db.Rooms.Find(model.RoomId.Value);
                        if (newRoom != null && contract.Status == "Active")
                        {
                            newRoom.IsOccupied = true;
                        }
                    }

                    contractRoom.PriceAgreed = model.PriceAgreed.Value;
                }

                contract.PriceAgreed = model.PriceAgreed.Value;
            }

            // Process tenants using helper
            TenantContractHelper.ProcessIndividualContractTenants(
                db, contract, model.Tenants, Request, true
            );
        }

        private void UpdateCompanyContract(Contract contract, ContractEditViewModel model)
        {
            if (model.SelectedRooms != null)
            {
                var currentRoomIds = contract.ContractRooms.Select(cr => cr.RoomId).ToList();
                var newRoomIds = model.SelectedRooms.Select(r => r.RoomId).ToList();

                // Remove rooms
                var roomsToRemove = contract.ContractRooms
                    .Where(cr => !newRoomIds.Contains(cr.RoomId))
                    .ToList();

                foreach (var contractRoom in roomsToRemove)
                {
                    RemoveRoomFromContract(contract.Id, contractRoom.RoomId);
                }

                // Add new rooms
                var roomsToAdd = newRoomIds
                    .Where(id => !currentRoomIds.Contains(id))
                    .ToList();

                foreach (var roomId in roomsToAdd)
                {
                    AddRoomToContract(contract.Id, roomId, model.SelectedRooms);
                }

                // Update existing rooms
                foreach (var roomModel in model.SelectedRooms)
                {
                    var contractRoom = contract.ContractRooms
                        .FirstOrDefault(cr => cr.RoomId == roomModel.RoomId);

                    if (contractRoom != null)
                    {
                        contractRoom.PriceAgreed = roomModel.AgreedPrice;
                        contractRoom.Notes = roomModel.Notes;
                    }
                }

                // Update total price
                contract.PriceAgreed = contract.ContractRooms.Sum(cr => cr.PriceAgreed);
            }
        }

        private Company CreateNewCompany(Company model)
        {
            return new Company
            {
                CompanyName = model.CompanyName,
                TaxCode = model.TaxCode,
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                Representative = model.Representative,
                RepresentativePhone = model.RepresentativePhone,
                RepresentativeEmail = model.RepresentativeEmail,
                BankAccount = model.BankAccount,
                BankName = model.BankName
            };
        }

        private void UpdateCompanyInfo(Company company, Company model)
        {
            company.CompanyName = model.CompanyName;
            company.Address = model.Address;
            company.Phone = model.Phone;
            company.Email = model.Email;
            company.Representative = model.Representative;
            company.RepresentativePhone = model.RepresentativePhone;
            company.RepresentativeEmail = model.RepresentativeEmail;
            company.BankAccount = model.BankAccount;
            company.BankName = model.BankName;
        }

        private void ProcessContractScanFile(Contract contract, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return;

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("File phải là PDF, JPG hoặc PNG");
            }

            if (!string.IsNullOrEmpty(contract.ContractScanFilePath))
            {
                var oldPath = Server.MapPath(contract.ContractScanFilePath);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            var fileName = $"Contract_{contract.Id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var uploadDir = Server.MapPath("~/Uploads/ContractScans");

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var filePath = Path.Combine(uploadDir, fileName);
            file.SaveAs(filePath);
            contract.ContractScanFilePath = $"/Uploads/ContractScans/{fileName}";
        }

        private void AddRoomToContract(int contractId, int roomId, List<RoomSelectionModel> roomModels)
        {
            var room = db.Rooms.Find(roomId);
            if (room == null) return;

            var isOccupied = db.ContractRooms
                .Any(cr => cr.RoomId == roomId &&
                          cr.Contract.Status == "Active" &&
                          cr.ContractId != contractId);

            if (isOccupied) return;

            var roomModel = roomModels.FirstOrDefault(r => r.RoomId == roomId);
            var agreedPrice = roomModel?.AgreedPrice ?? room.DefaultPrice;

            var contractRoom = new ContractRoom
            {
                ContractId = contractId,
                RoomId = roomId,
                PriceAgreed = agreedPrice,
                Notes = roomModel?.Notes
            };
            db.ContractRooms.Add(contractRoom);

            room.IsOccupied = true;
        }

        private void RemoveRoomFromContract(int contractId, int roomId)
        {
            var contractRoom = db.ContractRooms
                .FirstOrDefault(cr => cr.ContractId == contractId && cr.RoomId == roomId);

            if (contractRoom == null) return;

            // Remove tenants in room
            var tenantsInRoom = db.ContractTenants
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .ToList();

            foreach (var ct in tenantsInRoom)
            {
                db.ContractTenants.Remove(ct);

                var hasOtherContracts = db.ContractTenants
                    .Any(c => c.TenantId == ct.TenantId && c.ContractId != contractId);

                if (!hasOtherContracts)
                {
                    var tenant = db.Tenants.Find(ct.TenantId);
                    if (tenant != null)
                    {
                        if (!string.IsNullOrEmpty(tenant.Photo))
                        {
                            TenantPhotoHelper.DeleteTenantPhoto(tenant.Photo);
                        }
                        db.Tenants.Remove(tenant);
                    }
                }
            }

            var room = db.Rooms.Find(roomId);
            if (room != null)
            {
                room.IsOccupied = false;
            }

            db.ContractRooms.Remove(contractRoom);
        }

        private void LoadCreateViewData()
        {
            var availableRooms = db.Rooms
                .Where(r => !r.IsOccupied)
                .Select(r => new RoomSelectionModel
                {
                    RoomId = r.Id,
                    RoomName = r.Name,
                    DefaultPrice = r.DefaultPrice,
                    AgreedPrice = r.DefaultPrice,
                    IsSelected = false
                })
                .ToList();

            ViewBag.AvailableRooms = availableRooms;
        }

        private void LoadEditViewData(int contractId)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .FirstOrDefault(c => c.Id == contractId);

            if (contract != null)
            {
                if (contract.ContractType == "Individual")
                {
                    var currentRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId;
                    var occupiedRoomIds = db.ContractRooms
                        .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != contractId)
                        .Select(cr => cr.RoomId)
                        .ToList();

                    var availableRooms = db.Rooms
                        .Where(r => !occupiedRoomIds.Contains(r.Id) || r.Id == currentRoomId)
                        .ToList();

                    ViewBag.AvailableRooms = availableRooms.Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = r.Name + " - " + r.DefaultPrice.ToString("N0") + " VNĐ",
                        Selected = r.Id == currentRoomId
                    }).ToList();
                }
            }
        }

        #endregion

        #region AJAX Actions

        [HttpGet]
        public JsonResult GetAvailableRoomsForCompany(int contractId)
        {
            try
            {
                var currentContract = db.Contracts
                    .Include(c => c.ContractRooms)
                    .FirstOrDefault(c => c.Id == contractId);

                if (currentContract == null || currentContract.ContractType != "Company")
                {
                    return Json(new { success = false, message = "Invalid contract" },
                        JsonRequestBehavior.AllowGet);
                }

                var occupiedRoomIds = db.ContractRooms
                    .Where(cr => cr.Contract.Status == "Active" && cr.ContractId != contractId)
                    .Select(cr => cr.RoomId)
                    .ToList();

                var currentRoomIds = currentContract.ContractRooms
                    .Select(cr => cr.RoomId)
                    .ToList();

                var allRooms = db.Rooms.ToList();

                var availableRooms = allRooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id) || currentRoomIds.Contains(r.Id))
                    .Select(r => new
                    {
                        Id = r.Id,
                        Name = r.Name ?? $"Phòng {r.Id}",
                        DefaultPrice = r.DefaultPrice,
                        FormattedPrice = r.DefaultPrice.ToString("N0") + " VNĐ",
                        Area = r.Area,
                        Floor = r.Floor,
                        IsCurrentlySelected = currentRoomIds.Contains(r.Id)
                    })
                    .OrderBy(r => r.Name)
                    .ToList();

                return Json(new { success = true, rooms = availableRooms },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

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