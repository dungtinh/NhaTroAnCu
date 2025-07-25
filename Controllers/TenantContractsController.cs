using NhaTroAnCu.Models;
using NhaTroAnCu.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Web.Hosting;
using System.Globalization;
using iTextSharp.text.pdf.draw;

namespace NhaTroAnCu.Controllers
{
    public class TenantContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        public ActionResult Index(
            int page = 1,
            int pageSize = 50,
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "ContractSignedDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            t.FullName,
                            t.IdentityCard,
                            t.Gender,
                            t.PermanentAddress,
                            t.PhoneNumber,
                            t.Photo, // Thêm trường ảnh
                            RoomName = r.Name,
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            int totalItems = query.Count();

            // Lấy dữ liệu và thêm số thứ tự
            var items = query.Skip((page - 1) * pageSize).Take(pageSize)
                .ToList()
                .Select((x, index) => new TenantContractListItemViewModel
                {
                    OrderNumber = (page - 1) * pageSize + index + 1, // Thêm số thứ tự
                    FullName = x.FullName,
                    IdentityCard = x.IdentityCard,
                    Gender = x.Gender,
                    PermanentAddress = x.PermanentAddress,
                    PhoneNumber = x.PhoneNumber,
                    Photo = x.Photo,
                    RoomName = x.RoomName,
                    ContractSignedDate = x.ContractSignedDate,
                    MoveInDate = x.MoveInDate
                }).ToList();

            var model = new TenantContractListViewModel
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                SearchName = searchName,
                SearchCard = searchCard,
                SearchAddress = searchAddress,
                SortField = sortField,
                SortDirection = sortDirection,
                FromDate = from,
                ToDate = to
            };

            return View(model);
        }

        public ActionResult ExportToPdf(
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "ContractSignedDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            t.FullName,
                            t.IdentityCard,
                            t.Gender,
                            t.PermanentAddress,
                            t.PhoneNumber,
                            t.Photo, // Thêm trường ảnh
                            RoomName = r.Name,
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            var items = query.ToList();

            string fontPath = HostingEnvironment.MapPath("~/Fonts/times.ttf");
            BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

            // Định nghĩa fonts với kích thước và style
            var mainTitleFont = new Font(baseFont, 24, Font.BOLD, new BaseColor(0, 51, 102)); // Xanh đậm
            var titleFont = new Font(baseFont, 18, Font.BOLD, new BaseColor(0, 51, 102));
            var subtitleFont = new Font(baseFont, 14, Font.NORMAL, BaseColor.GRAY);
            var headerFont = new Font(baseFont, 11, Font.BOLD, BaseColor.WHITE);
            var rowFont = new Font(baseFont, 10, Font.NORMAL);
            var footerFont = new Font(baseFont, 10, Font.ITALIC, BaseColor.GRAY);

            // Fonts cho trang chi tiết
            var detailTitleFont = new Font(baseFont, 20, Font.BOLD, new BaseColor(0, 51, 102));
            var detailLabelFont = new Font(baseFont, 12, Font.BOLD, new BaseColor(70, 70, 70));
            var detailValueFont = new Font(baseFont, 12, Font.NORMAL);
            var detailHeaderFont = new Font(baseFont, 14, Font.BOLD, BaseColor.WHITE);

            using (var stream = new MemoryStream())
            {
                Document pdfDoc = new Document(PageSize.A4, 20, 10, 56, 45);
                PdfWriter writer = PdfWriter.GetInstance(pdfDoc, stream);

                // Thêm event để vẽ header và footer
                writer.PageEvent = new CustomPdfPageEventHelper();

                pdfDoc.Open();

                // TRANG BÌA
                pdfDoc.Add(new Paragraph(" "));
                pdfDoc.Add(new Paragraph(" "));

                // Logo hoặc tên nhà trọ
                var mainTitle = new Paragraph("NHÀ TRỌ AN CƯ", mainTitleFont);
                mainTitle.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(mainTitle);

                // Đường kẻ ngang
                var line = new LineSeparator(2f, 80f, new BaseColor(0, 51, 102), Element.ALIGN_CENTER, -2);
                pdfDoc.Add(new Chunk(line));
                pdfDoc.Add(new Paragraph(" "));

                // Tiêu đề báo cáo
                var reportTitle = new Paragraph("DANH SÁCH NGƯỜI THUÊ PHÒNG", titleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(reportTitle);

                // Thông tin ngày xuất
                var exportDate = new Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}", subtitleFont);
                exportDate.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(exportDate);

                pdfDoc.Add(new Paragraph(" "));
                pdfDoc.Add(new Paragraph(" "));

                // PHẦN 1: BẢNG DANH SÁCH
                PdfPTable table = new PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1f, 2.5f, 2.1f, 0.9f, 3f, 1.9f, 1.4f, 1.7f, 1.7f });
                table.SpacingBefore = 15f;
                table.SpacingAfter = 20f;

                // Header của bảng
                string[] headers = { "TT", "Họ tên", "Số thẻ", "GT", "Địa chỉ", "SĐT", "Phòng", "Ngày ký", "Ngày vào" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = new BaseColor(0, 51, 102); // Xanh đậm
                    cell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
                    cell.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;
                    cell.Padding = 8;
                    cell.BorderColor = BaseColor.WHITE;
                    table.AddCell(cell);
                }

                // Dữ liệu bảng
                int orderNumber = 1;
                bool isAlternateRow = false;
                foreach (var item in items)
                {
                    var bgColor = isAlternateRow ? new BaseColor(240, 240, 240) : BaseColor.WHITE;

                    // STT
                    var sttCell = new PdfPCell(new Phrase(orderNumber.ToString(), rowFont))
                    {
                        HorizontalAlignment = PdfPCell.ALIGN_CENTER,
                        BackgroundColor = bgColor,
                        Padding = 5,
                        BorderColor = new BaseColor(200, 200, 200)
                    };
                    table.AddCell(sttCell);

                    // Các cột khác
                    AddTableCell(table, item.FullName, rowFont, bgColor);
                    AddTableCell(table, item.IdentityCard, rowFont, bgColor);
                    AddTableCell(table, item.Gender, rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.PermanentAddress, rowFont, bgColor);
                    AddTableCell(table, item.PhoneNumber, rowFont, bgColor);
                    AddTableCell(table, item.RoomName, rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.ContractSignedDate.ToString("dd/MM/yyyy"), rowFont, bgColor, PdfPCell.ALIGN_CENTER);
                    AddTableCell(table, item.MoveInDate.ToString("dd/MM/yyyy"), rowFont, bgColor, PdfPCell.ALIGN_CENTER);

                    orderNumber++;
                    isAlternateRow = !isAlternateRow;
                }

                pdfDoc.Add(table);

                // Footer của trang danh sách
                pdfDoc.Add(new Paragraph(" "));
                var footerLine = new LineSeparator(1f, 100f, BaseColor.GRAY, Element.ALIGN_CENTER, -2);
                pdfDoc.Add(new Chunk(footerLine));

                var footerInfo = new Paragraph();
                footerInfo.Add(new Chunk("ĐỊA CHỈ: ", detailLabelFont));
                footerInfo.Add(new Chunk("Thôn Đình Ngọ (Ấp), Xã Hồng Phong, Huyện An Dương, Hải Phòng\n", detailValueFont));
                footerInfo.Add(new Chunk("CHỦ NHÀ TRỌ: ", detailLabelFont));
                footerInfo.Add(new Chunk("Tạ Ngọc Duy - CCCD: 034082002422\n", detailValueFont));
                footerInfo.Add(new Chunk("ĐIỆN THOẠI: ", detailLabelFont));
                footerInfo.Add(new Chunk("0975092833", detailValueFont));
                footerInfo.Alignment = Element.ALIGN_CENTER;
                pdfDoc.Add(footerInfo);

                // PHẦN 2: CHI TIẾT TỪNG NGƯỜI
                int tenantIndex = 1;
                foreach (var item in items)
                {
                    pdfDoc.NewPage();

                    // Header của trang chi tiết
                    var headerTable = new PdfPTable(1);
                    headerTable.WidthPercentage = 100;
                    var headerCell = new PdfPCell();
                    headerCell.BackgroundColor = new BaseColor(0, 51, 102);
                    headerCell.Border = Rectangle.NO_BORDER;
                    headerCell.Padding = 15;

                    var headerPara = new Paragraph("THÔNG TIN CHI TIẾT NGƯỜI THUÊ", new Font(baseFont, 18, Font.BOLD, BaseColor.WHITE));
                    headerPara.Alignment = Element.ALIGN_CENTER;
                    headerCell.AddElement(headerPara);

                    var tenantNumber = new Paragraph($"Người thuê số: {tenantIndex}/{items.Count}", new Font(baseFont, 12, Font.NORMAL, BaseColor.WHITE));
                    tenantNumber.Alignment = Element.ALIGN_CENTER;
                    headerCell.AddElement(tenantNumber);

                    headerTable.AddCell(headerCell);
                    pdfDoc.Add(headerTable);

                    pdfDoc.Add(new Paragraph(" "));

                    // Tạo bảng 2 cột để layout ảnh và thông tin
                    PdfPTable contentTable = new PdfPTable(1);
                    contentTable.WidthPercentage = 100;
                    //contentTable.SetWidths(new float[] { 65f, 35f });
                    contentTable.SpacingBefore = 20f;

                    // Cột trái - Ảnh
                    PdfPCell photoCell = new PdfPCell();
                    photoCell.Border = Rectangle.NO_BORDER;
                    photoCell.Padding = 10;
                    photoCell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;

                    if (!string.IsNullOrEmpty(item.Photo))
                    {
                        try
                        {
                            string imagePath = HostingEnvironment.MapPath(item.Photo);
                            if (System.IO.File.Exists(imagePath))
                            {
                                iTextSharp.text.Image photo = iTextSharp.text.Image.GetInstance(imagePath);
                                photo.ScaleToFit(440f, 330f);
                                photo.Border = Rectangle.BOX;
                                photo.BorderColor = new BaseColor(200, 200, 200);
                                photo.BorderWidth = 1f;
                                photoCell.AddElement(photo);
                            }
                            else
                            {
                                AddNoPhotoPlaceholder(photoCell);
                            }
                        }
                        catch
                        {
                            AddNoPhotoPlaceholder(photoCell);
                        }
                    }
                    else
                    {
                        AddNoPhotoPlaceholder(photoCell);
                    }
                    contentTable.AddCell(photoCell);

                    // Cột phải - Thông tin
                    PdfPCell infoCell = new PdfPCell();
                    infoCell.Border = Rectangle.NO_BORDER;
                    infoCell.Padding = 10;

                    // Tên người thuê (to hơn)
                    var nameTitle = new Paragraph(item.FullName, new Font(baseFont, 18, Font.BOLD, new BaseColor(0, 51, 102)));
                    nameTitle.SpacingAfter = 15f;
                    infoCell.AddElement(nameTitle);

                    // Bảng thông tin chi tiết
                    PdfPTable infoTable = new PdfPTable(2);
                    infoTable.WidthPercentage = 100;
                    infoTable.SetWidths(new float[] { 40f, 60f });

                    AddInfoRow(infoTable, "Số CMND/CCCD:", item.IdentityCard, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Giới tính:", item.Gender, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Địa chỉ thường trú:", item.PermanentAddress, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Số điện thoại:", item.PhoneNumber, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Phòng thuê:", item.RoomName, detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Ngày ký hợp đồng:", item.ContractSignedDate.ToString("dd/MM/yyyy"), detailLabelFont, detailValueFont);
                    AddInfoRow(infoTable, "Ngày vào ở:", item.MoveInDate.ToString("dd/MM/yyyy"), detailLabelFont, detailValueFont);

                    infoCell.AddElement(infoTable);
                    contentTable.AddCell(infoCell);

                    pdfDoc.Add(contentTable);

                    // Footer của trang chi tiết
                    var footerTable = new PdfPTable(1);
                    footerTable.WidthPercentage = 100;
                    footerTable.TotalWidth = pdfDoc.PageSize.Width - 80;
                    footerTable.WriteSelectedRows(0, -1, 40, 60, writer.DirectContent);

                    tenantIndex++;
                }

                pdfDoc.Close();

                byte[] pdfBytes = stream.ToArray();
                return File(pdfBytes, "application/pdf", "DanhSachNguoiThuePhong.pdf");
            }
        }

        // Helper methods
        private void AddTableCell(PdfPTable table, string text, Font font, BaseColor bgColor, int align = PdfPCell.ALIGN_LEFT)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                BackgroundColor = bgColor,
                Padding = 5,
                BorderColor = new BaseColor(200, 200, 200)
            };
            table.AddCell(cell);
        }

        private void AddInfoRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont))
            {
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 8,
                PaddingRight = 5,
                HorizontalAlignment = PdfPCell.ALIGN_RIGHT
            };
            table.AddCell(labelCell);

            var valueCell = new PdfPCell(new Phrase(value, valueFont))
            {
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 8,
                PaddingLeft = 5
            };
            table.AddCell(valueCell);
        }

        private void AddNoPhotoPlaceholder(PdfPCell cell)
        {
            var placeholderTable = new PdfPTable(1);
            placeholderTable.WidthPercentage = 100;

            var placeholderCell = new PdfPCell();
            placeholderCell.FixedHeight = 330f;
            placeholderCell.BackgroundColor = new BaseColor(245, 245, 245);
            placeholderCell.BorderColor = new BaseColor(200, 200, 200);
            placeholderCell.BorderWidth = 1f;
            placeholderCell.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
            placeholderCell.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;

            string fontPath = HostingEnvironment.MapPath("~/Fonts/times.ttf");
            var noPhotoText = new Paragraph("Không có ảnh", new Font(BaseFont.CreateFont(fontPath, BaseFont.CP1252, BaseFont.NOT_EMBEDDED), 14, Font.ITALIC, BaseColor.GRAY));
            placeholderCell.AddElement(noPhotoText);

            placeholderTable.AddCell(placeholderCell);
            cell.AddElement(placeholderTable);
        }
        // Thêm method này vào TenantContractsController.cs

        // Thêm method này vào TenantContractsController.cs

        // Thêm method này vào TenantContractsController.cs

        public ActionResult ExportToWord(
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "ContractSignedDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null)
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = from ct in db.ContractTenants
                        join t in db.Tenants on ct.TenantId equals t.Id
                        join c in db.Contracts on ct.ContractId equals c.Id
                        join r in db.Rooms on c.RoomId equals r.Id
                        select new
                        {
                            FullName = t.FullName ?? "",
                            IdentityCard = t.IdentityCard ?? "",
                            Gender = t.Gender ?? "",
                            PermanentAddress = t.PermanentAddress ?? "",
                            PhoneNumber = t.PhoneNumber ?? "",
                            Photo = t.Photo,
                            RoomName = r.Name ?? "",
                            ContractSignedDate = c.StartDate,
                            MoveInDate = ct.Contract.MoveInDate
                        };

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.FullName.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCard.Contains(searchCard));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.PermanentAddress.Contains(searchAddress));

            if (from.HasValue)
                query = query.Where(x => x.ContractSignedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.ContractSignedDate <= to.Value);

            switch (sortField)
            {
                case "FullName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.FullName) : query.OrderByDescending(x => x.FullName);
                    break;
                case "ContractSignedDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
                case "MoveInDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.MoveInDate) : query.OrderByDescending(x => x.MoveInDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.ContractSignedDate) : query.OrderByDescending(x => x.ContractSignedDate);
                    break;
            }

            var items = query.ToList();

            // Tạo document Word
            var doc = new Aspose.Words.Document();
            var builder = new Aspose.Words.DocumentBuilder(doc);

            // Thiết lập page setup - giảm margin để có nhiều không gian hơn
            var pageSetup = builder.PageSetup;
            pageSetup.LeftMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(10); // 1cm thay vì mặc định 2.54cm
            pageSetup.RightMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(10); // 1cm
            //pageSetup.TopMargin = Aspose.Words.ConvertUtil.CentimeterToPoint(1.5); // 1.5cm
            //pageSetup.BottomMargin = Aspose.Words.ConvertUtil.CentimeterToPoint(1.5); // 1.5cm
            pageSetup.Orientation = Aspose.Words.Orientation.Portrait; // Dọc

            // Thiết lập font tiếng Việt
            builder.Font.Name = "Times New Roman";
            builder.Font.Size = 12;

            // TRANG BÌA
            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
            builder.Font.Size = 24;
            builder.Font.Bold = true;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("NHÀ TRỌ AN CƯ");

            builder.Font.Size = 14;
            builder.Font.Bold = false;
            builder.Font.Color = System.Drawing.Color.Gray;
            builder.Writeln("Thôn Đình Ngọ (Ấp), Xã Hồng Phong, Huyện An Dương, Hải Phòng");
            builder.Writeln("");

            builder.Font.Size = 18;
            builder.Font.Bold = true;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("DANH SÁCH NGƯỜI THUÊ PHÒNG");

            builder.Font.Size = 12;
            builder.Font.Bold = false;
            builder.Font.Color = System.Drawing.Color.Black;
            builder.Writeln($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            builder.Writeln("");

            // Reset alignment
            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;

            // BẢNG DANH SÁCH
            var table = builder.StartTable();

            // Header
            string[] headers = { "STT", "Họ tên", "Số thẻ", "GT", "Địa chỉ", "SĐT", "Phòng", "Ngày ký", "Ngày vào" };
            foreach (var header in headers)
            {
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Font.Color = System.Drawing.Color.White;
                builder.Font.Bold = true;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(header);
            }
            builder.EndRow();

            // Data rows
            int orderNumber = 1;
            foreach (var item in items)
            {
                // STT
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.Font.Bold = false;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(orderNumber.ToString());

                // Họ tên
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Write(item.FullName ?? "");

                // Số thẻ
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.IdentityCard ?? "");

                // Giới tính
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(item.Gender ?? "");

                // Địa chỉ
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Write(item.PermanentAddress ?? "");

                // SĐT
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.PhoneNumber ?? "");

                // Phòng
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Write(item.RoomName ?? "");

                // Ngày ký
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.ContractSignedDate.ToString("dd/MM/yyyy"));

                // Ngày vào
                builder.InsertCell();
                builder.CellFormat.Shading.BackgroundPatternColor = orderNumber % 2 == 0 ?
                    System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.White;
                builder.Write(item.MoveInDate.ToString("dd/MM/yyyy"));

                builder.EndRow();
                orderNumber++;
            }

            builder.EndTable();

            // Footer thông tin
            builder.Writeln("");
            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
            builder.Font.Bold = true;
            builder.Font.Size = 14;
            builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
            builder.Writeln("NHÀ TRỌ AN CƯ");

            builder.Font.Bold = false;
            builder.Font.Size = 12;
            builder.Font.Color = System.Drawing.Color.Black;
            builder.Writeln("ĐỊA CHỈ: THÔN ĐÌNH NGỌ (ẤP), XÃ HỒNG PHONG, HUYỆN AN DƯƠNG, HẢI PHÒNG");
            builder.Writeln("CHỦ NHÀ TRỌ: TẠ NGỌC DUY - CCCD: 034082002422");
            builder.Writeln("ĐIỆN THOẠI: 0975092833");

            // PHẦN CHI TIẾT TỪNG NGƯỜI (nếu có ảnh)
            int tenantIndex = 1;
            foreach (var item in items.Where(x => !string.IsNullOrEmpty(x.Photo)))
            {
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);

                // Header trang chi tiết
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                builder.Font.Size = 18;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Writeln("THÔNG TIN CHI TIẾT NGƯỜI THUÊ");

                builder.Font.Size = 12;
                builder.Font.Bold = false;
                builder.Writeln($"Người thuê số: {tenantIndex}/{items.Count(x => !string.IsNullOrEmpty(x.Photo))}");
                builder.Writeln("");

                // Tên người thuê
                builder.Font.Size = 16;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.FromArgb(0, 51, 102);
                builder.Writeln(item.FullName ?? "");
                builder.Writeln("");

                // Ảnh CCCD
                if (!string.IsNullOrEmpty(item.Photo))
                {
                    try
                    {
                        string imagePath = Server.MapPath(item.Photo);
                        if (System.IO.File.Exists(imagePath))
                        {
                            builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Center;
                            builder.Font.Size = 12;
                            builder.Font.Bold = true;
                            builder.Font.Color = System.Drawing.Color.Black;
                            builder.Writeln("Ảnh CCCD/Hộ chiếu:");

                            var shape = builder.InsertImage(imagePath);
                            shape.Width = 400;
                            shape.Height = 300;
                            shape.WrapType = Aspose.Words.Drawing.WrapType.Inline;
                            builder.Writeln("");
                        }
                    }
                    catch
                    {
                        // Bỏ qua nếu không tải được ảnh
                    }
                }

                // Thông tin chi tiết
                builder.ParagraphFormat.Alignment = Aspose.Words.ParagraphAlignment.Left;
                builder.Font.Size = 12;
                builder.Font.Bold = false;
                builder.Font.Color = System.Drawing.Color.Black;

                var infoTable = builder.StartTable();

                // Số CMND/CCCD
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Số CMND/CCCD:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.IdentityCard ?? "");
                builder.EndRow();

                // Giới tính
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Giới tính:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.Gender ?? "");
                builder.EndRow();

                // Địa chỉ thường trú
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Địa chỉ thường trú:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.PermanentAddress ?? "");
                builder.EndRow();

                // Số điện thoại
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Số điện thoại:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.PhoneNumber ?? "");
                builder.EndRow();

                // Phòng thuê
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Phòng thuê:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.RoomName ?? "");
                builder.EndRow();

                // Ngày ký hợp đồng
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Ngày ký hợp đồng:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.ContractSignedDate.ToString("dd/MM/yyyy"));
                builder.EndRow();

                // Ngày vào ở
                builder.InsertCell();
                builder.Font.Bold = true;
                builder.Write("Ngày vào ở:");
                builder.InsertCell();
                builder.Font.Bold = false;
                builder.Write(item.MoveInDate.ToString("dd/MM/yyyy"));
                builder.EndRow();

                builder.EndTable();
                tenantIndex++;
            }

            // Lưu file
            string fileName = $"DanhSachNguoiThuePhong_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string tempPath = Server.MapPath("~/Uploads/Tenants/");
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            string filePath = Path.Combine(tempPath, fileName);
            doc.Save(filePath);

            // Trả về file
            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            System.IO.File.Delete(filePath); // Xóa file temp

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
    }
}