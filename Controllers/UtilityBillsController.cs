using NhaTroAnCu.Models;
using NhaTroAnCu.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
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

            // Tìm hợp đồng active cho phòng này thông qua ContractRooms
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .Where(c => c.Status == "Active" && c.ContractRooms.Any(cr => cr.RoomId == roomId))
                .FirstOrDefault();

            // Tìm bill theo ContractId
            var bill = contract != null ?
                db.UtilityBills.FirstOrDefault(b => b.ContractId == contract.Id && b.Month == month && b.Year == year) :
                null;

            UtilityBillCreateViewModel vm;
            if (bill != null)
            {
                // Map thủ công từng trường
                vm = new UtilityBillCreateViewModel
                {
                    RoomId = roomId,
                    Month = bill.Month,
                    Year = bill.Year,
                    ContractId = bill.ContractId,
                    WaterIndexStart = bill.WaterIndexStart,
                    WaterIndexEnd = bill.WaterIndexEnd,
                    ElectricityAmount = bill.ElectricityAmount,
                    WaterPrice = contract?.WaterPrice ?? 15000,
                    RentAmount = bill.RentAmount,
                    ExtraCharge = bill.ExtraCharge,
                    Discount = bill.Discount,
                    BillNote = bill.BillNote,
                    BillStatus = bill.BillStatus
                };
            }
            else
            {
                // Lấy chỉ số nước của tháng trước
                var service = new UtilityBillService(db);
                int waterPrev = service.GetHighestWaterIndexEndForContract(contract?.Id ?? 0);

                // Trường hợp chưa có bill, khởi tạo mặc định
                vm = new UtilityBillCreateViewModel
                {
                    RoomId = roomId,
                    Month = month,
                    Year = year,
                    ContractId = contract?.Id ?? 0,
                    WaterIndexStart = waterPrev,
                    WaterIndexEnd = waterPrev,
                    ElectricityAmount = 0,
                    WaterPrice = contract?.WaterPrice ?? 15000,
                    RentAmount = contract?.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId)?.PriceAgreed ?? room.DefaultPrice,
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
            // Tìm bill theo ContractId, Month, Year
            UtilityBill bill = db.UtilityBills.FirstOrDefault(b =>
                b.ContractId == vm.ContractId &&
                b.Month == vm.Month &&
                b.Year == vm.Year);

            if (bill == null)
            {
                bill = new UtilityBill();
                db.UtilityBills.Add(bill);
                bill.CreatedAt = DateTime.Now;
            }

            bill.Month = vm.Month;
            bill.Year = vm.Year;
            bill.ContractId = vm.ContractId;
            bill.WaterIndexStart = vm.WaterIndexStart;
            bill.WaterIndexEnd = vm.WaterIndexEnd;
            bill.ElectricityAmount = vm.ElectricityAmount;
            bill.WaterAmount = (vm.WaterIndexEnd - vm.WaterIndexStart) * vm.WaterPrice;
            bill.RentAmount = vm.RentAmount;
            bill.ExtraCharge = vm.ExtraCharge;
            bill.Discount = vm.Discount;
            bill.TotalAmount = bill.ElectricityAmount + bill.WaterAmount + bill.RentAmount + bill.ExtraCharge - bill.Discount;
            bill.BillNote = vm.BillNote;
            bill.BillStatus = "Final";
            bill.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            return RedirectToAction("Details", "Rooms", new { id = vm.RoomId });
        }

        public ActionResult CreateUtilityBillPartial(int roomId)
        {
            // Lấy các thông tin cần thiết
            var room = db.Rooms.Find(roomId);
            var now = DateTime.Now;

            // Tìm hợp đồng active cho phòng này thông qua ContractRooms
            var contract = db.Contracts
                .Include(c => c.ContractRooms)
                .Where(c => c.Status == "Active" && c.ContractRooms.Any(cr => cr.RoomId == roomId))
                .FirstOrDefault();

            int currentMonth = now.Month, currentYear = now.Year;

            var service = new UtilityBillService(db);
            int waterPrev = service.GetHighestWaterIndexEndForContract(contract?.Id ?? 0);

            var contractRoom = contract?.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId);

            var vm = new UtilityBillCreateViewModel
            {
                RoomId = roomId,
                ContractId = contract?.Id ?? 0,
                Month = currentMonth,
                Year = currentYear,
                WaterIndexStart = waterPrev,
                WaterPrice = contract?.WaterPrice ?? 15000,
                RentAmount = contractRoom?.PriceAgreed ?? room.DefaultPrice
            };

            return PartialView("_CreateOrEditUtilityBillPartial", vm);
        }

        [HttpPost]
        public ActionResult CreateOrUpdateAjax(UtilityBillCreateViewModel vm)
        {
            try
            {
                // Tìm phiếu báo tiền theo ContractId, Month, Year
                var bill = db.UtilityBills.FirstOrDefault(b =>
                    b.ContractId == vm.ContractId &&
                    b.Month == vm.Month &&
                    b.Year == vm.Year);

                if (bill == null)
                {
                    bill = new UtilityBill();
                    db.UtilityBills.Add(bill);
                    bill.CreatedAt = DateTime.Now;
                }

                bill.Month = vm.Month;
                bill.Year = vm.Year;
                bill.ContractId = vm.ContractId;
                bill.WaterIndexStart = vm.WaterIndexStart;
                bill.WaterIndexEnd = vm.WaterIndexEnd;
                bill.ElectricityAmount = vm.ElectricityAmount;
                bill.WaterAmount = (vm.WaterIndexEnd - vm.WaterIndexStart) * vm.WaterPrice;
                bill.RentAmount = vm.RentAmount;
                bill.ExtraCharge = vm.ExtraCharge;
                bill.Discount = vm.Discount;
                bill.TotalAmount = bill.ElectricityAmount + bill.WaterAmount + bill.RentAmount + bill.ExtraCharge - bill.Discount;
                bill.BillNote = vm.BillNote;
                bill.BillStatus = string.IsNullOrEmpty(vm.BillStatus) ? "Final" : vm.BillStatus;
                bill.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã lưu phiếu báo tiền thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
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