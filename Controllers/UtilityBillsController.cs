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
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using iTextSharp.text.pdf.draw;

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
                int waterPrev = service.GetHighestWaterIndexEnd(contract?.Id ?? 0);

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

                // TỰ ĐỘNG TÍNH TIỀN TĂNG/GIẢM CHO THÁNG ĐẦU
                CalculateFirstMonthAdjustment(vm, contract, roomId, month, year);
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

            int currentMonth = now.Month;
            int currentYear = now.Year;

            UtilityBillCreateViewModel vm;

            // QUAN TRỌNG: Kiểm tra xem đã có phiếu báo cho tháng này chưa
            if (contract != null)
            {
                var existingBill = db.UtilityBills.FirstOrDefault(b =>
                    b.ContractId == contract.Id &&
                    b.Month == currentMonth &&
                    b.Year == currentYear);

                if (existingBill != null)
                {
                    // NẾU ĐÃ CÓ PHIẾU BÁO -> Load dữ liệu cũ để sửa
                    vm = new UtilityBillCreateViewModel
                    {
                        RoomId = roomId,
                        ContractId = contract.Id,
                        Month = existingBill.Month,
                        Year = existingBill.Year,
                        WaterIndexStart = existingBill.WaterIndexStart,
                        WaterIndexEnd = existingBill.WaterIndexEnd,
                        ElectricityAmount = existingBill.ElectricityAmount,
                        WaterPrice = contract.WaterPrice,
                        RentAmount = existingBill.RentAmount,
                        ExtraCharge = existingBill.ExtraCharge,
                        Discount = existingBill.Discount,
                        BillNote = existingBill.BillNote,
                        TotalAmount = existingBill.TotalAmount
                    };

                    ViewBag.IsEdit = true;
                    ViewBag.BillId = existingBill.Id;
                }
                else
                {
                    // CHƯA CÓ PHIẾU BÁO -> Tạo mới
                    var service = new UtilityBillService(db);
                    int waterPrev = service.GetHighestWaterIndexEnd(contract.Id);
                    var contractRoom = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId && cr.ContractId == contract.Id);

                    vm = new UtilityBillCreateViewModel
                    {
                        RoomId = roomId,
                        ContractId = contract.Id,
                        Month = currentMonth,
                        Year = currentYear,
                        WaterIndexStart = waterPrev,
                        WaterIndexEnd = waterPrev,
                        ElectricityAmount = 0,
                        WaterPrice = contract.WaterPrice,
                        RentAmount = contractRoom?.PriceAgreed ?? room.DefaultPrice,
                        ExtraCharge = 0,
                        Discount = 0,
                        BillNote = "",
                        TotalAmount = 0
                    };

                    // Tính toán tiền tăng/giảm cho tháng đầu (nếu có)
                    if (currentMonth == contract.MoveInDate.Month && currentYear == contract.MoveInDate.Year)
                    {
                        CalculateFirstMonthAdjustment(vm, contract, roomId, currentMonth, currentYear);
                    }

                    ViewBag.IsEdit = false;
                }
            }
            else
            {
                // Không có hợp đồng active
                vm = new UtilityBillCreateViewModel
                {
                    RoomId = roomId,
                    ContractId = 0,
                    Month = currentMonth,
                    Year = currentYear,
                    WaterIndexStart = 0,
                    WaterIndexEnd = 0,
                    ElectricityAmount = 0,
                    WaterPrice = 15000,
                    RentAmount = room.DefaultPrice,
                    ExtraCharge = 0,
                    Discount = 0,
                    BillNote = "",
                    TotalAmount = 0
                };

                ViewBag.IsEdit = false;
            }

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

                bool isNewBill = (bill == null);

                if (isNewBill)
                {
                    bill = new UtilityBill();
                    db.UtilityBills.Add(bill);
                    bill.CreatedAt = DateTime.Now;

                    // Nếu là phiếu mới và là tháng đầu, tính toán lại để đảm bảo chính xác
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .FirstOrDefault(c => c.Id == vm.ContractId);

                    if (contract != null &&
                        vm.Month == contract.MoveInDate.Month &&
                        vm.Year == contract.MoveInDate.Year)
                    {
                        // Tính toán lại tiền tăng/giảm nếu user chưa nhập
                        if (vm.ExtraCharge == 0 && vm.Discount == 0)
                        {
                            CalculateFirstMonthAdjustment(vm, contract, vm.RoomId, vm.Month, vm.Year);
                        }
                    }
                }

                bill.Month = vm.Month;
                bill.Year = vm.Year;
                bill.RoomId = vm.RoomId;
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

                string message = isNewBill ?
                    "Đã tạo phiếu báo tiền thành công!" :
                    "Đã cập nhật phiếu báo tiền thành công!";

                return Json(new { success = true, message = message });
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
        private void CalculateFirstMonthAdjustment(UtilityBillCreateViewModel vm, Contract contract, int roomId, int month, int year)
        {
            if (contract == null) return;

            // Kiểm tra xem có phải tháng đầu tiên không
            if (month == contract.MoveInDate.Month && year == contract.MoveInDate.Year)
            {
                var contractRoom = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId);
                if (contractRoom == null) return;

                var moveInDay = contract.MoveInDate.Day;
                var startDay = contract.StartDate.Day;
                var pricePerDay = contractRoom.PriceAgreed / 30;

                // Logic tính toán theo ngày chuyển vào
                if (startDay < moveInDay)
                {
                    // Chuyển vào trước ngày 10 - tính tiền cộng thêm
                    vm.ExtraCharge = pricePerDay * (moveInDay - moveInDay);
                    vm.Discount = 0;
                    vm.BillNote = $"Phụ thu {moveInDay - startDay} ngày (chuyển vào ngày {moveInDay:00}/{month:00})";
                }
                else if (startDay > moveInDay)
                {
                    // Chuyển vào sau ngày 10 - tính tiền giảm trừ
                    vm.ExtraCharge = 0;
                    vm.Discount = pricePerDay * (startDay - moveInDay);
                    vm.BillNote = $"Giảm trừ {startDay - moveInDay} ngày (chuyển vào ngày {moveInDay:00}/{month:00})";
                }
                else
                {
                    // Chuyển vào đúng ngày 10 - không tăng không giảm
                    vm.ExtraCharge = 0;
                    vm.Discount = 0;
                    vm.BillNote = $"Chuyển vào ngày {moveInDay:00}/{month:00}";
                }
            }
        }
        public ActionResult Export(int id)
        {
            try
            {
                // Lấy thông tin phiếu báo tiền
                var bill = db.UtilityBills
                    .Include(b => b.Contract)
                    .Include(b => b.Contract.ContractTenants.Select(ct => ct.Tenant))
                    .Include(b => b.Contract.Company)
                    .Include(b => b.Room)
                    .FirstOrDefault(b => b.Id == id);

                if (bill == null)
                {
                    return HttpNotFound("Không tìm thấy phiếu báo tiền");
                }

                // Tạo document PDF - Sử dụng namespace đầy đủ
                iTextSharp.text.Document document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 60, 60);
                MemoryStream memoryStream = new MemoryStream();
                iTextSharp.text.pdf.PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, memoryStream);

                // Thêm metadata
                document.AddAuthor("Nhà Trọ An Cư");
                document.AddCreator("Hệ thống quản lý nhà trọ");
                document.AddSubject($"Phiếu báo tiền tháng {bill.Month}/{bill.Year}");
                document.AddTitle($"Phiếu báo tiền - Phòng {bill.Room.Name}");

                document.Open();

                // Font tiếng Việt
                string fontPath = Server.MapPath("~/Fonts/times.ttf");
                iTextSharp.text.pdf.BaseFont baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);
                iTextSharp.text.Font titleFont = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.BLACK);
                iTextSharp.text.Font headerFont = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.BLACK);
                iTextSharp.text.Font normalFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                iTextSharp.text.Font boldFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.BLACK);
                iTextSharp.text.Font smallFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);

                // HEADER - Tiêu đề
                iTextSharp.text.Paragraph title = new iTextSharp.text.Paragraph("PHIẾU BÁO TIỀN THUÊ PHÒNG", titleFont);
                title.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                title.SpacingAfter = 10f;
                document.Add(title);

                iTextSharp.text.Paragraph monthYear = new iTextSharp.text.Paragraph($"Tháng {bill.Month}/{bill.Year}", headerFont);
                monthYear.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                monthYear.SpacingAfter = 20f;
                document.Add(monthYear);

                // Thông tin cơ bản
                iTextSharp.text.pdf.PdfPTable infoTable = new iTextSharp.text.pdf.PdfPTable(2);
                infoTable.WidthPercentage = 100;
                infoTable.SetWidths(new float[] { 1, 1 });
                infoTable.SpacingBefore = 10f;
                infoTable.SpacingAfter = 20f;

                // Cột trái
                iTextSharp.text.pdf.PdfPCell leftCell = new iTextSharp.text.pdf.PdfPCell();
                leftCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
                leftCell.AddElement(new iTextSharp.text.Paragraph($"Phòng: {bill.Room.Name}", boldFont));
                leftCell.AddElement(new iTextSharp.text.Paragraph($"Mã hợp đồng: #{bill.ContractId}", normalFont));

                // Lấy tên khách thuê
                string tenantName = "";
                if (bill.Contract.ContractType == "Company" && bill.Contract.Company != null)
                {
                    tenantName = bill.Contract.Company.CompanyName;
                }
                else
                {
                    var tenant = bill.Contract.ContractTenants.FirstOrDefault()?.Tenant;
                    if (tenant != null)
                    {
                        tenantName = tenant.FullName;
                    }
                }
                leftCell.AddElement(new iTextSharp.text.Paragraph($"Khách thuê: {tenantName}", normalFont));

                // Cột phải
                iTextSharp.text.pdf.PdfPCell rightCell = new iTextSharp.text.pdf.PdfPCell();
                rightCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
                rightCell.AddElement(new iTextSharp.text.Paragraph($"Ngày lập: {DateTime.Now:dd/MM/yyyy}", normalFont));
                rightCell.AddElement(new iTextSharp.text.Paragraph($"Mã phiếu: #PBT{bill.Id:D6}", normalFont));
                rightCell.AddElement(new iTextSharp.text.Paragraph($"Trạng thái: {(bill.BillStatus == "Final" ? "Chính thức" : "Nháp")}", normalFont));

                infoTable.AddCell(leftCell);
                infoTable.AddCell(rightCell);
                document.Add(infoTable);

                // Đường kẻ ngang
                iTextSharp.text.pdf.draw.LineSeparator line = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.GRAY, iTextSharp.text.Element.ALIGN_CENTER, -2);
                document.Add(new iTextSharp.text.Chunk(line));
                document.Add(new iTextSharp.text.Paragraph(" "));

                // BẢNG CHI TIẾT
                iTextSharp.text.pdf.PdfPTable detailTable = new iTextSharp.text.pdf.PdfPTable(4);
                detailTable.WidthPercentage = 100;
                detailTable.SetWidths(new float[] { 1.5f, 2f, 1.5f, 1.5f });
                detailTable.SpacingBefore = 10f;

                // Header bảng
                AddTableHeader(detailTable, "STT", headerFont);
                AddTableHeader(detailTable, "Khoản mục", headerFont);
                AddTableHeader(detailTable, "Chi tiết", headerFont);
                AddTableHeader(detailTable, "Thành tiền", headerFont);

                int stt = 1;

                // 1. Tiền phòng
                AddTableCell(detailTable, stt++.ToString(), normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, "Tiền thuê phòng", normalFont, iTextSharp.text.Element.ALIGN_LEFT);
                AddTableCell(detailTable, $"Tháng {bill.Month}/{bill.Year}", normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, FormatCurrency(bill.RentAmount), normalFont, iTextSharp.text.Element.ALIGN_RIGHT);

                // 2. Tiền điện
                AddTableCell(detailTable, stt++.ToString(), normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, "Tiền điện", normalFont, iTextSharp.text.Element.ALIGN_LEFT);
                AddTableCell(detailTable, $"{bill.Contract.ElectricityPrice:N0}đ/kWh", normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, FormatCurrency(bill.ElectricityAmount), normalFont, iTextSharp.text.Element.ALIGN_RIGHT);

                // 3. Tiền nước
                int waterUsed = bill.WaterIndexEnd - bill.WaterIndexStart;
                AddTableCell(detailTable, stt++.ToString(), normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, "Tiền nước", normalFont, iTextSharp.text.Element.ALIGN_LEFT);
                AddTableCell(detailTable, $"{waterUsed}m³ x {bill.Contract.WaterPrice:N0}đ", normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                AddTableCell(detailTable, FormatCurrency(bill.WaterAmount), normalFont, iTextSharp.text.Element.ALIGN_RIGHT);

                // 4. Phụ thu (nếu có)
                if (bill.ExtraCharge > 0)
                {
                    AddTableCell(detailTable, stt++.ToString(), normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                    AddTableCell(detailTable, "Phụ thu", normalFont, iTextSharp.text.Element.ALIGN_LEFT);
                    AddTableCell(detailTable, "", normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                    AddTableCell(detailTable, FormatCurrency(bill.ExtraCharge), normalFont, iTextSharp.text.Element.ALIGN_RIGHT);
                }

                // 5. Giảm trừ (nếu có)
                if (bill.Discount > 0)
                {
                    AddTableCell(detailTable, stt++.ToString(), normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                    AddTableCell(detailTable, "Giảm trừ", normalFont, iTextSharp.text.Element.ALIGN_LEFT);
                    AddTableCell(detailTable, "", normalFont, iTextSharp.text.Element.ALIGN_CENTER);
                    AddTableCell(detailTable, "-" + FormatCurrency(bill.Discount), normalFont, iTextSharp.text.Element.ALIGN_RIGHT);
                }

                // Dòng tổng cộng
                iTextSharp.text.pdf.PdfPCell totalLabelCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase("TỔNG CỘNG", boldFont));
                totalLabelCell.Colspan = 3;
                totalLabelCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                totalLabelCell.Padding = 8;
                totalLabelCell.BackgroundColor = new iTextSharp.text.BaseColor(240, 240, 240);
                detailTable.AddCell(totalLabelCell);

                iTextSharp.text.pdf.PdfPCell totalAmountCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(FormatCurrency(bill.TotalAmount), boldFont));
                totalAmountCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                totalAmountCell.Padding = 8;
                totalAmountCell.BackgroundColor = new iTextSharp.text.BaseColor(240, 240, 240);
                detailTable.AddCell(totalAmountCell);

                document.Add(detailTable);

                // Chỉ số nước
                document.Add(new iTextSharp.text.Paragraph(" "));
                iTextSharp.text.Paragraph waterIndex = new iTextSharp.text.Paragraph($"Chỉ số nước: Đầu kỳ: {bill.WaterIndexStart} - Cuối kỳ: {bill.WaterIndexEnd} - Tiêu thụ: {waterUsed}m³", normalFont);
                waterIndex.SpacingAfter = 10f;
                document.Add(waterIndex);

                // Ghi chú (nếu có)
                if (!string.IsNullOrEmpty(bill.BillNote))
                {
                    iTextSharp.text.Paragraph noteTitle = new iTextSharp.text.Paragraph("Ghi chú:", boldFont);
                    noteTitle.SpacingAfter = 5f;
                    document.Add(noteTitle);

                    iTextSharp.text.Paragraph noteContent = new iTextSharp.text.Paragraph(bill.BillNote, normalFont);
                    noteContent.SpacingAfter = 20f;
                    document.Add(noteContent);
                }

                // Số tiền bằng chữ
                document.Add(new iTextSharp.text.Paragraph(" "));
                string amountInWords = NumberToText(bill.TotalAmount);
                iTextSharp.text.Paragraph amountText = new iTextSharp.text.Paragraph($"Số tiền bằng chữ: {amountInWords}", boldFont);
                amountText.SpacingAfter = 30f;
                document.Add(amountText);

                // Chữ ký
                iTextSharp.text.pdf.PdfPTable signTable = new iTextSharp.text.pdf.PdfPTable(2);
                signTable.WidthPercentage = 100;
                signTable.SetWidths(new float[] { 1, 1 });
                signTable.SpacingBefore = 30f;

                iTextSharp.text.pdf.PdfPCell tenantSignCell = new iTextSharp.text.pdf.PdfPCell();
                tenantSignCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
                tenantSignCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                tenantSignCell.AddElement(new iTextSharp.text.Paragraph("Người thuê", boldFont));
                tenantSignCell.AddElement(new iTextSharp.text.Paragraph("(Ký và ghi rõ họ tên)", smallFont));
                tenantSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));
                tenantSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));
                tenantSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));

                iTextSharp.text.pdf.PdfPCell landlordSignCell = new iTextSharp.text.pdf.PdfPCell();
                landlordSignCell.Border = iTextSharp.text.Rectangle.NO_BORDER;
                landlordSignCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                landlordSignCell.AddElement(new iTextSharp.text.Paragraph("Người cho thuê", boldFont));
                landlordSignCell.AddElement(new iTextSharp.text.Paragraph("(Ký và ghi rõ họ tên)", smallFont));
                landlordSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));
                landlordSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));
                landlordSignCell.AddElement(new iTextSharp.text.Paragraph(" ", normalFont));

                signTable.AddCell(tenantSignCell);
                signTable.AddCell(landlordSignCell);
                document.Add(signTable);

                // Footer
                document.Add(new iTextSharp.text.Paragraph(" "));
                document.Add(new iTextSharp.text.Chunk(line));
                iTextSharp.text.Paragraph footer = new iTextSharp.text.Paragraph("NHÀ TRỌ AN CƯ - Địa chỉ: [Địa chỉ nhà trọ] - SĐT: [Số điện thoại]", smallFont);
                footer.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                footer.SpacingBefore = 10f;
                document.Add(footer);

                document.Close();

                // Trả về file PDF
                byte[] bytes = memoryStream.ToArray();
                memoryStream.Close();

                return File(bytes, "application/pdf", $"PhieuBaoTien_Phong{bill.Room.Name}_T{bill.Month}.{bill.Year}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xuất PDF: " + ex.Message;
                return RedirectToAction("Details", "Rooms", new { id = id });
            }
        }

        // Helper methods - Sử dụng namespace đầy đủ
        private void AddTableHeader(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font)
        {
            iTextSharp.text.pdf.PdfPCell cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.BackgroundColor = new iTextSharp.text.BaseColor(52, 152, 219);
            cell.Padding = 8;
            font.Color = iTextSharp.text.BaseColor.WHITE;
            table.AddCell(cell);
            font.Color = iTextSharp.text.BaseColor.BLACK;
        }

        private void AddTableCell(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font, int alignment)
        {
            iTextSharp.text.pdf.PdfPCell cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.Padding = 6;
            table.AddCell(cell);
        }

        private string FormatCurrency(decimal amount)
        {
            return string.Format("{0:N0}đ", amount);
        }

        // Hàm chuyển số thành chữ
        private string NumberToText(decimal number)
        {
            if (number == 0) return "Không đồng";

            string[] ones = { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string[] tens = { "", "mười", "hai mươi", "ba mươi", "bốn mươi", "năm mươi",
                     "sáu mươi", "bảy mươi", "tám mươi", "chín mươi" };

            long intNumber = (long)number;
            string result = "";

            // Tỷ
            if (intNumber >= 1000000000)
            {
                long billions = intNumber / 1000000000;
                result += ConvertHundreds(billions) + " tỷ ";
                intNumber %= 1000000000;
            }

            // Triệu
            if (intNumber >= 1000000)
            {
                long millions = intNumber / 1000000;
                result += ConvertHundreds(millions) + " triệu ";
                intNumber %= 1000000;
            }

            // Nghìn
            if (intNumber >= 1000)
            {
                long thousands = intNumber / 1000;
                result += ConvertHundreds(thousands) + " nghìn ";
                intNumber %= 1000;
            }

            // Trăm
            if (intNumber > 0)
            {
                result += ConvertHundreds(intNumber);
            }

            result = result.Trim() + " đồng";
            // Viết hoa chữ cái đầu
            return char.ToUpper(result[0]) + result.Substring(1);
        }

        private string ConvertHundreds(long number)
        {
            string[] ones = { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string result = "";

            // Trăm
            if (number >= 100)
            {
                result += ones[number / 100] + " trăm ";
                number %= 100;
            }

            // Chục và đơn vị
            if (number >= 10 && number <= 19)
            {
                result += "mười ";
                if (number > 10)
                {
                    result += ones[number - 10] + " ";
                }
            }
            else if (number >= 20)
            {
                result += ones[number / 10] + " mươi ";
                if (number % 10 > 0)
                {
                    if (number % 10 == 1)
                        result += "mốt ";
                    else
                        result += ones[number % 10] + " ";
                }
            }
            else if (number > 0)
            {
                result += ones[number] + " ";
            }

            return result.Trim();
        }

        // Action Delete phiếu báo tiền
        [HttpPost]
        public ActionResult Delete(int id)
        {
            try
            {
                var bill = db.UtilityBills.Find(id);
                if (bill == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiếu báo tiền" });
                }

                var roomId = bill.RoomId;
                db.UtilityBills.Remove(bill);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa phiếu báo tiền thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}