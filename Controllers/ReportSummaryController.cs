using NhaTroAnCu.Models;
using System;
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

            // Phòng đã thu tiền tháng
            var paidRoomIds = db.PaymentHistories
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear)
                .Select(p => p.RoomId)
                .Distinct()
                .ToList();

            int paidRooms = paidRoomIds.Count;
            int unpaidRooms = rentedRooms - paidRooms; // chỉ xét phòng đã thuê mà chưa thu tiền

            decimal totalAmount = db.PaymentHistories
                .Where(p => p.Month == selectedMonth && p.Year == selectedYear)
                .Sum(p => (decimal?)p.TotalAmount) ?? 0;

            double density = totalRooms == 0 ? 0 : (double)rentedRooms / totalRooms;
            // Tìm các phòng đang thuê từ trước tháng hiện tại
            var activeContractRoomIds = db.Contracts
                .Where(c => c.Status == "Active" &&
                    c.StartDate < new DateTime(selectedYear, selectedMonth, 1))
                .Select(c => c.RoomId)
                .Distinct()
                .ToList();

            // Tìm các phòng đó chưa thanh toán đủ các tháng trước tháng hiện tại
            var overdueRoomIds = activeContractRoomIds.Where(roomId =>
            {
                // Xác định hợp đồng đang hoạt động cho phòng này
                var contract = db.Contracts.FirstOrDefault(c => c.RoomId == roomId && c.Status == "Active" && c.StartDate < new DateTime(selectedYear, selectedMonth, 1));
                if (contract == null) return false;

                // Danh sách các tháng chưa thanh toán từ StartDate đến tháng hiện tại - 1
                var start = contract.StartDate;
                var end = new DateTime(selectedYear, selectedMonth, 1).AddMonths(-1);

                var missingPayments = Enumerable.Range(0, ((end.Year - start.Year) * 12 + end.Month - start.Month) + 1)
                    .Select(offset => start.AddMonths(offset))
                    .Where(date =>
                        !db.PaymentHistories.Any(p =>
                            p.RoomId == roomId &&
                            p.Month == date.Month &&
                            p.Year == date.Year))
                    .ToList();

                return missingPayments.Any();
            }).ToList();

            int overdueRooms = overdueRoomIds.Count;

            var model = new ReportSummaryViewModel
            {
                Month = selectedMonth,
                Year = selectedYear,
                TotalRooms = totalRooms,
                RentedRooms = rentedRooms,
                UnrentedRooms = unrentedRooms,
                PaidRooms = paidRooms,
                UnpaidRooms = unpaidRooms,
                OverdueRooms = overdueRooms, // Thêm dòng này
                TotalAmount = totalAmount,
                Density = density
            };

            return PartialView(model);
        }
    }
}