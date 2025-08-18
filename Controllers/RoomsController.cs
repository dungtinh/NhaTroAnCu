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
                        && (cr.Contract.EndDate == null || cr.Contract.EndDate >= filterDate))
            .Select(cr => cr.Contract)
            .FirstOrDefault();

        // Kiểm tra nếu có hợp đồng active nhưng đã hết hạn
        if (activeContract != null && activeContract.EndDate < filterDate)
        {
            isContractExpired = true;
            validContract = activeContract;
        }

        // Lấy bill cho phòng này
        var bill = db.UtilityBills
            .FirstOrDefault(b => b.Month == selectedMonth
                && b.Year == selectedYear
                && validContract != null
                && b.ContractId == validContract.Id);

        decimal mustPay = bill != null ? bill.TotalAmount : 0;

        // Lấy payment history cho phòng này
        decimal paid = db.PaymentHistories
            .Where(p => p.RoomId == room.Id
                && p.Month == selectedMonth
                && p.Year == selectedYear
                && (validContract == null || p.ContractId == validContract.Id))
            .Sum(p => (decimal?)p.TotalAmount) ?? 0;

        // Xác định màu sắc cho phòng
        if (isContractExpired)
        {
            colorClass = "expired";
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
        if (validContract != null && !isContractExpired)
        {
            var daysLeft = (validContract.EndDate - now).TotalDays;
            isContractNearingEnd = daysLeft > 0 && daysLeft <= 31;
        }

        // Lấy tên người thuê từ ContractTenants của phòng này
        var tenantName = "";
        if (validContract != null)
        {
            var contractTenant = room.ContractTenants
                .FirstOrDefault(ct => ct.ContractId == validContract.Id && ct.RoomId == room.Id);
            tenantName = contractTenant?.Tenant?.FullName ?? "";
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

        var prevIndex = 0;
        if (activeContract != null)
        {
            var bill = db.UtilityBills.FirstOrDefault(b =>
                b.Month == currentMonth &&
                b.Year == currentYear &&
                b.ContractId == activeContract.Id);

            var payment = db.PaymentHistories
                .FirstOrDefault(p => p.RoomId == room.Id
                    && p.Month == currentMonth
                    && p.Year == currentYear
                    && p.ContractId == activeContract.Id);

            ViewBag.Bill = bill;
            ViewBag.Payment = payment;
            prevIndex = bill?.WaterIndexEnd ?? 0;

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

            // Lấy tenants cho phòng này
            var roomTenants = activeContract.ContractTenants
                .Where(ct => ct.RoomId == room.Id)
                .ToList();
            ViewBag.RoomTenants = roomTenants;
        }
        else
        {
            ViewBag.Payment = null;
            ViewBag.ExtraCharge = 0;
            ViewBag.Discount = 0;
            ViewBag.RoomTenants = null;
        }

        // Lấy chỉ số nước tháng trước
        var prevMonth = currentMonth == 1 ? 12 : currentMonth - 1;
        var prevYear = currentMonth == 1 ? currentYear - 1 : currentYear;

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