using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
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
        var validContract = room.Contracts
            .FirstOrDefault(c => c.Status == "Active"
                && c.StartDate <= filterDate
                && (c.EndDate == null || c.EndDate >= filterDate));

        var bill = room.UtilityBills
            .FirstOrDefault(b => b.Month == selectedMonth && b.Year == selectedYear);

        decimal mustPay = bill?.TotalAmount ?? 0;
        decimal paid = room.PaymentHistories
            .Where(p => p.Month == selectedMonth
                && p.Year == selectedYear
                && (validContract == null || p.ContractId == validContract.Id))
            .Sum(p => p.TotalAmount);

        string colorClass = DetermineColorClass(validContract, paid, mustPay);

        bool isContractNearingEnd = false;
        DateTime? contractEndDate = validContract?.EndDate;

        if (validContract != null && contractEndDate.HasValue)
        {
            var daysLeft = (contractEndDate.Value - now).TotalDays;
            isContractNearingEnd = daysLeft > 0 && daysLeft <= 31;
        }

        return new RoomViewModel
        {
            Room = room,
            ColorClass = colorClass,
            TenantName = validContract?.ContractTenants?.FirstOrDefault()?.Tenant?.FullName ?? "",
            IsContractNearingEnd = isContractNearingEnd,
            ContractEndDate = contractEndDate
        };
    }

    private string DetermineColorClass(Contract contract, decimal paid, decimal mustPay)
    {
        if (contract == null) return "gray";
        if (paid == 0) return "red";
        if (mustPay == 0) return "gray";
        if (paid >= mustPay) return "green";
        return "yellow";
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
            var payment = db.PaymentHistories
                .FirstOrDefault(p => p.RoomId == room.Id
                    && p.Month == currentMonth
                    && p.Year == currentYear
                    && p.ContractId == activeContract.Id);

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