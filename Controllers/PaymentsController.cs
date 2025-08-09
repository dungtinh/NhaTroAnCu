using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Globalization;
using System.Diagnostics.Contracts;

namespace NhaTroAnCu.Controllers
{
    public class PaymentsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // Helper method để parse date từ format dd/MM/yyyy
        private DateTime ParseDateFromString(string dateStr, DateTime defaultDate)
        {
            if (string.IsNullOrEmpty(dateStr))
                return defaultDate;

            try
            {
                // Thử parse với format dd/MM/yyyy
                DateTime result;
                if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                {
                    return result;
                }

                // Nếu không parse được, thử split manual
                var parts = dateStr.Split('/');
                if (parts.Length == 3)
                {
                    int day = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    int year = int.Parse(parts[2]);
                    return new DateTime(year, month, day);
                }
            }
            catch (Exception)
            {
                // Log error nếu cần
            }

            return defaultDate;
        }

        // GET: /Payments/Report
        public ActionResult Report(int? roomId, string fromDate, string toDate, int page = 1, int pageSize = 20)
        {
            // Xử lý ngày với helper method
            DateTime fromDateTime = ParseDateFromString(fromDate,
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));

            DateTime toDateTime = ParseDateFromString(toDate,
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
                    .AddMonths(1).AddDays(-1));

            // Đảm bảo toDate có giờ cuối ngày
            toDateTime = toDateTime.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            // Thống kê tổng quan TOÀN BỘ dữ liệu (không phụ thuộc vào điều kiện lọc)
            var allPayments = db.PaymentHistories.Include(p => p.Room);
            var overallStatistics = new PaymentStatistics
            {
                TotalRevenue = allPayments.Sum(p => (decimal?)p.TotalAmount) ?? 0,
                TotalPayments = allPayments.Count(),
                AveragePayment = allPayments.Any() ? allPayments.Average(p => p.TotalAmount) : 0,
                RoomCount = allPayments.Select(p => p.RoomId).Distinct().Count()
            };

            // Thống kê theo phòng cho TOÀN BỘ dữ liệu
            var overallRoomStatistics = allPayments
                .GroupBy(p => new { p.RoomId, p.Room.Name })
                .Select(g => new RoomPaymentStatistic
                {
                    RoomId = g.Key.RoomId,
                    RoomName = g.Key.Name,
                    PaymentCount = g.Count(),
                    TotalAmount = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(10) // Top 10 phòng
                .ToList();

            // Query cho dữ liệu được lọc
            var filteredQuery = db.PaymentHistories
                .Include(p => p.Room)
                .Include(p => p.Contract)
                .Include(p => p.Contract.ContractTenants.Select(ct => ct.Tenant))
                .Where(p => p.PaidDate >= fromDateTime && p.PaidDate <= toDateTime);

            // Lọc theo phòng nếu có
            if (roomId.HasValue)
            {
                filteredQuery = filteredQuery.Where(p => p.RoomId == roomId.Value);
            }

            // Thống kê cho dữ liệu được lọc
            var filteredTotalAmount = filteredQuery.Sum(p => (decimal?)p.TotalAmount) ?? 0;
            var filteredTotalCount = filteredQuery.Count();

            var filteredStatistics = new PaymentStatistics
            {
                TotalRevenue = filteredTotalAmount,
                TotalPayments = filteredTotalCount,
                AveragePayment = filteredTotalCount > 0 ? filteredTotalAmount / filteredTotalCount : 0,
                RoomCount = filteredQuery.Select(p => p.RoomId).Distinct().Count()
            };

            // Phân trang cho danh sách chi tiết
            var payments = filteredQuery
                .OrderByDescending(p => p.PaidDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Lấy danh sách phòng cho dropdown
            ViewBag.Rooms = new SelectList(db.Rooms.OrderBy(r => r.Name), "Id", "Name", roomId);

            // Tạo ViewModel
            var viewModel = new PaymentReportViewModel
            {
                Payments = payments,
                OverallStatistics = overallStatistics,  // Thống kê toàn bộ
                FilteredStatistics = filteredStatistics, // Thống kê theo điều kiện lọc
                OverallRoomStatistics = overallRoomStatistics, // Top phòng toàn bộ
                FilteredTotalAmount = filteredTotalAmount,
                FromDate = fromDateTime,
                ToDate = toDateTime,
                RoomId = roomId,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = filteredTotalCount,
                TotalPages = (int)Math.Ceiling((double)filteredTotalCount / pageSize)
            };

            return View(viewModel);
        }

        // Export to PDF
        public ActionResult ExportToPdf(int? roomId, string fromDate, string toDate)
        {
            // Xử lý ngày với helper method
            DateTime fromDateTime = ParseDateFromString(fromDate,
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));

            DateTime toDateTime = ParseDateFromString(toDate,
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
                    .AddMonths(1).AddDays(-1));

            toDateTime = toDateTime.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            var query = db.PaymentHistories
                .Include(p => p.Room)
                .Include(p => p.Contract)
                .Where(p => p.PaidDate >= fromDateTime && p.PaidDate <= toDateTime);

            if (roomId.HasValue)
            {
                query = query.Where(p => p.RoomId == roomId.Value);
            }

            var payments = query.OrderByDescending(p => p.PaidDate).ToList();

            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // Font Unicode
                string fontPath = Server.MapPath("~/fonts/times.ttf");
                BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bf, 16, Font.BOLD);
                Font headerFont = new Font(bf, 12, Font.BOLD);
                Font normalFont = new Font(bf, 10, Font.NORMAL);

                // Tiêu đề
                Paragraph title = new Paragraph($"BÁO CÁO THU CHI TỪ {fromDateTime:dd/MM/yyyy} ĐẾN {toDateTime:dd/MM/yyyy}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                document.Add(new Paragraph("\n"));

                // Thống kê tổng quan
                document.Add(new Paragraph($"Tổng doanh thu: {payments.Sum(p => p.TotalAmount):N0} VNĐ", headerFont));
                document.Add(new Paragraph($"Số lần thu: {payments.Count}", normalFont));
                document.Add(new Paragraph("\n"));

                // Bảng chi tiết
                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1f, 2f, 2f, 2f, 2f, 2f });

                // Header
                table.AddCell(new PdfPCell(new Phrase("STT", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Phòng", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Tháng/Năm", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Số tiền", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Ngày thu", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Ghi chú", headerFont)));

                // Data
                int stt = 1;
                foreach (var payment in payments)
                {
                    table.AddCell(new PdfPCell(new Phrase(stt.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.Room?.Name ?? "", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"{payment.Month}/{payment.Year}", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.TotalAmount.ToString("N0"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.PaidDate.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(payment.Note ?? "", normalFont)));
                    stt++;
                }

                document.Add(table);
                document.Close();

                byte[] bytes = ms.ToArray();
                return File(bytes, "application/pdf", $"BaoCaoThuChi_{fromDateTime:yyyyMMdd}_{toDateTime:yyyyMMdd}.pdf");
            }
        }
        [HttpPost]
        public ActionResult CollectAjax(int contractId, int roomId, int month, int year, decimal total, string note, decimal extraCharge, decimal discount, int waterCurrent, decimal waterMoney, decimal electricMoney)
        {
            try
            {
                // Kiểm tra xem contract có tồn tại và đang active không
                var contract = db.Contracts.FirstOrDefault(c => c.Id == contractId && c.Status == "Active");
                if (contract == null)
                {
                    return Json(new { success = false, message = "Hợp đồng không tồn tại hoặc đã kết thúc!" });
                }

                var payment = new PaymentHistory
                {
                    ContractId = contractId,
                    RoomId = roomId,
                    Month = month,
                    Year = year,
                    TotalAmount = total,
                    PaidDate = DateTime.Now,
                    Note = note,
                    CreatedAt = DateTime.Now
                };
                db.PaymentHistories.Add(payment);

                // Lưu hoặc cập nhật thông tin vào bảng UtilityBills
                var bill = db.UtilityBills.FirstOrDefault(b =>
                    b.RoomId == roomId &&
                    b.Month == month &&
                    b.Year == year &&
                    b.ContractId == contractId);

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
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    db.UtilityBills.Add(bill);
                }
                else
                {
                    bill.ExtraCharge = extraCharge;
                    bill.Discount = discount;
                    bill.Water = waterMoney;
                    bill.ElectricityAmount = electricMoney;
                    bill.UpdatedAt = DateTime.Now;
                }

                // Cập nhật chỉ số nước
                var waterIndex = db.WaterIndexes.FirstOrDefault(x =>
                    x.RoomId == roomId &&
                    x.Month == month &&
                    x.Year == year);

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
                    waterIndex.WaterReading = waterCurrent;
                    waterIndex.CreatedAt = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Thu tiền thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult CollectPayment(int utilityBillId, decimal amount, string note)
        {
            try
            {
                var bill = db.UtilityBills.Find(utilityBillId);
                if (bill == null)
                    return Json(new { success = false, message = "Không tìm thấy phiếu báo tiền!" });

                // Kiểm tra xem hợp đồng có còn hiệu lực không
                NhaTroAnCu.Models.Contract contract = null;
                if (bill.ContractId.HasValue)
                {
                    contract = db.Contracts.Find(bill.ContractId.Value);
                    if (contract == null || contract.Status != "Active")
                    {
                        return Json(new { success = false, message = "Hợp đồng không còn hiệu lực!" });
                    }
                }

                var payment = new PaymentHistory
                {
                    RoomId = bill.RoomId,
                    Month = bill.Month,
                    Year = bill.Year,
                    ContractId = bill.ContractId ?? 0,
                    UtilityBillId = utilityBillId,
                    TotalAmount = amount,
                    PaidDate = DateTime.Now,
                    Note = note,
                    CreatedAt = DateTime.Now
                };
                db.PaymentHistories.Add(payment);
                db.SaveChanges();

                var incomeCategory = db.IncomeExpenseCategories
                   .FirstOrDefault(c => c.Name == "Thu tiền phòng" && c.IsSystem);

                if (incomeCategory == null)
                {
                    // Create if not exists
                    incomeCategory = new IncomeExpenseCategory
                    {
                        Name = "Thu tiền phòng",
                        Type = "Income",
                        IsSystem = true,
                        CreatedAt = DateTime.Now
                    };
                    db.IncomeExpenseCategories.Add(incomeCategory);
                    db.SaveChanges();
                }
                var income = new IncomeExpense
                {
                    CategoryId = incomeCategory.Id,
                    Amount = amount,
                    Description = payment.Note ?? $"Thu tiền phòng {contract.Room.Name}",
                    TransactionDate = DateTime.Now,
                    RoomId = contract.RoomId,
                    ContractId = contract.Id,
                    CreatedBy = User.Identity.Name ?? "System",
                    CreatedAt = DateTime.Now
                };
                db.IncomeExpenses.Add(income);
                db.SaveChanges();

                return Json(new { success = true, message = "Ghi nhận thanh toán thành công!" });
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
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}