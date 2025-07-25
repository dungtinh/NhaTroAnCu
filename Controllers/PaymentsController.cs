using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class PaymentsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();


        // GET: /Payments/Report?month=7&year=2025
        public ActionResult Report(int month, int year)
        {
            var payments = db.PaymentHistories
                .Where(p => p.Month == month && p.Year == year)
                .Include(p => p.Room)
                .ToList();

            ViewBag.Total = payments.Sum(p => p.TotalAmount);
            return View(payments);
        }
        [HttpPost]
        public ActionResult CollectAjax(int contractId, int roomId, int month, int year, decimal total, string note, decimal extraCharge, decimal discount, int waterCurrent, decimal waterMoney, decimal electricMoney)
        {
            var payment = new PaymentHistory
            {
                ContractId = contractId,
                RoomId = roomId,
                Month = month,
                Year = year,
                TotalAmount = total,
                PaidDate = DateTime.Now,
                Note = note
            };
            db.PaymentHistories.Add(payment);

            // Lưu thông tin vào bảng UtilityBills nếu cần
            var bill = db.UtilityBills.FirstOrDefault(b => b.RoomId == roomId && b.Month == month && b.Year == year && b.ContractId == contractId);
            if (bill == null)
            {
                bill = new UtilityBill
                {
                    RoomId = roomId,
                    Month = month,
                    Year = year,
                    ContractId = contractId,
                    ExtraCharge = extraCharge,
                    Discount = discount,
                    Water = waterMoney,
                    ElectricityAmount = electricMoney,                    
                };
                db.UtilityBills.Add(bill);
            }
            else
            {
                bill.ExtraCharge = extraCharge;
                bill.Discount = discount;
                bill.Water = waterMoney;
                bill.ElectricityAmount = electricMoney;
            }

            var waterIndex = db.WaterIndexes.FirstOrDefault(x => x.RoomId == roomId && x.Month == month && x.Year == year);
            if (waterIndex == null)
            {
                waterIndex = new WaterIndex
                {
                    RoomId = roomId,
                    Month = month,
                    Year = year,
                    WaterReading = waterCurrent,
                    CreatedAt = DateTime.Now
                };
                db.WaterIndexes.Add(waterIndex);
            }
            else
            {
                waterIndex.WaterReading = waterCurrent; // cập nhật nếu đã có
                waterIndex.CreatedAt = DateTime.Now;
            }

            db.SaveChanges();
            return Json(new { success = true });
        }
        [HttpPost]
        public ActionResult CollectPayment(int utilityBillId, decimal amount, string note)
        {
            var bill = db.UtilityBills.Find(utilityBillId);
            if (bill == null) return HttpNotFound();

            var payment = new PaymentHistory
            {
                RoomId = bill.RoomId,
                Month = bill.Month,
                Year = bill.Year,
                ContractId = bill.ContractId ?? 0,
                UtilityBillId = utilityBillId,
                TotalAmount = amount,
                PaidDate = DateTime.Now,
                Note = note
            };
            db.PaymentHistories.Add(payment);
            db.SaveChanges();

            return Json(new { success = true });
        }

    }

}