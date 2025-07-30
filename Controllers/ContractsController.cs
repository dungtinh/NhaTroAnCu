using Microsoft.Owin.BuilderProperties;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Web;
using System.Web.Mvc;
using Xceed.Document.NET;
using Xceed.Pdf;
using Xceed.Words.NET;

namespace NhaTroAnCu.Controllers
{
    public class ContractsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: /Contracts/Create?roomId=5
        public ActionResult Create(int roomId)
        {
            var room = db.Rooms.Find(roomId);
            ViewBag.RoomId = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
            return View(new ContractCreateViewModel()
            {
                MoveInDate = DateTime.Now,
                PriceAgreed = room.DefaultPrice,
                DepositAmount = room.DefaultPrice,
                ElectricityPrice = 3500,
                WaterPrice = 15000,
            });
        }

        // GET: /Contracts/End/5
        public ActionResult End(int id)
        {
            var contract = db.Contracts.Find(id);
            if (contract == null) return HttpNotFound();

            contract.Status = "Ended";
            contract.EndDate = DateTime.Now;

            var room = db.Rooms.Find(contract.RoomId);
            if (room != null) room.IsOccupied = false;

            db.SaveChanges();
            return RedirectToAction("index", "Rooms", new { id = contract.RoomId });
        }
        public ActionResult ExportContract(int id)
        {
            var contract = db.Contracts
     .Include("Room")
     .Include("ContractTenants.Tenant") // ✅ dùng string thay vì lambda
     .FirstOrDefault(c => c.Id == id);

            if (contract == null) return HttpNotFound();

            string path = Server.MapPath("~/App_Data/Contracts");

            Aspose.Words.Document doc = new Aspose.Words.Document();
            doc.RemoveAllChildren();
            var dict = new Dictionary<string, string>();
            string pathTemp = Server.MapPath("~/App_Data/templates/CT01.docx");
            var doctemp = new Aspose.Words.Document(pathTemp);
            Aspose.Words.DocumentBuilder builder = new Aspose.Words.DocumentBuilder(doctemp);

            // Hợp đồng
            dict = new Dictionary<string, string>();
            pathTemp = Server.MapPath("~/App_Data/templates/HOPDONG.docx");
            doctemp = new Aspose.Words.Document(pathTemp);
            dict.Add("ngay", contract.StartDate.ToString("dd"));
            dict.Add("thang", contract.StartDate.ToString("MM"));
            dict.Add("nam", contract.StartDate.ToString("yyyy"));

            builder = new Aspose.Words.DocumentBuilder(doctemp);
            builder.MoveToMergeField("benthue");
            foreach (var khach in contract.ContractTenants)
            {
                string hoTen = khach.Tenant.FullName;
                // Kiểm tra chưa đủ 18 tuổi
                if (khach.Tenant.BirthDate != null)
                {
                    var age = (contract.StartDate - khach.Tenant.BirthDate.Value).TotalDays / 365.25;
                    if (age < 18)
                    {
                        hoTen += " (chưa đủ 18 tuổi)";
                    }
                }
                // kiểm tra nếu là dòng cuối thì builder.Write
                if (khach != contract.ContractTenants.Last())
                    builder.Writeln("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ";");
                else
                    builder.Write("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ".");
            }
            dict.Add("sophong", contract.Room.Name);
            doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());

            if (doctemp.PageCount % 2 != 0)
            {
                builder.MoveToDocumentEnd();
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
            }
            doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);

            // I, TỜ KHAI CT01
            foreach (var khach in contract.ContractTenants)
            {
                dict = new Dictionary<string, string>();
                pathTemp = Server.MapPath("~/App_Data/templates/CT01.docx");
                doctemp = new Aspose.Words.Document(pathTemp);
                dict.Add("hoten", khach.Tenant.FullName);
                dict.Add("sodienthoai", khach.Tenant.PhoneNumber);
                dict.Add("ngaysinh", khach.Tenant.BirthDate?.ToString("dd/MM/yyyy"));

                GenSoDinhDanh(khach.Tenant.IdentityCard, dict);
                doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());
                builder = new Aspose.Words.DocumentBuilder(doctemp);
                if (doctemp.PageCount % 2 != 0)
                {
                    builder.MoveToDocumentEnd();
                    builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
                }
                doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);
            }
            // Tài sản            
            pathTemp = Server.MapPath("~/App_Data/templates/TAISAN.docx");
            doctemp = new Aspose.Words.Document(pathTemp);

            builder = new Aspose.Words.DocumentBuilder(doctemp);
            builder.MoveToMergeField("benthue");
            foreach (var khach in contract.ContractTenants)
            {
                string hoTen = khach.Tenant.FullName;
                // Kiểm tra chưa đủ 18 tuổi
                if (khach.Tenant.BirthDate != null)
                {
                    var age = (contract.StartDate - khach.Tenant.BirthDate.Value).TotalDays / 365.25;
                    if (age < 18)
                    {
                        hoTen += " (chưa đủ 18 tuổi)";
                    }
                }
                // kiểm tra nếu là dòng cuối thì builder.Write
                if (khach != contract.ContractTenants.Last())
                    builder.Writeln("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ";");
                else
                    builder.Write("Họ và tên: " + hoTen + "; CC/CCCD: " + khach.Tenant.IdentityCard + "; Đt: " + khach.Tenant.PhoneNumber + ".");
            }
            doctemp.MailMerge.Execute(dict.Keys.ToArray(), dict.Values.ToArray());
            if (doctemp.PageCount % 2 != 0)
            {
                builder.MoveToDocumentEnd();
                builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
            }
            doc.AppendDocument(doctemp, Aspose.Words.ImportFormatMode.KeepSourceFormatting);


            string webpart = string.Empty;
            builder = new Aspose.Words.DocumentBuilder(doc);
            foreach (var khach in contract.ContractTenants)
            {
                webpart += khach.Tenant.FullName + "_";
                // --- Chèn ảnh khách thuê vào docx ---
                if (!string.IsNullOrEmpty(khach.Tenant.Photo))
                {
                    // Đường dẫn vật lý
                    string photoPath = Server.MapPath(khach.Tenant.Photo);
                    if (System.IO.File.Exists(photoPath))
                    {
                        // Chèn ảnh vào vị trí mong muốn, ví dụ dưới cùng trang
                        builder.MoveToDocumentEnd();
                        builder.Writeln(); // Xuống dòng
                        builder.InsertImage(photoPath);
                    }
                }
                // --- Kết thúc chèn ảnh ---
                if (doc.PageCount % 2 != 0)
                {
                    builder.MoveToDocumentEnd();
                    builder.InsertBreak(Aspose.Words.BreakType.PageBreak);
                }
            }
            webpart += "_phong_" + contract.Room.Name + "_c" + id + ".docx";
            foreach (Aspose.Words.Section section in doc.Sections)
            {
                section.ClearHeadersFooters();
            }
            path += webpart;
            doc.Save(path);
            return File(path, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", webpart);
        }

        public ActionResult ExportHistoryToPdf(
            int id,
            string searchName = "",
            string searchCard = "",
            string searchAddress = "",
            string sortField = "StartDate",
            string sortDirection = "desc",
            string fromDate = null,
            string toDate = null
        )
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = db.Contracts
                .Where(c => c.RoomId == id)
                .Select(c => new
                {
                    RoomName = c.Room.Name,
                    Tenants = c.ContractTenants.Select(ct => ct.Tenant.FullName),
                    IdentityCards = c.ContractTenants.Select(ct => ct.Tenant.IdentityCard),
                    Genders = c.ContractTenants.Select(ct => ct.Tenant.Gender),
                    Addresses = c.ContractTenants.Select(ct => ct.Tenant.PermanentAddress),
                    PhoneNumbers = c.ContractTenants.Select(ct => ct.Tenant.PhoneNumber),
                    c.StartDate,
                    c.EndDate,
                    c.Note
                });

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.Tenants.Any(n => n.Contains(searchName)));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCards.Any(ic => ic.Contains(searchCard)));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.Addresses.Any(addr => addr.Contains(searchAddress)));
            if (from.HasValue)
                query = query.Where(x => x.StartDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.StartDate <= to.Value);

            switch (sortField)
            {
                case "RoomName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.RoomName) : query.OrderByDescending(x => x.RoomName);
                    break;
                case "StartDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
                case "EndDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.EndDate) : query.OrderByDescending(x => x.EndDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
            }

            var items = query.ToList();

            string fontPath = Server.MapPath("~/Fonts/times.ttf");
            var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

            var titleFont = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD);
            var headerFont = new iTextSharp.text.Font(baseFont, 12, iTextSharp.text.Font.BOLD);
            var rowFont = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL);

            using (var stream = new MemoryStream())
            {
                var pdfDoc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 10, 10, 20, 20);
                iTextSharp.text.pdf.PdfWriter.GetInstance(pdfDoc, stream);
                pdfDoc.Open();

                pdfDoc.Add(new iTextSharp.text.Paragraph("LỊCH SỬ THUÊ PHÒNG", titleFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph(" "));

                var table = new iTextSharp.text.pdf.PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 2.2f, 2.8f, 2.2f, 1.2f, 3f, 2f, 2f, 2f, 3f });

                string[] headers = { "Phòng", "Người thuê", "Số thẻ", "Giới tính", "Địa chỉ", "SĐT", "Ngày thuê", "Ngày kết thúc", "Ghi chú" };
                foreach (var header in headers)
                {
                    var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(header, headerFont));
                    cell.BackgroundColor = new iTextSharp.text.BaseColor(220, 220, 220);
                    cell.HorizontalAlignment = iTextSharp.text.pdf.PdfPCell.ALIGN_CENTER;
                    table.AddCell(cell);
                }

                foreach (var item in items)
                {
                    table.AddCell(new iTextSharp.text.Phrase(item.RoomName, rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Tenants), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.IdentityCards), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Genders), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.Addresses), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(string.Join(", ", item.PhoneNumbers), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.StartDate.ToString("dd/MM/yyyy"), rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.EndDate.ToString("dd/MM/yyyy") ?? "-", rowFont));
                    table.AddCell(new iTextSharp.text.Phrase(item.Note, rowFont));
                }

                pdfDoc.Add(table);
                pdfDoc.Add(new iTextSharp.text.Paragraph(" "));
                pdfDoc.Add(new iTextSharp.text.Paragraph("NHÀ TRỌ AN CƯ", titleFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("ĐỊA CHỈ: THÔN ĐÌNH NGỌ (ẤP), XÃ HỒNG PHONG, HUYỆN AN DƯƠNG, HẢI PHÒNG", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("CHỦ NHÀ TRỌ: TẠ NGỌC DUY - CCCD: 034082002422", rowFont));
                pdfDoc.Add(new iTextSharp.text.Paragraph("ĐIỆN THOẠI: 0975092833", rowFont));
                pdfDoc.Close();

                byte[] pdfBytes = stream.ToArray();
                return File(pdfBytes, "application/pdf", "LichSuThuePhong.pdf");
            }
        }
        void GenSoDinhDanh(string sdd, Dictionary<string, string> dict)
        {
            char s1 = ' ', s2 = ' ', s3 = ' ', s4 = ' ', s5 = ' ',
                s6 = ' ', s7 = ' ', s8 = ' ', s9 = ' ', s10 = ' ', s11 = ' ', s12 = ' ';
            if (!string.IsNullOrEmpty(sdd))
            {
                var chars = sdd.ToCharArray();
                if (chars.Length >= 12)
                {
                    s1 = chars[0];
                    s2 = chars[1];
                    s3 = chars[2];
                    s4 = chars[3];
                    s5 = chars[4];
                    s6 = chars[5];
                    s7 = chars[6];
                    s8 = chars[7];
                    s9 = chars[8];
                    s10 = chars[9];
                    s11 = chars[10];
                    s12 = chars[11];
                }
            }
            dict.Add("s1", s1.ToString());
            dict.Add("s2", s2.ToString());
            dict.Add("s3", s3.ToString());
            dict.Add("s4", s4.ToString());
            dict.Add("s5", s5.ToString());
            dict.Add("s6", s6.ToString());
            dict.Add("s7", s7.ToString());
            dict.Add("s8", s8.ToString());
            dict.Add("s9", s9.ToString());
            dict.Add("s10", s10.ToString());
            dict.Add("s11", s11.ToString());
            dict.Add("s12", s12.ToString());
        }
        [HttpPost]
        public ActionResult UploadContractScan(int contractId, HttpPostedFileBase scanFile)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null) return HttpNotFound();

            if (scanFile != null && scanFile.ContentLength > 0)
            {
                string fileName = Path.GetFileNameWithoutExtension(scanFile.FileName);
                string ext = Path.GetExtension(scanFile.FileName);
                string uniqueName = $"contractscan_{contractId}_{DateTime.Now.Ticks}{ext}";
                string folder = Server.MapPath("~/Uploads/ContractScans/");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, uniqueName);
                scanFile.SaveAs(filePath);
                contract.ContractScanFilePath = "/Uploads/ContractScans/" + uniqueName;
                db.SaveChanges();
            }
            // Redirect về lại trang chi tiết phòng đang xem
            return RedirectToAction("Details", "Rooms", new { id = contract.RoomId });
        }

        public ActionResult Edit(int id)
        {
            var contract = db.Contracts.Include("ContractTenants.Tenant").FirstOrDefault(c => c.Id == id);
            if (contract == null) return HttpNotFound();

            var vm = new ContractEditViewModel
            {
                Id = contract.Id,
                RoomId = contract.RoomId,
                MoveInDate = contract.MoveInDate,
                Months = ((contract.EndDate.Year - contract.StartDate.Year) * 12 + contract.EndDate.Month - contract.StartDate.Month),
                PriceAgreed = contract.PriceAgreed,
                DepositAmount = contract.DepositAmount,
                ElectricityPrice = contract.ElectricityPrice,
                WaterPrice = contract.WaterPrice,
                Tenants = contract.ContractTenants.Select(ct => new TenantEditModel
                {
                    Id = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Photo = ct.Tenant.Photo,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate
                }).ToList()
            };

            ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name", vm.RoomId);
            return View(vm);
        }

        [HttpPost]
        public ActionResult ExtendContract(int contractId, int extendMonths, string note)
        {
            var contract = db.Contracts.Find(contractId);
            if (contract == null || contract.Status != "Active")
                return HttpNotFound();

            if (extendMonths <= 0)
                return Json(new { success = false, message = "Số tháng gia hạn phải lớn hơn 0." });

            DateTime? oldEndDate = contract.EndDate;
            if (oldEndDate.HasValue)
                contract.EndDate = oldEndDate.Value.AddMonths(extendMonths);
            else
                contract.EndDate = DateTime.Now.AddMonths(extendMonths);

            // Lưu lịch sử gia hạn
            var log = new ContractExtensionHistory
            {
                ContractId = contract.Id,
                ExtendedAt = DateTime.Now,
                OldEndDate = oldEndDate,
                NewEndDate = contract.EndDate,
                ExtendMonths = extendMonths,
                Note = note
            };
            db.ContractExtensionHistories.Add(log);

            db.SaveChanges();
            return Json(new { success = true, newEndDate = contract.EndDate.ToString("dd/MM/yyyy") });
        }

        public ActionResult History(
                int id, int page = 1, int pageSize = 20,
                string searchName = "", string searchCard = "", string searchAddress = "",
                string sortField = "StartDate", string sortDirection = "desc",
                string fromDate = null, string toDate = null)
        {
            ViewBag.RoomId = id;
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDate))
                from = DateTime.ParseExact(fromDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(toDate))
                to = DateTime.ParseExact(toDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            var query = db.Contracts
                .Where(c => c.RoomId == id)
                .Select(c => new
                {
                    RoomName = c.Room.Name,
                    Tenants = c.ContractTenants.Select(ct => ct.Tenant.FullName),
                    IdentityCards = c.ContractTenants.Select(ct => ct.Tenant.IdentityCard),
                    Genders = c.ContractTenants.Select(ct => ct.Tenant.Gender),
                    Addresses = c.ContractTenants.Select(ct => ct.Tenant.PermanentAddress),
                    PhoneNumbers = c.ContractTenants.Select(ct => ct.Tenant.PhoneNumber),
                    c.StartDate,
                    c.EndDate,
                    c.Note
                });

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(x => x.Tenants.Any(n => n.Contains(searchName)));
            if (!string.IsNullOrEmpty(searchCard))
                query = query.Where(x => x.IdentityCards.Any(ic => ic.Contains(searchCard)));
            if (!string.IsNullOrEmpty(searchAddress))
                query = query.Where(x => x.Addresses.Any(addr => addr.Contains(searchAddress)));
            if (from.HasValue)
                query = query.Where(x => x.StartDate >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.StartDate <= to.Value);

            switch (sortField)
            {
                case "RoomName":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.RoomName) : query.OrderByDescending(x => x.RoomName);
                    break;
                case "StartDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
                case "EndDate":
                    query = sortDirection == "asc" ? query.OrderBy(x => x.EndDate) : query.OrderByDescending(x => x.EndDate);
                    break;
                default:
                    query = sortDirection == "asc" ? query.OrderBy(x => x.StartDate) : query.OrderByDescending(x => x.StartDate);
                    break;
            }

            int totalItems = query.Count();

            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                .Select(x => new NhaTroAnCu.Models.ContractHistoryItemViewModel
                {
                    RoomName = x.RoomName,
                    TenantNames = string.Join(", ", x.Tenants),
                    IdentityCards = string.Join(", ", x.IdentityCards),
                    Genders = string.Join(", ", x.Genders),
                    Addresses = string.Join(", ", x.Addresses),
                    PhoneNumbers = string.Join(", ", x.PhoneNumbers),
                    StartDate = x.StartDate,
                    EndDate = x.EndDate, // giữ nullable
                    Note = x.Note
                }).ToList();

            var model = new NhaTroAnCu.Models.ContractHistoryViewModel
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
                ToDate = to,
                RoomId = id
            };

            return View(model);
        }
        // POST: /Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ContractCreateViewModel vm, IEnumerable<HttpPostedFileBase> TenantPhotos)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RoomId = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
                return View(vm);
            }

            var contract = new Contract
            {
                RoomId = vm.RoomId,
                MoveInDate = vm.MoveInDate,
                StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day),
                EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months),
                PriceAgreed = vm.PriceAgreed,
                DepositAmount = vm.DepositAmount,
                Note = vm.Note,
                Status = "Active",
                ElectricityPrice = vm.ElectricityPrice,
                WaterPrice = vm.WaterPrice
            };

            db.Contracts.Add(contract);
            db.SaveChanges();

            // Group uploaded files by tenant index based on file input names
            var photosByTenant = GroupPhotosByTenant(TenantPhotos, vm.Tenants.Count);

            for (int i = 0; i < vm.Tenants.Count; i++)
            {
                string photoPaths = null;
                var tenantPhotos = new List<string>();

                // Get photos specifically for this tenant
                if (photosByTenant.ContainsKey(i))
                {
                    var files = photosByTenant[i];
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                        string ext = Path.GetExtension(file.FileName);
                        string uniqueName = fileName + "_" + Guid.NewGuid().ToString("N") + ext;
                        string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");
                        if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
                        string savePath = Path.Combine(serverPath, uniqueName);
                        file.SaveAs(savePath);
                        tenantPhotos.Add("/Uploads/TenantPhotos/" + uniqueName);
                    }
                }

                // Save photo paths
                if (tenantPhotos.Any())
                {
                    photoPaths = string.Join(";", tenantPhotos);
                }

                var t = vm.Tenants[i];
                var tenant = new Tenant
                {
                    FullName = t.FullName,
                    IdentityCard = t.IdentityCard,
                    PhoneNumber = t.PhoneNumber,
                    BirthDate = t.BirthDate,
                    Gender = t.Gender,
                    PermanentAddress = t.PermanentAddress,
                    Photo = photoPaths,
                    Ethnicity = t.Ethnicity,
                    VehiclePlate = t.VehiclePlate
                };
                db.Tenants.Add(tenant);
                db.SaveChanges();

                db.ContractTenants.Add(new ContractTenant
                {
                    ContractId = contract.Id,
                    TenantId = tenant.Id,
                    CreatedAt = DateTime.Now
                });
            }

            // Mark room as occupied
            var room = db.Rooms.Find(vm.RoomId);
            if (room != null)
            {
                room.IsOccupied = true;
            }

            db.SaveChanges();
            return RedirectToAction("Index", "Rooms");
        }

        /// <summary>
        /// Enhanced method to group uploaded photos by tenant index
        /// Supports multiple strategies for associating photos with tenants
        /// </summary>
        /// <param name="photos">All uploaded photos</param>
        /// <param name="tenantCount">Number of tenants</param>
        /// <returns>Dictionary with tenant index as key and list of photos as value</returns>
        private Dictionary<int, List<HttpPostedFileBase>> GroupPhotosByTenant(IEnumerable<HttpPostedFileBase> photos, int tenantCount)
        {
            var result = new Dictionary<int, List<HttpPostedFileBase>>();

            if (photos == null)
                return result;

            var photoList = photos.Where(p => p != null && p.ContentLength > 0).ToList();
            if (!photoList.Any())
                return result;

            // Initialize lists for each tenant
            for (int i = 0; i < tenantCount; i++)
            {
                result[i] = new List<HttpPostedFileBase>();
            }

            // Strategy 1: Parse tenant index from filename (if modified by frontend)
            var assignedPhotos = new HashSet<HttpPostedFileBase>();

            foreach (var photo in photoList)
            {
                var fileName = Path.GetFileNameWithoutExtension(photo.FileName);

                // Look for pattern like "tenant_0_filename" 
                if (fileName.StartsWith("tenant_"))
                {
                    try
                    {
                        var parts = fileName.Split('_');
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int tenantIndex))
                        {
                            if (tenantIndex >= 0 && tenantIndex < tenantCount)
                            {
                                result[tenantIndex].Add(photo);
                                assignedPhotos.Add(photo);
                            }
                        }
                    }
                    catch
                    {
                        // Continue to next strategy if parsing fails
                    }
                }
            }

            // Strategy 2: Group by consecutive batches (for when files are uploaded in order)
            var unassignedPhotos = photoList.Where(p => !assignedPhotos.Contains(p)).ToList();

            if (unassignedPhotos.Any())
            {
                // Calculate photos per tenant (with remainder handling)
                int photosPerTenant = unassignedPhotos.Count / tenantCount;
                int remainder = unassignedPhotos.Count % tenantCount;

                int currentIndex = 0;

                for (int tenantIndex = 0; tenantIndex < tenantCount && currentIndex < unassignedPhotos.Count; tenantIndex++)
                {
                    // Calculate how many photos this tenant should get
                    int photosForThisTenant = photosPerTenant + (tenantIndex < remainder ? 1 : 0);

                    // Assign photos to this tenant
                    for (int j = 0; j < photosForThisTenant && currentIndex < unassignedPhotos.Count; j++)
                    {
                        result[tenantIndex].Add(unassignedPhotos[currentIndex]);
                        currentIndex++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Alternative method: Use form field positioning to determine tenant association
        /// This method assumes the HTML form sends files in the same order as the tenant forms
        /// </summary>
        /// <param name="request">Current HTTP request</param>
        /// <param name="tenantCount">Number of tenants</param>
        /// <returns>Dictionary with tenant index as key and list of photos as value</returns>
        private Dictionary<int, List<HttpPostedFileBase>> GroupPhotosByFormPosition(HttpRequestBase request, int tenantCount)
        {
            var result = new Dictionary<int, List<HttpPostedFileBase>>();

            // Initialize lists for each tenant
            for (int i = 0; i < tenantCount; i++)
            {
                result[i] = new List<HttpPostedFileBase>();
            }

            // Get all files for TenantPhotos
            var allFiles = new List<HttpPostedFileBase>();

            for (int i = 0; i < request.Files.Count; i++)
            {
                var file = request.Files[i];
                if (file != null && file.ContentLength > 0 &&
                    request.Files.AllKeys[i] == "TenantPhotos")
                {
                    allFiles.Add(file);
                }
            }

            // Distribute files evenly among tenants
            if (allFiles.Any())
            {
                int photosPerTenant = Math.Max(1, allFiles.Count / tenantCount);
                int currentTenantIndex = 0;
                int photosAssignedToCurrentTenant = 0;

                foreach (var file in allFiles)
                {
                    result[currentTenantIndex].Add(file);
                    photosAssignedToCurrentTenant++;

                    // Move to next tenant if current tenant has enough photos
                    if (photosAssignedToCurrentTenant >= photosPerTenant &&
                        currentTenantIndex < tenantCount - 1)
                    {
                        currentTenantIndex++;
                        photosAssignedToCurrentTenant = 0;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Method to handle file uploads with explicit tenant association
        /// Call this method instead of the original Create/Edit methods
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateWithTenantPhotos(ContractCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RoomId = new SelectList(db.Rooms.Where(r => !r.IsOccupied), "Id", "Name");
                return View("Create", vm);
            }

            var contract = new Contract
            {
                RoomId = vm.RoomId,
                MoveInDate = vm.MoveInDate,
                StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day),
                EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months),
                PriceAgreed = vm.PriceAgreed,
                DepositAmount = vm.DepositAmount,
                Note = vm.Note,
                Status = "Active",
                ElectricityPrice = vm.ElectricityPrice,
                WaterPrice = vm.WaterPrice
            };

            db.Contracts.Add(contract);
            db.SaveChanges();

            // Use improved photo grouping
            var photosByTenant = GroupPhotosByFormPosition(Request, vm.Tenants.Count);

            for (int i = 0; i < vm.Tenants.Count; i++)
            {
                string photoPaths = ProcessTenantPhotos(photosByTenant.ContainsKey(i) ? photosByTenant[i] : new List<HttpPostedFileBase>());

                var t = vm.Tenants[i];
                var tenant = new Tenant
                {
                    FullName = t.FullName,
                    IdentityCard = t.IdentityCard,
                    PhoneNumber = t.PhoneNumber,
                    BirthDate = t.BirthDate,
                    Gender = t.Gender,
                    PermanentAddress = t.PermanentAddress,
                    Photo = photoPaths,
                    Ethnicity = t.Ethnicity,
                    VehiclePlate = t.VehiclePlate
                };

                db.Tenants.Add(tenant);
                db.SaveChanges();

                db.ContractTenants.Add(new ContractTenant
                {
                    ContractId = contract.Id,
                    TenantId = tenant.Id,
                    CreatedAt = DateTime.Now
                });
            }

            // Mark room as occupied
            var room = db.Rooms.Find(vm.RoomId);
            if (room != null)
            {
                room.IsOccupied = true;
            }

            db.SaveChanges();
            return RedirectToAction("Index", "Rooms");
        }

        /// <summary>
        /// Helper method to process and save photos for a tenant
        /// </summary>
        /// <param name="files">List of photo files for this tenant</param>
        /// <returns>Semicolon-separated string of photo paths</returns>
        private string ProcessTenantPhotos(List<HttpPostedFileBase> files)
        {
            if (files == null || !files.Any())
                return null;

            var tenantPhotos = new List<string>();

            foreach (var file in files)
            {
                if (file != null && file.ContentLength > 0)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                        string ext = Path.GetExtension(file.FileName);
                        string uniqueName = fileName + "_" + Guid.NewGuid().ToString("N") + ext;
                        string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");

                        if (!Directory.Exists(serverPath))
                            Directory.CreateDirectory(serverPath);

                        string savePath = Path.Combine(serverPath, uniqueName);
                        file.SaveAs(savePath);
                        tenantPhotos.Add("/Uploads/TenantPhotos/" + uniqueName);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other files
                        System.Diagnostics.Debug.WriteLine($"Error saving photo: {ex.Message}");
                    }
                }
            }

            return tenantPhotos.Any() ? string.Join(";", tenantPhotos) : null;
        }

        /// <summary>
        /// Helper method to safely delete old photos
        /// </summary>
        /// <param name="photoPath">Semicolon-separated photo paths</param>
        private void DeleteOldPhotos(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
                return;

            var oldPhotos = photoPath.Split(';');
            foreach (var oldPhoto in oldPhotos.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                try
                {
                    var physicalPath = Server.MapPath(oldPhoto);
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    System.Diagnostics.Debug.WriteLine($"Error deleting photo {oldPhoto}: {ex.Message}");
                }
            }
        }
        // Cập nhật phương thức Edit trong ContractsController.cs

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ContractEditViewModel vm)
        {
            var contract = db.Contracts.Include("ContractTenants.Tenant").FirstOrDefault(c => c.Id == vm.Id);
            if (contract == null)
                return HttpNotFound();

            // Update contract information
            contract.MoveInDate = vm.MoveInDate;
            contract.StartDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day);
            contract.EndDate = vm.MoveInDate.AddDays(10 - vm.MoveInDate.Day).AddMonths(vm.Months);
            contract.PriceAgreed = vm.PriceAgreed;
            contract.DepositAmount = vm.DepositAmount;
            contract.ElectricityPrice = vm.ElectricityPrice;
            contract.WaterPrice = vm.WaterPrice;

            // Handle tenant mapping
            var oldTenantIds = contract.ContractTenants.Select(ct => ct.TenantId).ToList();
            var newTenantIds = vm.Tenants.Where(t => t.Id > 0).Select(t => t.Id).ToList();

            // Remove tenants that are no longer in the contract
            foreach (var ct in contract.ContractTenants.Where(ct => !newTenantIds.Contains(ct.TenantId)).ToList())
            {
                var tenant = db.Tenants.Find(ct.TenantId);
                if (tenant != null && !string.IsNullOrEmpty(tenant.Photo))
                {
                    DeleteOldPhotos(tenant.Photo);
                }
                db.ContractTenants.Remove(ct);
                if (tenant != null)
                {
                    db.Tenants.Remove(tenant);
                }
            }

            // Process file uploads - Cải tiến logic xử lý
            var allFiles = new List<HttpPostedFileBase>();
            var filesByTenant = new Dictionary<int, List<HttpPostedFileBase>>();

            // Thu thập tất cả files và phân loại theo tenant index
            for (int i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];
                if (file != null && file.ContentLength > 0)
                {
                    // Kiểm tra key name để xác định tenant index
                    var key = Request.Files.AllKeys[i];
                    if (key == "TenantPhotos")
                    {
                        // Sử dụng một cách khác để xác định tenant index
                        // Dựa vào thứ tự upload hoặc metadata khác
                        allFiles.Add(file);
                    }
                }
            }

            // Phương pháp mới: Phân bổ files dựa trên file name pattern
            foreach (var file in allFiles)
            {
                int tenantIndex = ExtractTenantIndexFromFileName(file.FileName);
                if (tenantIndex == -1)
                {
                    // Nếu không tìm thấy index trong filename, 
                    // phân bổ dựa trên vị trí trong danh sách
                    tenantIndex = DetermineTargetTenant(file, allFiles, vm.Tenants.Count);
                }

                if (!filesByTenant.ContainsKey(tenantIndex))
                {
                    filesByTenant[tenantIndex] = new List<HttpPostedFileBase>();
                }
                filesByTenant[tenantIndex].Add(file);
            }

            // Update existing tenants and add new tenants
            for (int i = 0; i < vm.Tenants.Count; i++)
            {
                var t = vm.Tenants[i];
                var tenantPhotos = new List<string>();

                // Xử lý photos cho tenant này
                if (filesByTenant.ContainsKey(i))
                {
                    foreach (var file in filesByTenant[i])
                    {
                        try
                        {
                            string savedPath = SaveTenantPhoto(file);
                            if (!string.IsNullOrEmpty(savedPath))
                            {
                                tenantPhotos.Add(savedPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error saving photo: {ex.Message}");
                        }
                    }
                }

                if (t.Id > 0)
                {
                    // Update existing tenant
                    var tenant = db.Tenants.Find(t.Id);
                    if (tenant != null)
                    {
                        tenant.FullName = t.FullName;
                        tenant.IdentityCard = t.IdentityCard;
                        tenant.PhoneNumber = t.PhoneNumber;
                        tenant.BirthDate = t.BirthDate;
                        tenant.Gender = t.Gender;
                        tenant.PermanentAddress = t.PermanentAddress;
                        tenant.Ethnicity = t.Ethnicity;
                        tenant.VehiclePlate = t.VehiclePlate;

                        // Only update photos if new ones are uploaded
                        if (tenantPhotos.Any())
                        {
                            // Delete old photos
                            if (!string.IsNullOrEmpty(tenant.Photo))
                            {
                                DeleteOldPhotos(tenant.Photo);
                            }
                            tenant.Photo = string.Join(";", tenantPhotos);
                        }
                        // Keep existing photos if no new ones uploaded
                    }
                }
                else
                {
                    // Add new tenant
                    var tenant = new Tenant
                    {
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Photo = tenantPhotos.Any() ? string.Join(";", tenantPhotos) : null,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate
                    };
                    db.Tenants.Add(tenant);
                    db.SaveChanges();

                    db.ContractTenants.Add(new ContractTenant
                    {
                        ContractId = contract.Id,
                        TenantId = tenant.Id,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            db.SaveChanges();
            return RedirectToAction("Details", "Rooms", new { id = contract.RoomId });
        }

        // Helper methods mới

        /// <summary>
        /// Trích xuất tenant index từ tên file
        /// Ví dụ: "tenant_0_cccd.jpg" -> 0
        /// </summary>
        private int ExtractTenantIndexFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return -1;

            // Pattern: tenant_X_ hoặc tenantX_
            var patterns = new[] { @"tenant_(\d+)_", @"tenant(\d+)_" };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Xác định tenant target dựa trên vị trí và context
        /// </summary>
        private int DetermineTargetTenant(HttpPostedFileBase file, List<HttpPostedFileBase> allFiles, int tenantCount)
        {
            // Tìm vị trí của file trong danh sách
            int fileIndex = allFiles.IndexOf(file);
            if (fileIndex == -1) return 0;

            // Phân bổ đều các files cho các tenants
            int filesPerTenant = Math.Max(1, allFiles.Count / tenantCount);
            int targetTenant = fileIndex / filesPerTenant;

            // Đảm bảo không vượt quá số lượng tenant
            return Math.Min(targetTenant, tenantCount - 1);
        }

        /// <summary>
        /// Lưu ảnh tenant với error handling tốt hơn
        /// </summary>
        private string SaveTenantPhoto(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                string ext = Path.GetExtension(file.FileName);

                // Tạo tên file unique
                string uniqueName = $"{fileName}_{DateTime.Now.Ticks}_{Guid.NewGuid():N}{ext}";

                string serverPath = Server.MapPath("~/Uploads/TenantPhotos/");
                if (!Directory.Exists(serverPath))
                {
                    Directory.CreateDirectory(serverPath);
                }

                string savePath = Path.Combine(serverPath, uniqueName);
                file.SaveAs(savePath);

                return "/Uploads/TenantPhotos/" + uniqueName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving file {file.FileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Phương thức cải tiến để nhóm photos theo tenant
        /// </summary>
        private Dictionary<int, List<HttpPostedFileBase>> GroupPhotosByTenantImproved(int tenantCount)
        {
            var result = new Dictionary<int, List<HttpPostedFileBase>>();

            // Initialize
            for (int i = 0; i < tenantCount; i++)
            {
                result[i] = new List<HttpPostedFileBase>();
            }

            // Đọc thông tin từ form data để xác định chính xác file nào thuộc tenant nào
            var tenantPhotoMappings = new Dictionary<string, int>();

            for (int i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];
                var key = Request.Files.AllKeys[i];

                if (file != null && file.ContentLength > 0 && key == "TenantPhotos")
                {
                    // Kiểm tra xem có thông tin tenant index trong form không
                    var tenantIndexKey = $"TenantPhotos_Index_{i}";
                    if (Request.Form[tenantIndexKey] != null &&
                        int.TryParse(Request.Form[tenantIndexKey], out int tenantIndex))
                    {
                        if (tenantIndex >= 0 && tenantIndex < tenantCount)
                        {
                            result[tenantIndex].Add(file);
                            continue;
                        }
                    }

                    // Fallback: phân bổ tuần tự
                    int targetIndex = i % tenantCount;
                    result[targetIndex].Add(file);
                }
            }

            return result;
        }
    }
}