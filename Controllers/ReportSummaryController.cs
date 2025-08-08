using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NhaTroAnCu.Controllers
{
    public class ReportSummaryController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /ReportSummary/Month?month=7&year=2025
        public PartialViewResult _MonthSummary(int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;
            DateTime filterDate = new DateTime(selectedYear, selectedMonth, DateTime.DaysInMonth(selectedYear, selectedMonth));

            int totalRooms = db.Rooms.Count();

            // Phòng đang có hợp đồng hiệu lực trong tháng
            var rentedRoomIds = db.Contracts
                .Where(c => c.Status == "Active"
                    && c.StartDate <= filterDate
                    && (c.EndDate == null || c.EndDate >= new DateTime(selectedYear, selectedMonth, 1)))
                .Select(c => c.RoomId)
                .Distinct()
                .ToList();

            int rentedRooms = rentedRoomIds.Count;
            int unrentedRooms = totalRooms - rentedRooms;

            // Lấy danh sách hợp đồng đang active trong tháng
            var activeContracts = db.Contracts
                .Where(c => c.Status == "Active"
                    && c.StartDate <= filterDate
                    && (c.EndDate == null || c.EndDate >= new DateTime(selectedYear, selectedMonth, 1)))
                .ToList();

            // Phòng đã thu tiền trong tháng (kiểm tra theo ContractId)
            var paidRoomIds = new List<int>();
            foreach (var contract in activeContracts)
            {
                var hasPaid = db.PaymentHistories
                    .Any(p => p.RoomId == contract.RoomId
                        && p.ContractId == contract.Id
                        && p.Month == selectedMonth
                        && p.Year == selectedYear);

                if (hasPaid)
                {
                    paidRoomIds.Add(contract.RoomId);
                }
            }

            int paidRooms = paidRoomIds.Distinct().Count();
            int unpaidRooms = rentedRooms - paidRooms;

            // Tổng tiền đã thu trong tháng
            decimal totalAmount = db.PaymentHistories
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear)
                .Sum(p => (decimal?)p.TotalAmount) ?? 0;

            double density = totalRooms == 0 ? 0 : (double)rentedRooms / totalRooms;

            // Tìm các phòng còn nợ tháng trước
            var overdueRoomIds = new List<int>();

            foreach (var contract in activeContracts)
            {
                // Kiểm tra xem hợp đồng này đã tồn tại từ tháng trước không
                if (contract.StartDate < new DateTime(selectedYear, selectedMonth, 1))
                {
                    // Lấy danh sách các tháng từ khi bắt đầu hợp đồng đến tháng trước tháng hiện tại
                    var start = contract.StartDate;
                    var end = new DateTime(selectedYear, selectedMonth, 1).AddMonths(-1);

                    var missingPayments = new List<DateTime>();

                    for (var date = start; date <= end; date = date.AddMonths(1))
                    {
                        var hasPayment = db.PaymentHistories.Any(p =>
                            p.RoomId == contract.RoomId &&
                            p.ContractId == contract.Id &&
                            p.Month == date.Month &&
                            p.Year == date.Year);

                        if (!hasPayment)
                        {
                            missingPayments.Add(date);
                        }
                    }

                    if (missingPayments.Any())
                    {
                        overdueRoomIds.Add(contract.RoomId);
                    }
                }
            }

            int overdueRooms = overdueRoomIds.Distinct().Count();

            var model = new ReportSummaryViewModel
            {
                Month = selectedMonth,
                Year = selectedYear,
                TotalRooms = totalRooms,
                RentedRooms = rentedRooms,
                UnrentedRooms = unrentedRooms,
                PaidRooms = paidRooms,
                UnpaidRooms = unpaidRooms,
                OverdueRooms = overdueRooms,
                TotalAmount = totalAmount,
                Density = density
            };

            return PartialView(model);
        }
    }
}