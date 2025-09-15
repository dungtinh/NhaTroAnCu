using NhaTroAnCu.Helpers;
using NhaTroAnCu.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using FlexCel.Core;
using FlexCel.XlsAdapter;
using System.Drawing;
using System.IO;
using System.Drawing;

namespace NhaTroAnCu.Controllers
{
    public class ContractTenantsController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();

        // GET: ContractTenants/ExportExcel - Xuất danh sách khách thuê ra Excel
        public ActionResult ExportExcel(string searchName, string searchCard, string searchRoom,
    string filterStatus, string filterCompany, string filterContractType)
        {
            try
            {
                var query = from ct in db.ContractTenants
                            join t in db.Tenants on ct.TenantId equals t.Id
                            join r in db.Rooms on ct.RoomId equals r.Id
                            join c in db.Contracts on ct.ContractId equals c.Id
                            join cr in db.ContractRooms on new { c.Id, ct.RoomId }
                                equals new { Id = cr.ContractId, RoomId = cr.RoomId }
                            select new
                            {
                                ct.Id,
                                TenantId = t.Id,
                                TenantName = t.FullName,
                                t.IdentityCard,
                                t.PhoneNumber,
                                t.BirthDate,
                                t.Gender,
                                t.Ethnicity,
                                t.PermanentAddress,
                                t.VehiclePlate,
                                t.Photo,
                                RoomId = r.Id,
                                RoomName = r.Name,
                                ContractId = c.Id,
                                ContractType = c.ContractType,
                                ContractStatus = c.Status,
                                c.StartDate,
                                c.EndDate,
                                c.MoveInDate,
                                CompanyId = c.CompanyId,
                                CompanyName = c.Company != null ? c.Company.CompanyName : null
                            };

                // Apply filters
                if (!string.IsNullOrEmpty(searchName))
                    query = query.Where(x => x.TenantName.Contains(searchName));

                if (!string.IsNullOrEmpty(searchCard))
                    query = query.Where(x => x.IdentityCard.Contains(searchCard));

                if (!string.IsNullOrEmpty(searchRoom))
                    query = query.Where(x => x.RoomName.Contains(searchRoom));

                if (!string.IsNullOrEmpty(filterStatus))
                    query = query.Where(x => x.ContractStatus == filterStatus);

                if (!string.IsNullOrEmpty(filterCompany) && filterCompany != "0")
                {
                    int companyId = int.Parse(filterCompany);
                    query = query.Where(x => x.CompanyId == companyId);
                }

                if (!string.IsNullOrEmpty(filterContractType))
                    query = query.Where(x => x.ContractType == filterContractType);

                var allTenants = query.OrderBy(x => x.RoomName).ThenBy(x => x.TenantName).ToList();

                // Phân loại người Việt Nam và người nước ngoài
                var vietnameseTenants = allTenants.Where(t =>
                    string.IsNullOrEmpty(t.Ethnicity) ||
                    (!t.Ethnicity.ToLower().Contains("trung quốc") &&
                     !t.Ethnicity.ToLower().Contains("china") &&
                     !t.Ethnicity.ToLower().Contains("chinese") &&
                     !t.Ethnicity.ToLower().Contains("tq") &&
                     !t.Ethnicity.ToLower().Contains("nước ngoài") &&
                     !t.Ethnicity.ToLower().Contains("foreign") &&
                     !t.Ethnicity.ToLower().Contains("lào") &&
                     !t.Ethnicity.ToLower().Contains("campuchia") &&
                     !t.Ethnicity.ToLower().Contains("thái lan"))
                ).ToList();

                var foreignTenants = allTenants.Where(t =>
                    !string.IsNullOrEmpty(t.Ethnicity) &&
                    (t.Ethnicity.ToLower().Contains("trung quốc") ||
                     t.Ethnicity.ToLower().Contains("china") ||
                     t.Ethnicity.ToLower().Contains("chinese") ||
                     t.Ethnicity.ToLower().Contains("tq") ||
                     t.Ethnicity.ToLower().Contains("nước ngoài") ||
                     t.Ethnicity.ToLower().Contains("foreign") ||
                     t.Ethnicity.ToLower().Contains("lào") ||
                     t.Ethnicity.ToLower().Contains("campuchia") ||
                     t.Ethnicity.ToLower().Contains("thái lan"))
                ).ToList();

                // Tạo file Excel với FlexCel - luôn tạo 2 sheets
                XlsFile xls = new XlsFile(2, true);

                // ===== ĐỊNH NGHĨA CÁC FORMAT DÙNG CHUNG =====
                // Format tiêu đề chính
                TFlxFormat titleFormat = xls.GetDefaultFormat;
                titleFormat.Font.Name = "Times New Roman";
                titleFormat.Font.Style = TFlxFontStyles.Bold;
                titleFormat.HAlignment = THFlxAlignment.center;
                titleFormat.VAlignment = TVFlxAlignment.center;
                titleFormat.WrapText = true;
                int titleFormatIdx = xls.AddFormat(titleFormat);

                // Format ngày giờ
                TFlxFormat dateFormat = xls.GetDefaultFormat;
                dateFormat.Font.Name = "Times New Roman";
                dateFormat.Font.Style = TFlxFontStyles.Italic;
                int dateFormatIdx = xls.AddFormat(dateFormat);

                // Format header
                TFlxFormat headerFormat = xls.GetDefaultFormat;
                headerFormat.Font.Name = "Times New Roman";
                headerFormat.Font.Style = TFlxFontStyles.Bold;
                headerFormat.HAlignment = THFlxAlignment.center;
                headerFormat.VAlignment = TVFlxAlignment.center;
                headerFormat.Borders.Left.Style = TFlxBorderStyle.Thin;
                headerFormat.Borders.Left.Color = Color.Black;
                headerFormat.Borders.Right.Style = TFlxBorderStyle.Thin;
                headerFormat.Borders.Right.Color = Color.Black;
                headerFormat.Borders.Top.Style = TFlxBorderStyle.Medium;
                headerFormat.Borders.Top.Color = Color.Black;
                headerFormat.Borders.Bottom.Style = TFlxBorderStyle.Medium;
                headerFormat.Borders.Bottom.Color = Color.Black;
                headerFormat.WrapText = true;
                int headerFormatIdx = xls.AddFormat(headerFormat);

                // Format data
                TFlxFormat dataFormat = xls.GetDefaultFormat;
                dataFormat.Font.Name = "Times New Roman";
                dataFormat.Borders.Left.Style = TFlxBorderStyle.Thin;
                dataFormat.Borders.Left.Color = Color.Black;
                dataFormat.Borders.Right.Style = TFlxBorderStyle.Thin;
                dataFormat.Borders.Right.Color = Color.Black;
                dataFormat.Borders.Top.Style = TFlxBorderStyle.Thin;
                dataFormat.Borders.Top.Color = Color.Black;
                dataFormat.Borders.Bottom.Style = TFlxBorderStyle.Thin;
                dataFormat.Borders.Bottom.Color = Color.Black;
                dataFormat.VAlignment = TVFlxAlignment.center;
                int dataFormatIdx = xls.AddFormat(dataFormat);

                // Format center
                TFlxFormat centerFormat = dataFormat;
                centerFormat.HAlignment = THFlxAlignment.center;
                int centerFormatIdx = xls.AddFormat(centerFormat);

                // Format số phòng
                TFlxFormat roomFormat = dataFormat;
                //roomFormat.Font.Style = TFlxFontStyles.Bold;
                roomFormat.HAlignment = THFlxAlignment.center;
                roomFormat.VAlignment = TVFlxAlignment.center;
                int roomFormatIdx = xls.AddFormat(roomFormat);

                // Format dòng chẵn
                TFlxFormat evenRowFormat = dataFormat;
                int evenRowFormatIdx = xls.AddFormat(evenRowFormat);

                TFlxFormat evenCenterFormat = centerFormat;
                int evenCenterFormatIdx = xls.AddFormat(evenCenterFormat);

                // Format footer
                TFlxFormat footerFormat = xls.GetDefaultFormat;
                footerFormat.Font.Name = "Times New Roman";
                footerFormat.Font.Style = TFlxFontStyles.Bold;
                footerFormat.Font.Color = Color.Black;
                int footerFormatIdx = xls.AddFormat(footerFormat);

                // ========== SHEET 1: NGƯỜI VIỆT NAM ==========
                xls.ActiveSheet = 1;
                xls.SheetName = "Việt Nam";

                // Tiêu đề chính
                xls.MergeCells(1, 1, 2, 11);
                xls.SetCellValue(1, 1, "DANH SÁCH ĐĂNG KÝ Ở TẠI NHÀ TRỌ AN CƯ, TỔ DÂN PHỐ ĐÌNH NGỌ, PHƯỜNG AN PHONG");
                xls.SetCellFormat(1, 1, titleFormatIdx);

                // Thông tin thời gian
                xls.SetCellValue(3, 1, $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
                xls.SetCellFormat(3, 1, dateFormatIdx);
                xls.MergeCells(3, 1, 3, 11);

                // Headers
                var headers = new[] {
            "STT", "Số phòng", "Họ và Tên", "Năm sinh",
            "Số điện thoại", "Giới tính", "Số giấy tờ",
            "Dân tộc", "Địa chỉ thường trú", "Biển số xe", "Ghi chú"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    xls.SetCellValue(4, i + 1, headers[i]);
                    xls.SetCellFormat(4, i + 1, headerFormatIdx);
                }

                // Data rows cho người Việt Nam
                int row = 5;
                int stt = 1;
                Dictionary<string, List<int>> roomRowRanges = new Dictionary<string, List<int>>();

                foreach (var tenant in vietnameseTenants)
                {
                    if (!roomRowRanges.ContainsKey(tenant.RoomName))
                        roomRowRanges[tenant.RoomName] = new List<int>();
                    roomRowRanges[tenant.RoomName].Add(row);

                    bool isEvenRow = (row - 5) % 2 == 1;
                    int rowDataFormat = isEvenRow ? evenRowFormatIdx : dataFormatIdx;
                    int rowCenterFormat = isEvenRow ? evenCenterFormatIdx : centerFormatIdx;

                    xls.SetCellValue(row, 1, stt++);
                    xls.SetCellFormat(row, 1, rowCenterFormat);

                    xls.SetCellValue(row, 2, tenant.RoomName);
                    xls.SetCellFormat(row, 2, roomFormatIdx);

                    xls.SetCellValue(row, 3, tenant.TenantName ?? "");
                    xls.SetCellFormat(row, 3, rowCenterFormat);

                    xls.SetCellValue(row, 4, tenant.BirthDate?.ToString("dd/MM/yyyy") ?? "");
                    xls.SetCellFormat(row, 4, rowCenterFormat);

                    xls.SetCellValue(row, 5, tenant.PhoneNumber ?? "");
                    xls.SetCellFormat(row, 5, rowCenterFormat);

                    xls.SetCellValue(row, 6, tenant.Gender ?? "");
                    xls.SetCellFormat(row, 6, rowCenterFormat);

                    xls.SetCellValue(row, 7, tenant.IdentityCard ?? "");
                    xls.SetCellFormat(row, 7, rowCenterFormat);

                    xls.SetCellValue(row, 8, tenant.Ethnicity ?? "Kinh");
                    xls.SetCellFormat(row, 8, rowCenterFormat);

                    xls.SetCellValue(row, 9, tenant.PermanentAddress ?? "");
                    xls.SetCellFormat(row, 9, rowCenterFormat);

                    xls.SetCellValue(row, 10, tenant.VehiclePlate ?? "");
                    xls.SetCellFormat(row, 10, rowCenterFormat);

                    xls.SetCellValue(row, 11, "");
                    xls.SetCellFormat(row, 11, rowCenterFormat);

                    row++;
                }

                // Merge cells cho cùng phòng
                foreach (var roomGroup in roomRowRanges.Where(g => g.Value.Count > 1))
                {
                    int startRow = roomGroup.Value.Min();
                    int endRow = roomGroup.Value.Max();
                    xls.MergeCells(startRow, 2, endRow, 2);
                }

                // Footer
                row++;
                xls.SetCellValue(row, 1, $"Tổng số người: {vietnameseTenants.Count}");
                xls.MergeCells(row, 1, row, 3);
                xls.SetCellFormat(row, 1, footerFormatIdx);

                row++;
                xls.SetCellValue(row, 1, $"Tổng số phòng: {roomRowRanges.Count}");
                xls.MergeCells(row, 1, row, 3);
                xls.SetCellFormat(row, 1, footerFormatIdx);

                // Column widths
                xls.SetColWidth(1, 1, 1536);   // STT
                xls.SetColWidth(2, 2, 2560);   // Số phòng
                xls.SetColWidth(3, 3, 5632);   // Họ và Tên
                xls.SetColWidth(4, 4, 2816);   // Năm sinh
                xls.SetColWidth(5, 5, 3584);   // Số điện thoại
                xls.SetColWidth(6, 6, 2048);   // Giới tính
                xls.SetColWidth(7, 7, 3840);   // Số CCCD
                xls.SetColWidth(8, 8, 2304);   // Dân tộc
                xls.SetColWidth(9, 9, 12240);  // Địa chỉ
                xls.SetColWidth(10, 10, 3072); // Biển xe
                xls.SetColWidth(11, 11, 3120); // Ghi chú

                // Page setup
                xls.PrintLandscape = true;
                xls.PrintPaperSize = TPaperSize.A4;
                xls.PrintOptions = TPrintOptions.None;
                xls.PrintScale = 100;
                xls.PageHeader = "&C&\"Times New Roman,Bold\"&14DANH SÁCH NGƯỜI VIỆT NAM";
                xls.PageFooter = "&L&\"Times New Roman\"&10Ngày in: &D &T" + "&C&P/&N" + "&R&\"Times New Roman\"&10Nhà Trọ An Cư";
                xls.SetPrintMargins(new TXlsMargins(0.7, 0.7, 0.7, 0.7, 0.3, 0.3));
                xls.FreezePanes(new TCellAddress(5, 1));

                // ========== SHEET 2: KHÁCH NƯỚC NGOÀI ==========
                xls.ActiveSheet = 2;
                xls.SheetName = "Nước ngoài";

                if (foreignTenants.Any())
                {
                    // Tiêu đề chính
                    xls.MergeCells(1, 1, 2, 11);
                    xls.SetCellValue(1, 1, "DANH SÁCH KHÁCH NƯỚC NGOÀI LƯU TRÚ TẠI NHÀ TRỌ AN CƯ");
                    xls.SetCellFormat(1, 1, titleFormatIdx);
                    // Thông tin thời gian
                    xls.SetCellValue(3, 1, $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
                    xls.SetCellFormat(3, 1, dateFormatIdx);
                    xls.MergeCells(3, 1, 3, 11);

                    // Headers cho khách nước ngoài
                    var foreignHeaders = new[] {
                "STT", "Số phòng", "Họ và Tên", "Năm sinh",
                "Giới tính", "Số giấy tờ",
                "Quốc tịch", "Địa chỉ"
            };

                    for (int i = 0; i < foreignHeaders.Length; i++)
                    {
                        xls.SetCellValue(4, i + 1, foreignHeaders[i]);
                        xls.SetCellFormat(4, i + 1, headerFormatIdx);
                    }

                    // Data rows cho khách nước ngoài
                    int foreignRow = 5;
                    int foreignStt = 1;
                    Dictionary<string, List<int>> foreignRoomRows = new Dictionary<string, List<int>>();

                    foreach (var tenant in foreignTenants)
                    {
                        if (!foreignRoomRows.ContainsKey(tenant.RoomName))
                            foreignRoomRows[tenant.RoomName] = new List<int>();
                        foreignRoomRows[tenant.RoomName].Add(foreignRow);

                        bool isEvenRow = (foreignRow - 5) % 2 == 1;
                        int rowDataFormat = isEvenRow ? evenRowFormatIdx : dataFormatIdx;
                        int rowCenterFormat = isEvenRow ? evenCenterFormatIdx : centerFormatIdx;

                        xls.SetCellValue(foreignRow, 1, foreignStt++);
                        xls.SetCellFormat(foreignRow, 1, rowCenterFormat);

                        xls.SetCellValue(foreignRow, 2, tenant.RoomName);
                        xls.SetCellFormat(foreignRow, 2, roomFormatIdx);

                        xls.SetCellValue(foreignRow, 3, tenant.TenantName ?? "");
                        xls.SetCellFormat(foreignRow, 3, rowCenterFormat);

                        xls.SetCellValue(foreignRow, 4, tenant.BirthDate?.ToString("dd/MM/yyyy") ?? "");
                        xls.SetCellFormat(foreignRow, 4, rowCenterFormat);

                        xls.SetCellValue(foreignRow, 5, tenant.Gender ?? "");
                        xls.SetCellFormat(foreignRow, 5, rowCenterFormat);

                        xls.SetCellValue(foreignRow, 6, tenant.IdentityCard ?? "");
                        xls.SetCellFormat(foreignRow, 6, rowCenterFormat);

                        // Xác định quốc tịch từ Ethnicity
                        string nationality = "Trung Quốc";
                        if (!string.IsNullOrEmpty(tenant.Ethnicity))
                        {
                            var eth = tenant.Ethnicity.ToLower();
                            if (eth.Contains("trung quốc") || eth.Contains("china") || eth.Contains("tq"))
                                nationality = "Trung Quốc";
                            else if (eth.Contains("lào"))
                                nationality = "Lào";
                            else if (eth.Contains("campuchia"))
                                nationality = "Campuchia";
                            else if (eth.Contains("thái lan"))
                                nationality = "Thái Lan";
                            else
                                nationality = tenant.Ethnicity;
                        }
                        xls.SetCellValue(foreignRow, 7, nationality);
                        xls.SetCellFormat(foreignRow, 7, rowCenterFormat);

                        xls.SetCellValue(foreignRow, 8, tenant.PermanentAddress ?? "");
                        xls.SetCellFormat(foreignRow, 8, rowCenterFormat);

                        foreignRow++;
                    }

                    // Merge cells cho cùng phòng
                    foreach (var roomGroup in foreignRoomRows.Where(g => g.Value.Count > 1))
                    {
                        int startRow = roomGroup.Value.Min();
                        int endRow = roomGroup.Value.Max();
                        xls.MergeCells(startRow, 2, endRow, 2);
                    }

                    // Footer
                    foreignRow++;
                    xls.SetCellValue(foreignRow, 1, $"Tổng số khách nước ngoài: {foreignTenants.Count}");
                    xls.MergeCells(foreignRow, 1, foreignRow, 3);
                    xls.SetCellFormat(foreignRow, 1, footerFormatIdx);

                    foreignRow++;
                    xls.SetCellValue(foreignRow, 1, $"Số phòng có khách nước ngoài: {foreignRoomRows.Count}");
                    xls.MergeCells(foreignRow, 1, foreignRow, 3);
                    xls.SetCellFormat(foreignRow, 1, footerFormatIdx);

                    foreignRow++;

                    // Column widths
                    xls.SetColWidth(1, 1, 1536);   // STT
                    xls.SetColWidth(2, 2, 2560);   // Số phòng
                    xls.SetColWidth(3, 3, 5632);   // Họ và Tên
                    xls.SetColWidth(4, 4, 2816);   // Năm sinh                    
                    xls.SetColWidth(5, 5, 2048);   // Giới tính
                    xls.SetColWidth(6, 6, 3840);   // Số hộ chiếu
                    xls.SetColWidth(7, 7, 2560);   // Quốc tịch
                    xls.SetColWidth(8, 8, 12240);  // Địa chỉ                    

                    // Page setup
                    xls.PrintLandscape = true;
                    xls.PrintPaperSize = TPaperSize.A4;
                    xls.PrintOptions = TPrintOptions.None;
                    xls.PrintScale = 100;
                    xls.PageHeader = "&C&\"Times New Roman,Bold\"&14DANH SÁCH KHÁCH NƯỚC NGOÀI";
                    xls.PageFooter = "&L&\"Times New Roman\"&10Ngày in: &D &T" + "&C&P/&N" + "&R&\"Times New Roman\"&10Nhà Trọ An Cư";
                    xls.SetPrintMargins(new TXlsMargins(0.7, 0.7, 0.7, 0.7, 0.3, 0.3));
                    xls.FreezePanes(new TCellAddress(5, 1));
                }
                else
                {
                    // Nếu không có khách nước ngoài, hiển thị thông báo
                    xls.SetCellValue(1, 1, "Không có khách nước ngoài");
                    xls.SetCellFormat(1, 1, titleFormatIdx);
                }

                // Quay lại sheet 1 là active sheet
                xls.ActiveSheet = 1;

                // Save to memory stream
                using (MemoryStream ms = new MemoryStream())
                {
                    xls.Save(ms, TFileFormats.Xlsx);
                    ms.Position = 0;

                    string fileName = $"DanhSachKhachThue_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi xuất Excel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        public ActionResult TenantManager(int contractId, int roomId)
        {
            // Validate contract and room exist
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng!";
                return RedirectToAction("Index", "Contracts");
            }

            // Check if room belongs to this contract
            var contractRoom = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId);

            if (contractRoom == null)
            {
                TempData["Error"] = "Phòng không thuộc hợp đồng này!";
                return RedirectToAction("Details", "Contracts", new { id = contractId });
            }

            // Get existing tenants for this room and contract
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .ToList();

            var model = new TenantManagerViewModel
            {
                ContractId = contractId,
                RoomId = roomId,
                ContractCode = $"HD-{contract.Id:D6}",
                RoomName = contractRoom.Room.Name,
                CompanyName = contract.Company?.CompanyName ?? "Cá nhân",
                CompanyId = contract.CompanyId,
                ContractTenants = contractTenants.Select(ct => new TenantViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    FullName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    Gender = ct.Tenant.Gender,
                    BirthDate = ct.Tenant.BirthDate,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    Ethnicity = ct.Tenant.Ethnicity,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,
                    CreatedAt = ct.CreatedAt
                }).ToList()
            };

            return View(model);
        }

        // POST: TenantContracts/SaveTenants
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTenants(TenantManagerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadViewData(model);
                return View("TenantManager", model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contract = db.Contracts
                        .Include(c => c.ContractRooms)
                        .FirstOrDefault(c => c.Id == model.ContractId);

                    if (contract == null)
                    {
                        throw new InvalidOperationException("Không tìm thấy hợp đồng");
                    }

                    // Validate room belongs to contract
                    if (!contract.ContractRooms.Any(cr => cr.RoomId == model.RoomId))
                    {
                        throw new InvalidOperationException("Phòng không thuộc hợp đồng này");
                    }

                    // Convert Tenants to TenantViewModel for processing
                    var tenantViewModels = model.Tenants?.Select(t => new TenantViewModel
                    {
                        TenantId = t.Id,
                        FullName = t.FullName,
                        IdentityCard = t.IdentityCard,
                        PhoneNumber = t.PhoneNumber,
                        BirthDate = t.BirthDate,
                        Gender = t.Gender,
                        PermanentAddress = t.PermanentAddress,
                        Ethnicity = t.Ethnicity,
                        VehiclePlate = t.VehiclePlate,
                        Photo = t.Photo
                    }).ToList();

                    // Process tenants based on contract type
                    if (contract.ContractType == "Company")
                    {
                        // Với hợp đồng công ty, truyền roomId cụ thể
                        TenantContractHelper.ProcessCompanyContractTenants(
                            db,
                            contract,
                            model.RoomId,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }
                    else
                    {
                        // Với hợp đồng cá nhân, không cần roomId vì chỉ có 1 phòng
                        TenantContractHelper.ProcessIndividualContractTenants(
                            db,
                            contract,
                            tenantViewModels,
                            Request,
                            true // isEdit mode
                        );
                    }

                    transaction.Commit();
                    TempData["Success"] = "Đã cập nhật thông tin người thuê thành công!";
                    return RedirectToAction("Details", "Rooms", new { id = model.RoomId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    LoadViewData(model);
                    return View("TenantManager", model);
                }
            }
        }

        private void LoadViewData(TenantManagerViewModel model)
        {
            // Reload contract and room info for display
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == model.ContractId);

            if (contract != null)
            {
                var room = contract.ContractRooms
                    .FirstOrDefault(cr => cr.RoomId == model.RoomId)?.Room;

                if (room != null)
                {
                    model.ContractCode = $"HD-{contract.Id:D6}";
                    model.RoomName = room.Name;
                    model.CompanyName = contract.Company?.CompanyName ?? "Cá nhân";
                    model.CompanyId = contract.CompanyId;
                }
            }
        }

        // POST: TenantContracts/RemoveTenant
        [HttpPost]
        public ActionResult RemoveTenant(int contractTenantId)
        {
            try
            {
                var contractTenant = db.ContractTenants.Find(contractTenantId);
                if (contractTenant == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người thuê" });
                }

                db.ContractTenants.Remove(contractTenant);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa người thuê" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: TenantContracts/GetTenants
        public ActionResult GetTenants(int contractId, int roomId)
        {
            var contractTenants = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Where(ct => ct.ContractId == contractId && ct.RoomId == roomId)
                .Select(ct => new
                {
                    id = ct.Id,
                    tenantId = ct.TenantId,
                    contractTenantId = ct.Id,
                    fullName = ct.Tenant.FullName,
                    identityCard = ct.Tenant.IdentityCard,
                    phoneNumber = ct.Tenant.PhoneNumber,
                    gender = ct.Tenant.Gender,
                    birthDate = ct.Tenant.BirthDate,
                    permanentAddress = ct.Tenant.PermanentAddress,
                    ethnicity = ct.Tenant.Ethnicity,
                    vehiclePlate = ct.Tenant.VehiclePlate,
                    photo = ct.Tenant.Photo,
                    createdAt = ct.CreatedAt
                })
                .ToList();

            return Json(contractTenants, JsonRequestBehavior.AllowGet);
        }

        // GET: TenantContracts/GetRoomContractInfo
        public ActionResult GetRoomContractInfo(int contractId, int roomId)
        {
            var contract = db.Contracts
                .Include(c => c.Company)
                .Include(c => c.ContractRooms.Select(cr => cr.Room))
                .FirstOrDefault(c => c.Id == contractId);

            if (contract == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            var room = contract.ContractRooms
                .FirstOrDefault(cr => cr.RoomId == roomId)?.Room;

            if (room == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                roomName = room.Name,
                contractCode = $"HD-{contract.Id:D6}",
                companyName = contract.Company?.CompanyName ?? "Cá nhân"
            }, JsonRequestBehavior.AllowGet);
        }

        private void UpdateTenantInfo(Tenant tenant, TenantDataViewModel data)
        {
            tenant.FullName = data.FullName;
            tenant.IdentityCard = data.IdentityCard;
            tenant.PhoneNumber = data.PhoneNumber;
            tenant.Gender = data.Gender;
            tenant.BirthDate = data.BirthDate;
            tenant.PermanentAddress = data.PermanentAddress;
            tenant.Ethnicity = data.Ethnicity;
            tenant.VehiclePlate = data.VehiclePlate;

            // Handle photo if provided (base64 or file path)
            if (!string.IsNullOrEmpty(data.Photo))
            {
                tenant.Photo = data.Photo;
            }
        }


        // GET: ContractTenants
        public ActionResult Index(string searchName, string searchCard, string searchRoom,
             string filterStatus, string filterCompany, string filterContractType)
        {
            var query = db.ContractTenants
                .Include(ct => ct.Tenant)
                .Include(ct => ct.Room)
                .Include(ct => ct.Contract)
                .Include(ct => ct.Contract.Company)
                .AsQueryable();

            // Lọc theo trạng thái hợp đồng
            if (!string.IsNullOrEmpty(filterStatus))
            {
                query = query.Where(ct => ct.Contract.Status == filterStatus);
            }
            else
            {
                // Mặc định chỉ hiển thị hợp đồng Active
                query = query.Where(ct => ct.Contract.Status == "Active");
            }

            // Lọc theo loại hợp đồng
            if (!string.IsNullOrEmpty(filterContractType))
            {
                query = query.Where(ct => ct.Contract.ContractType == filterContractType);
            }

            // Lọc theo công ty (cho hợp đồng công ty)
            if (!string.IsNullOrEmpty(filterCompany))
            {
                int companyId = int.Parse(filterCompany);
                query = query.Where(ct => ct.Contract.CompanyId == companyId);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(ct => ct.Tenant.FullName.Contains(searchName));
            }
            if (!string.IsNullOrEmpty(searchCard))
            {
                query = query.Where(ct => ct.Tenant.IdentityCard.Contains(searchCard));
            }
            if (!string.IsNullOrEmpty(searchRoom))
            {
                query = query.Where(ct => ct.Room.Name.Contains(searchRoom));
            }

            var result = query
                .OrderBy(ct => ct.Room.Name)
                .ThenBy(ct => ct.Tenant.FullName)
                .Select(ct => new TenantReportViewModel
                {
                    Id = ct.Id,
                    TenantId = ct.TenantId,
                    TenantName = ct.Tenant.FullName,
                    IdentityCard = ct.Tenant.IdentityCard,
                    PhoneNumber = ct.Tenant.PhoneNumber,
                    BirthDate = ct.Tenant.BirthDate,
                    Gender = ct.Tenant.Gender,
                    Ethnicity = ct.Tenant.Ethnicity,
                    PermanentAddress = ct.Tenant.PermanentAddress,
                    VehiclePlate = ct.Tenant.VehiclePlate,
                    Photo = ct.Tenant.Photo,

                    RoomId = ct.RoomId,
                    RoomName = ct.Room.Name,

                    ContractId = ct.ContractId,
                    ContractType = ct.Contract.ContractType,
                    ContractStatus = ct.Contract.Status,
                    StartDate = ct.Contract.StartDate,
                    EndDate = ct.Contract.EndDate,
                    MoveInDate = ct.Contract.MoveInDate,

                    CompanyId = ct.Contract.CompanyId,
                    CompanyName = ct.Contract.Company != null ? ct.Contract.Company.CompanyName : null,

                })
                .ToList();

            // Chuẩn bị dữ liệu cho filters
            ViewBag.Companies = db.Companies
                .OrderBy(c => c.CompanyName)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.CompanyName
                })
                .ToList();

            ViewBag.ContractTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Individual", Text = "Cá nhân/Hộ gia đình" },
                new SelectListItem { Value = "Company", Text = "Công ty" }
            };

            ViewBag.Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "Active", Text = "Đang ở" },
                new SelectListItem { Value = "Expired", Text = "Hết hạn" },
                new SelectListItem { Value = "Terminated", Text = "Đã kết thúc" }
            };

            // Truyền các giá trị filter để maintain state
            ViewBag.SearchName = searchName;
            ViewBag.SearchCard = searchCard;
            ViewBag.SearchRoom = searchRoom;
            ViewBag.FilterStatus = filterStatus;
            ViewBag.FilterCompany = filterCompany;
            ViewBag.FilterContractType = filterContractType;

            // Thống kê
            ViewBag.TotalTenants = result.Count;
            ViewBag.TotalRooms = result.Select(r => r.RoomId).Distinct().Count();
            ViewBag.TotalCompanies = result.Where(r => r.CompanyId.HasValue)
                .Select(r => r.CompanyId).Distinct().Count();

            return View(result);
        }

        // GET: ContractTenants/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // GET: ContractTenants/Create
        public ActionResult Create()
        {
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status");
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name");
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName");
            return View();
        }

        // POST: ContractTenants/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.ContractTenants.Add(contractTenant);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // POST: ContractTenants/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,RoomId,TenantId,ContractId,CreatedAt")] ContractTenant contractTenant)
        {
            if (ModelState.IsValid)
            {
                db.Entry(contractTenant).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.ContractId = new SelectList(db.Contracts, "Id", "Status", contractTenant.ContractId);
            ViewBag.RoomId = new SelectList(db.Rooms, "Id", "Name", contractTenant.RoomId);
            ViewBag.TenantId = new SelectList(db.Tenants, "Id", "FullName", contractTenant.TenantId);
            return View(contractTenant);
        }

        // GET: ContractTenants/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            if (contractTenant == null)
            {
                return HttpNotFound();
            }
            return View(contractTenant);
        }

        // POST: ContractTenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ContractTenant contractTenant = db.ContractTenants.Find(id);
            db.ContractTenants.Remove(contractTenant);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // GET: ContractTenants/Export - Xuất báo cáo Excel
        // GET: ContractTenants/Export - Xuất báo cáo PDF
        public ActionResult Export(string searchName, string searchCard, string searchRoom,
            string filterStatus, string filterCompany, string filterContractType)
        {
            try
            {
                // Build query giống như trong Index
                var query = from ct in db.ContractTenants
                            join t in db.Tenants on ct.TenantId equals t.Id
                            join r in db.Rooms on ct.RoomId equals r.Id
                            join c in db.Contracts on ct.ContractId equals c.Id
                            join cr in db.ContractRooms on new { ct.ContractId, ct.RoomId }
                                equals new { cr.ContractId, cr.RoomId }
                            select new TenantReportViewModel
                            {
                                Id = ct.Id,
                                TenantId = t.Id,
                                TenantName = t.FullName,
                                IdentityCard = t.IdentityCard,
                                PhoneNumber = t.PhoneNumber,
                                BirthDate = t.BirthDate,
                                Gender = t.Gender,
                                Ethnicity = t.Ethnicity,
                                PermanentAddress = t.PermanentAddress,
                                VehiclePlate = t.VehiclePlate,
                                Photo = t.Photo,
                                RoomId = r.Id,
                                RoomName = r.Name,
                                ContractId = c.Id,
                                ContractType = c.ContractType,
                                ContractStatus = c.Status,
                                MoveInDate = c.MoveInDate,
                                CompanyId = c.CompanyId,
                                CompanyName = c.Company.CompanyName
                            };

                // Apply filters - nếu filterStatus rỗng thì mặc định lấy Active
                if (string.IsNullOrEmpty(filterStatus))
                {
                    query = query.Where(x => x.ContractStatus == "Active");
                }
                else
                {
                    query = query.Where(x => x.ContractStatus == filterStatus);
                }

                // Apply other filters
                if (!string.IsNullOrEmpty(searchName))
                {
                    query = query.Where(x => x.TenantName.Contains(searchName));
                }

                if (!string.IsNullOrEmpty(searchCard))
                {
                    query = query.Where(x => x.IdentityCard.Contains(searchCard));
                }

                if (!string.IsNullOrEmpty(searchRoom))
                {
                    query = query.Where(x => x.RoomName.Contains(searchRoom));
                }

                if (!string.IsNullOrEmpty(filterContractType))
                {
                    query = query.Where(x => x.ContractType == filterContractType);
                }

                if (!string.IsNullOrEmpty(filterCompany))
                {
                    int companyId = int.Parse(filterCompany);
                    query = query.Where(x => x.CompanyId == companyId);
                }

                var data = query.OrderBy(x => x.RoomName).ThenBy(x => x.TenantName).ToList();

                // Tạo PDF document sử dụng iTextSharp
                using (var memoryStream = new MemoryStream())
                {
                    // Khởi tạo document
                    var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate(), 25, 25, 30, 30);
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, memoryStream);

                    // Thêm metadata
                    document.AddAuthor("Nhà Trọ An Cư");
                    document.AddCreator("Hệ thống quản lý nhà trọ");
                    document.AddSubject("Danh sách khách thuê");
                    document.AddTitle("Báo cáo danh sách khách thuê");

                    document.Open();

                    // Font tiếng Việt
                    string fontPath = Server.MapPath("~/Fonts/times.ttf");
                    var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath,
                        iTextSharp.text.pdf.BaseFont.IDENTITY_H,
                        iTextSharp.text.pdf.BaseFont.EMBEDDED);

                    var titleFont = new iTextSharp.text.Font(baseFont, 18, iTextSharp.text.Font.BOLD);
                    var headerFont = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.BOLD);
                    var normalFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL);
                    var boldFont = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.BOLD);
                    var smallFont = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL);

                    // Title
                    var title = new iTextSharp.text.Paragraph("DANH SÁCH KHÁCH THUÊ", titleFont);
                    title.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                    title.SpacingAfter = 10f;
                    document.Add(title);

                    // Thông tin xuất
                    var exportInfo = new iTextSharp.text.Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}", normalFont);
                    exportInfo.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                    exportInfo.SpacingAfter = 5f;
                    document.Add(exportInfo);

                    if (!string.IsNullOrEmpty(filterStatus))
                    {
                        var statusText = filterStatus == "Active" ? "Đang ở" :
                                        filterStatus == "Expired" ? "Hết hạn" : "Đã kết thúc";
                        var statusInfo = new iTextSharp.text.Paragraph($"Trạng thái: {statusText}", normalFont);
                        statusInfo.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        statusInfo.SpacingAfter = 10f;
                        document.Add(statusInfo);
                    }

                    // Tạo bảng
                    var table = new iTextSharp.text.pdf.PdfPTable(12);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 3f, 12f, 10f, 10f, 8f, 5f, 8f, 8f, 12f, 8f, 8f, 8f });
                    table.SpacingBefore = 10f;

                    // Header row
                    AddTableHeader(table, "STT", headerFont);
                    AddTableHeader(table, "Họ tên", headerFont);
                    AddTableHeader(table, "CCCD", headerFont);
                    AddTableHeader(table, "Điện thoại", headerFont);
                    AddTableHeader(table, "Ngày sinh", headerFont);
                    AddTableHeader(table, "Giới tính", headerFont);
                    AddTableHeader(table, "Phòng", headerFont);
                    AddTableHeader(table, "Loại HĐ", headerFont);
                    AddTableHeader(table, "Công ty", headerFont);
                    AddTableHeader(table, "Trạng thái", headerFont);
                    AddTableHeader(table, "Ngày vào", headerFont);
                    AddTableHeader(table, "Chính", headerFont);

                    // Data rows
                    int stt = 1;
                    foreach (var item in data)
                    {
                        AddTableCell(table, stt++.ToString(), smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.TenantName, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.IdentityCard, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.PhoneNumber, smallFont, iTextSharp.text.Element.ALIGN_LEFT);
                        AddTableCell(table, item.BirthDate?.ToString("dd/MM/yyyy") ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.Gender ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.RoomName, smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.ContractType == "Individual" ? "Cá nhân" : "Công ty", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.CompanyName ?? "-", smallFont, iTextSharp.text.Element.ALIGN_LEFT);

                        string status = item.ContractStatus == "Active" ? "Đang ở" :
                                       item.ContractStatus == "Expired" ? "Hết hạn" : "Kết thúc";
                        AddTableCell(table, status, smallFont, iTextSharp.text.Element.ALIGN_CENTER);

                        AddTableCell(table, item.MoveInDate?.ToString("dd/MM/yyyy") ?? "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                        AddTableCell(table, item.IsPrimary ? "✓" : "", smallFont, iTextSharp.text.Element.ALIGN_CENTER);
                    }

                    document.Add(table);

                    // Đường kẻ
                    document.Add(new iTextSharp.text.Paragraph(" "));
                    var line = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.GRAY,
                        iTextSharp.text.Element.ALIGN_CENTER, -2);
                    document.Add(new iTextSharp.text.Chunk(line));
                    document.Add(new iTextSharp.text.Paragraph(" "));

                    // Footer với thống kê
                    var footer = new iTextSharp.text.Paragraph("THỐNG KÊ:", boldFont);
                    footer.SpacingAfter = 5f;
                    document.Add(footer);

                    var stats = new iTextSharp.text.Paragraph();
                    stats.Add(new iTextSharp.text.Chunk($"- Tổng số người: {data.Count}\n", normalFont));
                    stats.Add(new iTextSharp.text.Chunk($"- Số phòng: {data.Select(x => x.RoomId).Distinct().Count()}\n", normalFont));

                    if (data.Any(x => x.ContractType == "Company"))
                    {
                        stats.Add(new iTextSharp.text.Chunk($"- Số công ty: {data.Where(x => x.CompanyId.HasValue).Select(x => x.CompanyId).Distinct().Count()}", normalFont));
                    }
                    document.Add(stats);

                    document.Close();
                    writer.Close();

                    // Trả về file PDF
                    byte[] bytes = memoryStream.ToArray();
                    var fileName = $"DanhSachKhachThue_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(bytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xuất file: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Helper methods cho bảng PDF
        private void AddTableHeader(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.BackgroundColor = new iTextSharp.text.BaseColor(230, 230, 230);
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddTableCell(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font, int alignment)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
            cell.Padding = 4;
            table.AddCell(cell);
        }
    }
}
