using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using NhaTroAnCu.Models;
using NhaTroAnCu.Helpers;

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

        var rooms = db.Rooms.Include(r => r.Contracts.Select(c => c.ContractTenants.Select(ct => ct.Tenant)))
                            .Include(r => r.UtilityBills)
                            .Include(r => r.PaymentHistories)
                            .ToList();

        var roomViewModels = rooms.Select(room =>
        {
            string colorClass = "gray";
            var validContract = room.Contracts
                .Where(c => c.Status == "Active"
                            && c.StartDate <= filterDate
                            && (c.EndDate == null || c.EndDate >= filterDate))
                .FirstOrDefault();

            var bill = room.UtilityBills.FirstOrDefault(b => b.Month == selectedMonth && b.Year == selectedYear);

            decimal mustPay = bill != null ? (bill.TotalAmount ?? 0) : 0;
            decimal paid = room.PaymentHistories
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear && (validContract == null || p.ContractId == validContract.Id))
                .Sum(p => p.TotalAmount);

            if (validContract != null)
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
            if (validContract != null)
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
                ContractEndDate = contractEndDate
            };
        }).ToList();

        return View(roomViewModels);
    }

    // GET: /Rooms/Details/5
    //public ActionResult Details(int id)
    //{
    //    var room = db.Rooms
    //        .Include(r => r.Contracts.Select(c => c.ContractTenants.Select(ct => ct.Tenant)))
    //        .Include(r => r.UtilityBills)
    //        .Include(r => r.PaymentHistories)
    //        .FirstOrDefault(r => r.Id == id);

    //    // Tháng hiện tại
    //    var activeContract = db.Contracts
    //   .Include("ContractTenants.Tenant")
    //   .FirstOrDefault(c => c.RoomId == id && c.Status == "Active");
    //    var now = DateTime.Now;
    //    var currentMonth = now.Month;
    //    var currentYear = now.Year;

    //    // Kiểm tra đã thu tiền tháng này chưa
    //    var payment = db.PaymentHistories.FirstOrDefault(p =>
    //        p.RoomId == id && p.Month == currentMonth && p.Year == currentYear);


    //    ViewBag.ActiveContract = activeContract;
    //    ViewBag.Payment = payment;
    //    ViewBag.CurrentMonth = currentMonth;
    //    ViewBag.CurrentYear = currentYear;

    //    if (room == null)
    //        return HttpNotFound();

    //    return View(room);
    //}

    // AJAX: /Rooms/ToggleOccupied

    //public ActionResult Details(int id)
    //{
    //    var room = db.Rooms.Find(id);
    //    if (room == null) return HttpNotFound();

    //    var activeContract = db.Contracts
    //        .Include("ContractTenants.Tenant")
    //        .FirstOrDefault(c => c.RoomId == id && c.Status == "Active");

    //    // Tháng hiện tại
    //    var now = DateTime.Now;
    //    var currentMonth = now.Month;
    //    var currentYear = now.Year;

    //    // Kiểm tra đã thu tiền tháng này chưa
    //    var payment = db.PaymentHistories.FirstOrDefault(p =>
    //        p.RoomId == id && p.Month == currentMonth && p.Year == currentYear && p.ContractId == activeContract.Id);

    //    ViewBag.ActiveContract = activeContract;
    //    ViewBag.Payment = payment;
    //    ViewBag.CurrentMonth = currentMonth;
    //    ViewBag.CurrentYear = currentYear;

    //    return View(room);
    //}
    public ActionResult Details(int id)
    {
        var room = db.Rooms.Find(id);
        if (room == null) return HttpNotFound();

        var activeContract = db.Contracts
            .Include("ContractTenants.Tenant")
            .FirstOrDefault(c => c.RoomId == id && c.Status == "Active");

        // Tháng hiện tại
        var now = DateTime.Now;
        var currentMonth = now.Month;
        var currentYear = now.Year;

        if (activeContract != null)
        {
            // Tìm phiếu báo tiền theo cả RoomId và ContractId
            var bill = db.UtilityBills.FirstOrDefault(b =>
                b.RoomId == id &&
                b.Month == currentMonth &&
                b.Year == currentYear &&
                b.ContractId == activeContract.Id);

            var payment = db.PaymentHistories.FirstOrDefault(p =>
                p.RoomId == id &&
                p.Month == currentMonth &&
                p.Year == currentYear &&
                p.ContractId == activeContract.Id
            );

            ViewBag.ActiveContract = activeContract;
            ViewBag.Payment = payment;
            ViewBag.Bill = bill; // Thêm bill vào ViewBag
        }
        else
        {
            ViewBag.ActiveContract = null;
            ViewBag.Payment = null;
            ViewBag.Bill = null;
        }

        ViewBag.CurrentMonth = currentMonth;
        ViewBag.CurrentYear = currentYear;

        // --- TÍNH TIỀN EXTRA/DISCOUNT ---
        decimal extraCharge = 0, discount = 0;
        if (activeContract != null && activeContract.StartDate != null && activeContract.MoveInDate != null)
        {
            // Chỉ áp dụng với tháng đầu tiên
            if (now.Month == activeContract.MoveInDate.Month && now.Year == activeContract.MoveInDate.Year)
            {
                var moveInDay = activeContract.MoveInDate.Day;
                var signedDay = 10; // ngày ký hợp đồng mặc định là 10
                var pricePerDay = activeContract.PriceAgreed / 30;
                if (moveInDay < signedDay)
                    extraCharge = pricePerDay * (signedDay - moveInDay);
                else if (moveInDay > signedDay)
                    discount = pricePerDay * (moveInDay - signedDay);
            }
        }
        ViewBag.ExtraCharge = extraCharge;
        ViewBag.Discount = discount;

        // Lấy chỉ số nước tháng trước của hợp đồng hiện tại
        var service = new UtilityBillService(db);
        ViewBag.WaterPrev = service.GetHighestWaterIndexEnd(id);

        return View(room);
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
            // Auto tạo tên phòng nếu chưa có
            if (string.IsNullOrEmpty(room.Name))
            {
                room.Name = $"P{room.Area}{room.Floor}{room.RoomNumber}";
            }

            db.Rooms.Add(room);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        return View(room);
    }
    public ActionResult GenerateRooms()
    {
        var rooms = new List<Room>();
        foreach (var area in new[] { "A", "B" })
        {
            for (int floor = 0; floor <= 3; floor++)
            {
                for (int number = 1; number <= 8; number++)
                {
                    rooms.Add(new Room
                    {
                        Area = area,
                        Floor = floor,
                        RoomNumber = number,
                        Name = $"{area}{floor}.{number}",
                        DefaultPrice = 2000000,
                        HasAirCondition = true,
                        HasFridge = true,
                        IsOccupied = false
                    });
                }
            }
        }

        db.Rooms.AddRange(rooms);
        db.SaveChanges();
        return RedirectToAction("Index");
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

    // GET: Rooms/Delete/5
    public ActionResult Delete(int id)
    {
        var room = db.Rooms.Find(id);
        if (room == null) return HttpNotFound();

        return View(room);
    }

    // POST: Rooms/DeleteConfirmed/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmed(int id)
    {
        var room = db.Rooms.Find(id);
        if (room == null) return HttpNotFound();

        db.Rooms.Remove(room);
        db.SaveChanges();
        return RedirectToAction("Index");
    }

}
