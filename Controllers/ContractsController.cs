using Microsoft.AspNet.Identity;
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
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Contracts
        public ActionResult Index()
        {
            var contracts = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.Company)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(contracts);
        }

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
                SelectedRooms = new List<RoomSelectionModel>() // Khởi tạo list
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

            // Load danh sách phòng trống - Đảm bảo là List
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
                .ToList(); // Chắc chắn convert to List

            ViewBag.AvailableRooms = availableRooms;

            return View(model);
        }

        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel model)
        {
            if (Request.Form["StartDate"] != null)
            {
                var startDateStr = Request.Form["StartDate"];
                var parsedDate = DateTimeHelper.ParseDate(startDateStr);
                if (parsedDate.HasValue)
                    model.StartDate = parsedDate.Value;
            }

            if (Request.Form["MoveInDate"] != null)
            {
                var moveInDateStr = Request.Form["MoveInDate"];
                var parsedDate = DateTimeHelper.ParseDate(moveInDateStr);
                if (parsedDate.HasValue)
                    model.MoveInDate = parsedDate.Value;
            }
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    Contract contract = new Contract
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
                        // Xử lý hợp đồng công ty
                        contract = CreateCompanyContract(model, contract);
                    }
                    else
                    {
                        // Xử lý hợp đồng cá nhân
                        contract = CreateIndividualContract(model, contract);
                    }

                    transaction.Commit();
                    return RedirectToAction("Details", new { id = contract.Id });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);

                    // Reload data for view
                    ViewBag.AvailableRooms = db.Rooms
                        .Where(r => !r.IsOccupied)
                        .Select(r => new RoomSelectionModel
                        {
                            RoomId = r.Id,
                            RoomName = r.Name,
                            DefaultPrice = r.DefaultPrice,
                            AgreedPrice = r.DefaultPrice
                        })
                        .ToList();

                    return View(model);
                }
            }
        }

        // Xử lý tạo hợp đồng công ty
        private Contract CreateCompanyContract(ContractCreateViewModel model, Contract contract)
        {
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"SelectedRooms count: {model.SelectedRooms?.Count ?? 0}");

            // 1. Tạo hoặc cập nhật thông tin công ty
            Company company;
            var existingCompany = db.Companies
                .FirstOrDefault(c => c.TaxCode == model.Company.TaxCode);

            if (existingCompany != null)
            {
                company = existingCompany;
                // Update company info
                company.CompanyName = model.Company.CompanyName;
                company.Address = model.Company.Address;
                company.Representative = model.Company.Representative;
                company.RepresentativePhone = model.Company.RepresentativePhone;
                company.Email = model.Company.Email;
                company.UpdatedAt = DateTime.Now;
            }
            else
            {
                company = new Company
                {
                    CompanyName = model.Company.CompanyName,
                    TaxCode = model.Company.TaxCode,
                    Address = model.Company.Address,
                    Email = model.Company.Email,
                    Representative = model.Company.Representative,
                    RepresentativePhone = model.Company.RepresentativePhone,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };
                db.Companies.Add(company);
            }

            db.SaveChanges();
            contract.CompanyId = company.Id;

            // 2. Lưu contract
            contract.PriceAgreed = 0;
            db.Contracts.Add(contract);
            db.SaveChanges();

            // 3. Tạo ContractRooms cho các phòng được chọn
            decimal totalPrice = 0;
            int roomCount = 0;

            if (model.SelectedRooms != null && model.SelectedRooms.Any())
            {
                foreach (var roomSelection in model.SelectedRooms.Where(r => r.IsSelected))
                {
                    // Debug log
                    System.Diagnostics.Debug.WriteLine($"Processing room: {roomSelection.RoomId}, Price: {roomSelection.AgreedPrice}");

                    // Kiểm tra phòng tồn tại
                    var room = db.Rooms.Find(roomSelection.RoomId);
                    if (room == null)
                    {
                        throw new Exception($"Phòng với ID {roomSelection.RoomId} không tồn tại");
                    }

                    // Kiểm tra phòng còn trống
                    if (room.IsOccupied)
                    {
                        throw new Exception($"Phòng {room.Name} đã được thuê");
                    }

                    var contractRoom = new ContractRoom
                    {
                        ContractId = contract.Id,
                        RoomId = roomSelection.RoomId,
                        PriceAgreed = roomSelection.AgreedPrice > 0 ? roomSelection.AgreedPrice : room.DefaultPrice,
                        Notes = roomSelection.Notes
                    };

                    db.ContractRooms.Add(contractRoom);
                    totalPrice += contractRoom.PriceAgreed;
                    roomCount++;

                    // Cập nhật trạng thái phòng
                    room.IsOccupied = true;
                }
            }

            // Kiểm tra có phòng nào được chọn không
            if (roomCount == 0)
            {
                throw new Exception("Vui lòng chọn ít nhất một phòng cho hợp đồng công ty");
            }

            // Cập nhật tổng giá trị hợp đồng
            contract.PriceAgreed = totalPrice;

            db.SaveChanges();
            return contract;
        }

        // Xử lý tạo hợp đồng cá nhân
        private Contract CreateIndividualContract(ContractCreateViewModel model, Contract contract)
        {
            // 1. Kiểm tra phòng
            if (!model.SingleRoomId.HasValue)
            {
                throw new Exception("Vui lòng chọn phòng cho hợp đồng cá nhân");
            }

            var room = db.Rooms.Find(model.SingleRoomId.Value);
            if (room == null)
            {
                throw new Exception("Phòng không tồn tại");
            }

            // 2. Set giá cho contract
            contract.PriceAgreed = model.SingleRoomPrice ?? room.DefaultPrice;

            // 3. Lưu contract
            db.Contracts.Add(contract);
            db.SaveChanges();

            // 4. Tạo ContractRoom
            var contractRoom = new ContractRoom
            {
                ContractId = contract.Id,
                RoomId = room.Id,
                PriceAgreed = contract.PriceAgreed,
                Notes = model.Note
            };
            db.ContractRooms.Add(contractRoom);

            // 5. Tạo Tenant nếu có thông tin
            // Xử lý danh sách người thuê
            if (model.Tenants != null && model.Tenants.Any())
            {
                foreach (var tenantModel in model.Tenants)
                {
                    if (string.IsNullOrEmpty(tenantModel.FullName) || string.IsNullOrEmpty(tenantModel.IdentityCard))
                    {
                        continue;
                    }

                    var existingTenant = db.Tenants
                        .FirstOrDefault(t => t.IdentityCard == tenantModel.IdentityCard);

                    Tenant tenant;
                    if (existingTenant != null)
                    {
                        tenant = existingTenant;
                        tenant.FullName = tenantModel.FullName;
                        tenant.PhoneNumber = tenantModel.PhoneNumber;
                        tenant.BirthDate = tenantModel.BirthDate;
                        tenant.Gender = tenantModel.Gender;
                        tenant.PermanentAddress = tenantModel.PermanentAddress;
                        tenant.Ethnicity = tenantModel.Ethnicity;
                        tenant.VehiclePlate = tenantModel.VehiclePlate;
                    }
                    else
                    {
                        tenant = new Tenant
                        {
                            FullName = tenantModel.FullName,
                            IdentityCard = tenantModel.IdentityCard,
                            PhoneNumber = tenantModel.PhoneNumber,
                            BirthDate = tenantModel.BirthDate,
                            Gender = tenantModel.Gender,
                            PermanentAddress = tenantModel.PermanentAddress,
                            Ethnicity = tenantModel.Ethnicity,
                            VehiclePlate = tenantModel.VehiclePlate,
                            CompanyId = null
                        };

                        // Sử dụng TenantPhotoHelper để xử lý upload ảnh
                        var tenantIndex = model.Tenants.IndexOf(tenantModel);
                        var photoKey = $"TenantPhotos[{tenantIndex}]";

                        if (Request.Files[photoKey] != null)
                        {
                            var photoFile = Request.Files[photoKey];
                            try
                            {
                                tenant.Photo = TenantPhotoHelper.SaveTenantPhoto(photoFile, tenant.IdentityCard);
                            }
                            catch (Exception ex)
                            {
                                // Log error hoặc thêm vào ModelState
                                ModelState.AddModelError("", $"Lỗi upload ảnh cho {tenant.FullName}: {ex.Message}");
                            }
                        }

                        db.Tenants.Add(tenant);
                    }

                    db.SaveChanges();

                    var contractTenant = new ContractTenant
                    {
                        ContractId = contract.Id,
                        TenantId = tenant.Id,
                        RoomId = room.Id,
                        CreatedAt = DateTime.Now
                    };
                    db.ContractTenants.Add(contractTenant);
                }
            }

            // Cập nhật trạng thái phòng
            room.IsOccupied = true;
            db.SaveChanges();

            // 7. Cập nhật trạng thái phòng
            room.IsOccupied = true;

            db.SaveChanges();
            return contract;
        }


        // GET: /Contracts/Details/5
        public ActionResult Details(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .Include(c => c.ContractExtensionHistories)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            return View(contract);
        }

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.Company)
                .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            ViewBag.Contract = contract;
            return View();
        }

        // POST: /Contracts/EndConfirm
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                if (expenseCategory == null)
                {
                    expenseCategory = new IncomeExpenseCategory
                    {
                        Name = "Trả tiền cọc",
                        Type = "Expense",
                        IsSystem = true,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.IncomeExpenseCategories.Add(expenseCategory);
                    db.SaveChanges();
                }

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

            db.SaveChanges();

            // Redirect về phòng đầu tiên trong hợp đồng
            var firstRoomId = contract.ContractRooms.FirstOrDefault()?.RoomId ?? 0;
            return RedirectToAction("Details", "Rooms", new { id = firstRoomId });
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