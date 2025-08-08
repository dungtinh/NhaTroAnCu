using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Xceed.Document.NET;

namespace NhaTroAnCu.Controllers
{
    public class UtilityBillsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /UtilityBills/CreateBill?roomId=5&month=7&year=2025
        public ActionResult CreateBill(int roomId, int month, int year)
        {
            var room = db.Rooms.Find(roomId);
            var contract = db.Contracts.FirstOrDefault(c => c.RoomId == roomId && c.Status == "Active");
            var bill = db.UtilityBills.FirstOrDefault(b => b.RoomId == roomId && b.Month == month && b.Year == year);

            UtilityBillCreateViewModel vm;
            if (bill != null)
            {
                // Map thủ công từng trường
                vm = new UtilityBillCreateViewModel
                {
                    RoomId = bill.RoomId,
                    Month = bill.Month,
                    Year = bill.Year,
                    ContractId = bill.ContractId ?? 0,
                    WaterIndexStart = bill.WaterIndexStart ?? 0,
                    WaterIndexEnd = bill.WaterIndexEnd ?? 0,
                    ElectricityAmount = bill.ElectricityAmount ?? 0,
                    WaterPrice = bill.WaterPrice ?? 15000, // hoặc giá mặc định nếu null
                    RentAmount = bill.RentAmount ?? 0,
                    ExtraCharge = bill.ExtraCharge ?? 0,
                    Discount = bill.Discount ?? 0,
                    BillNote = bill.BillNote,
                    BillStatus = bill.BillStatus
                };
            }
            else
            {
                // Trường hợp chưa có bill, khởi tạo mặc định
                vm = new UtilityBillCreateViewModel
                {
                    RoomId = roomId,
                    Month = month,
                    Year = year,
                    ContractId = contract?.Id ?? 0,
                    WaterIndexStart = 0,
                    WaterIndexEnd = 0,
                    ElectricityAmount = 0,
                    WaterPrice = 15000,
                    RentAmount = contract?.PriceAgreed ?? room.DefaultPrice,
                    ExtraCharge = 0,
                    Discount = 0,
                    BillNote = "",
                    BillStatus = "Draft"
                };
            }

            return View(vm);
        }

        [HttpPost]
        public ActionResult CreateBill(UtilityBillCreateViewModel vm)
        {
            UtilityBill bill = db.UtilityBills.FirstOrDefault(b => b.RoomId == vm.RoomId && b.Month == vm.Month && b.Year == vm.Year);

            if (bill == null)
            {
                bill = new UtilityBill();
                db.UtilityBills.Add(bill);
            }

            bill.RoomId = vm.RoomId;
            bill.Month = vm.Month;
            bill.Year = vm.Year;
            bill.ContractId = vm.ContractId;
            bill.WaterIndexStart = vm.WaterIndexStart;
            bill.WaterIndexEnd = vm.WaterIndexEnd;
            bill.ElectricityAmount = vm.ElectricityAmount;
            bill.Water = (vm.WaterIndexEnd - vm.WaterIndexStart) * vm.WaterPrice;
            bill.RentAmount = vm.RentAmount;
            bill.ExtraCharge = vm.ExtraCharge;
            bill.Discount = vm.Discount;
            bill.TotalAmount = bill.ElectricityAmount + bill.Water + bill.RentAmount + bill.ExtraCharge - bill.Discount;
            bill.BillNote = vm.BillNote;
            bill.BillStatus = "Final";

            db.SaveChanges();

            return RedirectToAction("Details", "Rooms", new { id = vm.RoomId });
        }
        public ActionResult CreateUtilityBillPartial(int roomId)
        {
            // Lấy các thông tin cần thiết
            var room = db.Rooms.Find(roomId);
            var now = DateTime.Now;
            var contract = db.Contracts.FirstOrDefault(c => c.RoomId == roomId && c.Status == "Active");
            int currentMonth = now.Month, currentYear = now.Year;

            var prevBill = db.UtilityBills
                .Where(b => b.RoomId == roomId && (
                    (b.Month == (currentMonth == 1 ? 12 : currentMonth - 1)) &&
                    (b.Year == (currentMonth == 1 ? currentYear - 1 : currentYear))
                ))
                .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month)
                .FirstOrDefault();

            var vm = new UtilityBillCreateViewModel
            {
                RoomId = roomId,
                ContractId = contract?.Id ?? 0,
                Month = currentMonth,
                Year = currentYear,
                WaterIndexStart = prevBill?.WaterIndexEnd ?? 0,
                WaterPrice = contract?.WaterPrice ?? 15000,
                RentAmount = contract?.PriceAgreed ?? room.DefaultPrice
            };

            return PartialView("_CreateOrEditUtilityBillPartial", vm);
        }
        [HttpPost]
        public ActionResult CreateOrUpdateAjax(UtilityBillCreateViewModel vm)
        {
            try
            {
                // Tìm phiếu báo tiền theo RoomId, Month, Year VÀ ContractId
                var bill = db.UtilityBills.FirstOrDefault(b =>
                    b.RoomId == vm.RoomId &&
                    b.Month == vm.Month &&
                    b.Year == vm.Year &&
                    b.ContractId == vm.ContractId);

                if (bill == null)
                {
                    bill = new UtilityBill();
                    db.UtilityBills.Add(bill);
                }

                bill.RoomId = vm.RoomId;
                bill.Month = vm.Month;
                bill.Year = vm.Year;
                bill.ContractId = vm.ContractId; // Đảm bảo lưu ContractId
                bill.WaterIndexStart = vm.WaterIndexStart;
                bill.WaterIndexEnd = vm.WaterIndexEnd;
                bill.ElectricityAmount = vm.ElectricityAmount;
                bill.WaterPrice = vm.WaterPrice;
                bill.Water = (vm.WaterIndexEnd - vm.WaterIndexStart) * vm.WaterPrice;
                bill.RentAmount = vm.RentAmount;
                bill.ExtraCharge = vm.ExtraCharge;
                bill.Discount = vm.Discount;
                bill.TotalAmount = bill.ElectricityAmount + bill.Water + bill.RentAmount + bill.ExtraCharge - bill.Discount;
                bill.BillNote = vm.BillNote;
                bill.BillStatus = string.IsNullOrEmpty(vm.BillStatus) ? "Final" : vm.BillStatus;
                bill.CreatedAt = bill.CreatedAt ?? DateTime.Now;
                bill.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã lưu phiếu báo tiền thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }

}