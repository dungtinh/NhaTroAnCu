using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
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

            return View(roomViewModels);
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
            }

            if (validContract != null)
            {
                contractEndDate = validContract.EndDate;

                // Kiểm tra sắp hết hạn
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

                // Lấy bill cho hợp đồng này
                var bill = db.UtilityBills
                    .FirstOrDefault(b => b.Month == selectedMonth
                        && b.Year == selectedYear
                        && b.ContractId == validContract.Id);

                decimal mustPay = bill?.TotalAmount ?? 0;

                // Lấy payment history cho hợp đồng này
                decimal paid = db.PaymentHistories
                    .Where(p => p.RoomId == room.Id
                        && p.Month == selectedMonth
                        && p.Year == selectedYear
                        && p.ContractId == validContract.Id)
                    .Sum(p => (decimal?)p.TotalAmount) ?? 0;

                // Xác định màu sắc
                if (isContractExpired)
                {
                    colorClass = "red"; // Hết hạn
                }
                else if (mustPay == 0)
                {
                    colorClass = "gray"; // Chưa có bill
                }
                else if (paid >= mustPay)
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
    }
}