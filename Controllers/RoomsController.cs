using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Office2010.Excel;
using NhaTroAnCu.Models;

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
            .Include(r => r.Contracts.Select(c => c.ContractTenants.Select(ct => ct.Tenant)))
            .Include(r => r.UtilityBills)
            .Include(r => r.PaymentHistories)
            .ToList();

        var roomViewModels = rooms.Select(room => GetRoomViewModel(room, filterDate, selectedMonth, selectedYear, now))
                                  .ToList();

        return View(roomViewModels);
    }

    private RoomViewModel GetRoomViewModel(Room room, DateTime filterDate, int selectedMonth, int selectedYear, DateTime now)
    {
        string colorClass = "gray";
        bool isContractExpired = false; // Thêm biến kiểm tra hợp đồng đã hết hạn

        // Tìm hợp đồng đang active (bao gồm cả đã hết hạn)
        var activeContract = room.Contracts
            .Where(c => c.Status == "Active")
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();

        // Tìm hợp đồng còn hiệu lực tại thời điểm filter
        var validContract = room.Contracts
            .Where(c => c.Status == "Active"
                        && c.StartDate <= filterDate
                        && (c.EndDate == null || c.EndDate >= filterDate))
            .FirstOrDefault();

        // Kiểm tra nếu có hợp đồng active nhưng đã hết hạn
        if (activeContract != null && activeContract.EndDate < filterDate)
        {
            isContractExpired = true;
            // Dùng activeContract thay vì validContract cho phòng hết hạn
            validContract = activeContract;
        }

        var bill = room.UtilityBills.FirstOrDefault(b => b.Month == selectedMonth && b.Year == selectedYear);

        decimal mustPay = bill != null ? (bill.TotalAmount ?? 0) : 0;
        decimal paid = room.PaymentHistories
            .Where(p => p.Month == selectedMonth && p.Year == selectedYear && (validContract == null || p.ContractId == validContract.Id))
            .Sum(p => p.TotalAmount);

        // Xác định màu sắc cho phòng
        if (isContractExpired)
        {
            // Phòng đã hết hạn hợp đồng - luôn hiển thị màu đỏ cảnh báo
            colorClass = "expired"; // Sử dụng class mới cho phòng hết hạn
        }
        else if (validContract != null)
        {
            if (paid == 0)
                colorClass = "red";
            else if (mustPay == 0)
                colorClass = "gray";
            else if (paid >= mustPay)
                colorClass = "green";
            else
                colorClass = "yellow";
        }

        // Tính toán cảnh báo hợp đồng sắp hết hạn
        bool isContractNearingEnd = false;
        DateTime? contractEndDate = validContract?.EndDate;
        if (validContract != null && !isContractExpired) // Không cảnh báo sắp hết hạn nếu đã hết hạn
        {
            var daysLeft = (validContract.EndDate - now).TotalDays;
            isContractNearingEnd = daysLeft > 0 && daysLeft <= 31;
        }

        return new NhaTroAnCu.Models.RoomViewModel
        {
            Room = room,
            ColorClass = colorClass,
            TenantName = validContract?.ContractTenants?.FirstOrDefault()?.Tenant?.FullName ?? "",
            IsContractNearingEnd = isContractNearingEnd,
            IsContractExpired = isContractExpired, // Thêm property mới
            ContractEndDate = contractEndDate
        };
    }
    public ActionResult Details(int id)
    {
        var room = db.Rooms.Find(id);
        if (room == null) return HttpNotFound();

        var activeContract = db.Contracts
            .Include("ContractTenants.Tenant")
            .FirstOrDefault(c => c.RoomId == id && c.Status == "Active");

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
            var bill = db.UtilityBills.FirstOrDefault(b =>
                b.RoomId == room.Id &&
                b.Month == currentMonth &&
                b.Year == currentYear &&
                b.ContractId == activeContract.Id);

            var payment = db.PaymentHistories
                .FirstOrDefault(p => p.RoomId == room.Id
                    && p.Month == currentMonth
                    && p.Year == currentYear
                    && p.ContractId == activeContract.Id);

            ViewBag.Bill = bill; // Thêm bill vào ViewBag
            ViewBag.Payment = payment;

            // Tính extra/discount cho tháng đầu
            decimal extraCharge = 0, discount = 0;
            if (now.Month == activeContract.MoveInDate.Month && now.Year == activeContract.MoveInDate.Year)
            {
                var moveInDay = activeContract.MoveInDate.Day;
                var pricePerDay = activeContract.PriceAgreed / 30;

                if (moveInDay < 10)
                    extraCharge = pricePerDay * (10 - moveInDay);
                else if (moveInDay > 10)
                    discount = pricePerDay * (moveInDay - 10);
            }

            ViewBag.ExtraCharge = extraCharge;
            ViewBag.Discount = discount;
        }
        else
        {
            ViewBag.Payment = null;
            ViewBag.ExtraCharge = 0;
            ViewBag.Discount = 0;
        }

        // Lấy chỉ số nước tháng trước
        var prevMonth = currentMonth == 1 ? 12 : currentMonth - 1;
        var prevYear = currentMonth == 1 ? currentYear - 1 : currentYear;

        var prevIndex = db.WaterIndexes
            .Where(x => x.RoomId == room.Id && x.Month == prevMonth && x.Year == prevYear)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.WaterReading)
            .FirstOrDefault();

        ViewBag.WaterPrev = prevIndex;
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