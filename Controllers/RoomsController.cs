using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Wordprocessing;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class RoomsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult Index(int? day, int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;
            int selectedDay = day ?? DateTime.DaysInMonth(selectedYear, selectedMonth);

            ViewBag.SelectedDay = selectedDay;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            var filterDate = new DateTime(selectedYear, selectedMonth, selectedDay);

            var rooms = db.Rooms
                .Include(r => r.ContractRooms.Select(cr => cr.Contract))
                .Include(r => r.ContractTenants.Select(ct => ct.Tenant))
                .ToList();

            var roomViewModels = rooms.Select(room => GetRoomViewModel(room, filterDate, selectedMonth, selectedYear, now))
                                      .ToList();

            // Tính toán thống kê cho Summary Dashboard
            CalculateSummaryStatistics(roomViewModels, filterDate);

            return View(roomViewModels);
        }

        private void CalculateSummaryStatistics(List<RoomViewModel> roomViewModels, DateTime filterDate)
        {
            // Tổng số phòng
            int totalRooms = roomViewModels.Count;

            // Phòng đã thuê (có hợp đồng active)
            int rentedRooms = roomViewModels.Count(r =>
                r.Room.ContractRooms.Any(cr =>
                    cr.Contract.Status == "Active" &&
                    cr.Contract.StartDate <= filterDate &&
                    cr.Contract.EndDate >= filterDate));

            // Phòng còn trống
            int emptyRooms = totalRooms - rentedRooms;

            // Phòng sắp hết hạn (trong vòng 31 ngày)
            int nearExpiredRooms = roomViewModels.Count(r => r.IsContractNearingEnd);

            // Phòng đã hết hạn hợp đồng
            int expiredRooms = roomViewModels.Count(r => r.IsContractExpired);

            // Tính hiệu suất sử dụng phòng
            decimal occupancyRate = totalRooms > 0 ? (decimal)rentedRooms / totalRooms : 0;

            // Gán vào ViewBag để sử dụng trong View
            ViewBag.TotalRooms = totalRooms;
            ViewBag.RentedRooms = rentedRooms;
            ViewBag.EmptyRooms = emptyRooms;
            ViewBag.NearExpiredRooms = nearExpiredRooms;
            ViewBag.ExpiredRooms = expiredRooms;
            ViewBag.OccupancyRate = occupancyRate;
        }

        private RoomViewModel GetRoomViewModel(Room room, DateTime filterDate, int selectedMonth, int selectedYear, DateTime now)
        {
            string colorClass = "gray";
            bool isContractExpired = false;
            bool isContractNearingEnd = false;
            string tenantName = "";
            DateTime? contractEndDate = null;

            // Tìm hợp đồng active cho phòng này thông qua ContractRooms
            var activeContract = room.ContractRooms
                .Where(cr => cr.Contract.Status == "Active")
                .OrderByDescending(cr => cr.Contract.StartDate)
                .Select(cr => cr.Contract)
                .FirstOrDefault();

            // Tìm hợp đồng còn hiệu lực tại thời điểm filter
            var validContract = room.ContractRooms
                .Where(cr => cr.Contract.Status == "Active"
                            && cr.Contract.StartDate <= filterDate
                            && cr.Contract.EndDate >= filterDate)
                .Select(cr => cr.Contract)
                .FirstOrDefault();

            // Kiểm tra nếu có hợp đồng active nhưng đã hết hạn
            if (activeContract != null && activeContract.EndDate < filterDate)
            {
                isContractExpired = true;
                validContract = activeContract;
                colorClass = "expired"; // Màu cho phòng hết hạn
            }

            if (validContract != null)
            {
                contractEndDate = validContract.EndDate;

                // Kiểm tra sắp hết hạn (trong vòng 31 ngày)
                var daysUntilEnd = (validContract.EndDate - filterDate).TotalDays;
                if (daysUntilEnd > 0 && daysUntilEnd <= 31)
                {
                    isContractNearingEnd = true;
                }

                // Lấy tên khách thuê từ ContractTenants
                var contractTenant = db.ContractTenants
                    .Include(ct => ct.Tenant)
                    .FirstOrDefault(ct => ct.ContractId == validContract.Id && ct.RoomId == room.Id);

                if (contractTenant != null)
                {
                    tenantName = contractTenant.Tenant.FullName;
                }

                // Nếu không phải hết hạn, xác định màu sắc dựa trên thanh toán
                if (!isContractExpired)
                {
                    // Lấy bill cho hợp đồng này
                    var bill = db.UtilityBills
                        .FirstOrDefault(b => b.Month == selectedMonth
                            && b.Year == selectedYear
                            && b.ContractId == validContract.Id);

                    if (bill != null)
                    {
                        decimal mustPay = bill.TotalAmount;

                        // Lấy payment history cho hợp đồng này
                        decimal paid = db.PaymentHistories
                            .Where(p => p.RoomId == room.Id
                                && p.Month == selectedMonth
                                && p.Year == selectedYear
                                && p.ContractId == validContract.Id)
                            .Sum(p => (decimal?)p.TotalAmount) ?? 0;

                        // Xác định màu sắc theo tình trạng thanh toán
                        if (paid >= mustPay)
                        {
                            colorClass = "green"; // Đã thanh toán đủ
                        }
                        else if (paid > 0)
                        {
                            colorClass = "yellow"; // Thanh toán một phần
                        }
                        else
                        {
                            colorClass = "orange"; // Chưa thanh toán
                        }
                    }
                    else
                    {
                        // Chưa có bill
                        colorClass = "gray";
                    }
                }
            }
            else
            {
                // Phòng không có hợp đồng
                colorClass = room.IsOccupied ? "blue" : "gray";
                tenantName = room.IsOccupied ? "Có người ở" : "";
            }

            return new RoomViewModel
            {
                Room = room,
                ColorClass = colorClass,
                TenantName = tenantName,
                IsContractNearingEnd = isContractNearingEnd,
                IsContractExpired = isContractExpired,
                ContractEndDate = contractEndDate
            };
        }

        public ActionResult Details(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            // Tìm hợp đồng active cho phòng này
            var activeContract = db.Contracts
                .Include(c => c.ContractRooms)
                .Include(c => c.ContractTenants.Select(ct => ct.Tenant))
                .Include(c => c.ContractExtensionHistories)
                .Where(c => c.Status == "Active" && c.ContractRooms.Any(cr => cr.RoomId == id))
                .FirstOrDefault();

            var now = DateTime.Now;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            SetupViewBagForDetails(room, activeContract, currentMonth, currentYear, now);

            return View(room);
        }

        private void SetupViewBagForDetails(Room room, Contract activeContract, int currentMonth, int currentYear, DateTime now)
        {
            ViewBag.ActiveContract = activeContract;
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.CurrentYear = currentYear;

            if (activeContract != null)
            {
                // Lấy bill cho hợp đồng này
                var bill = db.UtilityBills.FirstOrDefault(b =>
                    b.Month == currentMonth &&
                    b.Year == currentYear &&
                    b.ContractId == activeContract.Id);

                // Lấy payment cho phòng và hợp đồng này
                var payment = db.PaymentHistories
                    .FirstOrDefault(p => p.RoomId == room.Id
                        && p.Month == currentMonth
                        && p.Year == currentYear
                        && p.ContractId == activeContract.Id);

                ViewBag.Bill = bill;
                ViewBag.Payment = payment;

                // Lấy chỉ số nước tháng trước
                var prevBill = db.UtilityBills
                    .Where(b => b.ContractId == activeContract.Id)
                    .OrderByDescending(b => b.Year)
                    .ThenByDescending(b => b.Month)
                    .FirstOrDefault();

                ViewBag.WaterPrev = prevBill?.WaterIndexEnd ?? 0;

                // Tính extra/discount cho tháng đầu
                decimal extraCharge = 0, discount = 0;
                if (now.Month == activeContract.MoveInDate.Month && now.Year == activeContract.MoveInDate.Year)
                {
                    var contractRoom = activeContract.ContractRooms.FirstOrDefault(cr => cr.RoomId == room.Id);
                    var moveInDay = activeContract.MoveInDate.Day;
                    var pricePerDay = (contractRoom?.PriceAgreed ?? room.DefaultPrice) / 30;

                    if (moveInDay < 10)
                        extraCharge = pricePerDay * (10 - moveInDay);
                    else if (moveInDay > 10)
                        discount = pricePerDay * (moveInDay - 10);
                }

                ViewBag.ExtraCharge = extraCharge;
                ViewBag.Discount = discount;

                // Lấy tenants cho phòng này
                var roomTenants = db.ContractTenants
                    .Include(ct => ct.Tenant)
                    .Where(ct => ct.ContractId == activeContract.Id && ct.RoomId == room.Id)
                    .ToList();
                ViewBag.RoomTenants = roomTenants;
            }
            else
            {
                ViewBag.Bill = null;
                ViewBag.Payment = null;
                ViewBag.WaterPrev = 0;
                ViewBag.ExtraCharge = 0;
                ViewBag.Discount = 0;
                ViewBag.RoomTenants = null;
            }
        }

        [HttpPost]
        public ActionResult ToggleOccupied(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            room.IsOccupied = !room.IsOccupied;
            db.SaveChanges();

            return Json(new { success = true, status = room.IsOccupied });
        }

        // GET: Rooms/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Room room)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(room.Name))
                    room.Name = $"P{room.Area}{room.Floor}{room.RoomNumber}";

                db.Rooms.Add(room);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(room);
        }

        // GET: Rooms/Edit/5
        public ActionResult Edit(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Room room)
        {
            if (ModelState.IsValid)
            {
                db.Entry(room).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(room);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
        // GET: /Rooms/ManageEmployees/5
        public ActionResult ManageTenants(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            // Tìm contract active của phòng
            var contractRoom = db.ContractRooms
                .Include(cr => cr.Contract.Company)
                .FirstOrDefault(cr => cr.RoomId == id
                    && cr.Contract.Status == "Active");

            if (contractRoom == null)
            {
                TempData["Error"] = "Phòng này chưa có hợp đồng active";
                return RedirectToAction("Details", new { id });
            }

            // Lấy danh sách người thuê hiện tại trong phòng
            var currentTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractRoom.ContractId && ct.RoomId == id)
                .Select(ct => new TenantViewModel
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
                    JoinDate = ct.CreatedAt
                })
                .ToList();

            var model = new RoomTenantManagementViewModel
            {
                RoomId = id,
                RoomName = room.Name,
                ContractId = contractRoom.ContractId,
                CompanyName = contractRoom.Contract.Company?.CompanyName,
                ContractType = contractRoom.Contract.ContractType ?? "Individual",
                ContractStartDate = contractRoom.Contract.StartDate,
                ContractEndDate = contractRoom.Contract.EndDate,
                CurrentTenants = currentTenants
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTenant(int roomId, TenantInputModel model, HttpPostedFileBase photoFile)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin";
                return RedirectToAction("ManageTenants", new { id = roomId });
            }

            // Kiểm tra contract
            var contractRoom = db.ContractRooms
                .Include(cr => cr.Contract)
                .FirstOrDefault(cr => cr.RoomId == roomId
                    && cr.Contract.Status == "Active");

            if (contractRoom == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng active cho phòng này";
                return RedirectToAction("Details", new { id = roomId });
            }

            // Kiểm tra giới hạn số người trong phòng
            var currentTenantCount = db.ContractTenants
                .Count(ct => ct.ContractId == contractRoom.ContractId && ct.RoomId == roomId);

            if (currentTenantCount >= 4) // Giới hạn 4 người/phòng
            {
                TempData["Error"] = "Phòng đã đạt giới hạn số người (tối đa 4 người)";
                return RedirectToAction("ManageTenants", new { id = roomId });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Kiểm tra xem CCCD đã tồn tại chưa
                    var existingTenant = db.Tenants
                        .FirstOrDefault(t => t.IdentityCard == model.IdentityCard);

                    Tenant tenant;
                    if (existingTenant != null)
                    {
                        // Cập nhật thông tin tenant existing
                        tenant = existingTenant;
                        tenant.FullName = model.FullName;
                        tenant.PhoneNumber = model.PhoneNumber;
                        tenant.BirthDate = model.BirthDate;
                        tenant.Gender = model.Gender;
                        tenant.PermanentAddress = model.PermanentAddress;
                        tenant.Ethnicity = model.Ethnicity;
                        tenant.VehiclePlate = model.VehiclePlate;

                        // Set CompanyId nếu là hợp đồng công ty
                        if (contractRoom.Contract.CompanyId.HasValue)
                        {
                            tenant.CompanyId = contractRoom.Contract.CompanyId;
                        }

                        // Kiểm tra xem tenant này đã ở phòng khác chưa
                        var otherRoom = db.ContractTenants
                            .Include(ct => ct.Room)
                            .Include(ct => ct.Contract)
                            .FirstOrDefault(ct => ct.TenantId == tenant.Id
                                && ct.Contract.Status == "Active"
                                && ct.RoomId != roomId);

                        if (otherRoom != null)
                        {
                            TempData["Warning"] = $"Người thuê này đang ở phòng {otherRoom.Room.Name}. Đã chuyển sang phòng mới.";
                            // Remove from old room
                            db.ContractTenants.Remove(otherRoom);
                        }
                    }
                    else
                    {
                        // Tạo tenant mới
                        tenant = new Tenant
                        {
                            FullName = model.FullName,
                            IdentityCard = model.IdentityCard,
                            PhoneNumber = model.PhoneNumber,
                            BirthDate = model.BirthDate,
                            Gender = model.Gender,
                            PermanentAddress = model.PermanentAddress,
                            Ethnicity = model.Ethnicity,
                            VehiclePlate = model.VehiclePlate,
                            // Set CompanyId nếu là hợp đồng công ty
                            CompanyId = contractRoom.Contract.CompanyId
                        };

                        // Xử lý upload ảnh CCCD
                        if (photoFile != null && photoFile.ContentLength > 0)
                        {
                            tenant.Photo = SaveTenantPhoto(photoFile);
                        }

                        db.Tenants.Add(tenant);
                    }

                    db.SaveChanges();

                    // Kiểm tra xem tenant đã trong contract này chưa
                    var existingContractTenant = db.ContractTenants
                        .FirstOrDefault(ct => ct.ContractId == contractRoom.ContractId
                            && ct.TenantId == tenant.Id
                            && ct.RoomId == roomId);

                    if (existingContractTenant == null)
                    {
                        // Thêm vào ContractTenants
                        var contractTenant = new ContractTenant
                        {
                            ContractId = contractRoom.ContractId,
                            TenantId = tenant.Id,
                            RoomId = roomId,
                            CreatedAt = DateTime.Now
                        };
                        db.ContractTenants.Add(contractTenant);
                        db.SaveChanges();
                    }

                    transaction.Commit();
                    TempData["Success"] = $"Đã thêm {tenant.FullName} vào phòng {contractRoom.Room.Name}";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                }
            }

            return RedirectToAction("ManageTenants", new { id = roomId });
        }

        // POST: /Rooms/RemoveTenant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveTenant(int roomId, int tenantId)
        {
            var contractTenant = db.ContractTenants
                .Include(ct => ct.Tenant)
                .FirstOrDefault(ct => ct.RoomId == roomId && ct.TenantId == tenantId);

            if (contractTenant != null)
            {
                var tenantName = contractTenant.Tenant.FullName;
                db.ContractTenants.Remove(contractTenant);
                db.SaveChanges();

                TempData["Success"] = $"Đã xóa {tenantName} khỏi phòng";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy người thuê trong phòng này";
            }

            return RedirectToAction("ManageTenants", new { id = roomId });
        }

        // GET: /Rooms/TenantReport
        // Báo cáo danh sách người đang thuê trọ
        public ActionResult TenantReport(string contractType = "All", int? companyId = null)
        {
            var query = db.ContractTenants
                .Include(ct => ct.Contract)
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .Where(ct => ct.Contract.Status == "Active");

            // Filter by contract type
            if (contractType == "Company")
            {
                query = query.Where(ct => ct.Contract.ContractType == "Company");
                if (companyId.HasValue)
                {
                    query = query.Where(ct => ct.Contract.CompanyId == companyId);
                }
            }
            else if (contractType == "Individual")
            {
                query = query.Where(ct => ct.Contract.ContractType == "Individual");
            }

            var tenants = query
                .Select(ct => new
                {
                    ct.Tenant.FullName,
                    ct.Tenant.IdentityCard,
                    ct.Tenant.PhoneNumber,
                    ct.Tenant.BirthDate,
                    ct.Tenant.Gender,
                    ct.Tenant.VehiclePlate,
                    RoomName = ct.Room.Name,
                    ContractType = ct.Contract.ContractType,
                    CompanyName = ct.Contract.Company.CompanyName,
                    MoveInDate = ct.Contract.MoveInDate,
                    ContractEndDate = ct.Contract.EndDate
                })
                .ToList();

            ViewBag.ContractType = contractType;
            ViewBag.CompanyId = companyId;

            return View(tenants);
        }

        // Helper method to save tenant photo
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
    }
}