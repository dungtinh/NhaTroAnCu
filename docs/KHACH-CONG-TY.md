# Khách công ty (hợp đồng Horn) – phương án xử lý

Bản nghiên cứu ngày 22/10/2026, từ hai văn bản trong `temps/HĐ khách trung quốc/` (bản scan, không có lớp chữ):
hợp đồng số 01/HĐTN-Horn ký 10/09/2025 và phụ lục số 03.2026/Horn-TND.

## 1. Nội dung hợp đồng và phụ lục

| Nội dung | Hợp đồng 01/HĐTN-Horn (10/09/2025) | Phụ lục 03.2026 (từ 10/09/2026) |
|---|---|---|
| Bên thuê | Công ty TNHH Horn (Việt Nam), đại diện Tổng giám đốc | như cũ |
| Phòng | 20 phòng: B1.1, B1.2, B1.7, B1.8, B2.1–B2.8, B3.1–B3.8 | **5 phòng: B3.1–B3.5** |
| Giá | 2.200.000đ/phòng/tháng = 44.000.000đ/tháng | 2.200.000đ/phòng/tháng = **11.000.000đ/tháng** |
| Cọc | 3 tháng = 132.000.000đ | 3 tháng = **33.000.000đ** |
| Thời hạn | 10/09/2025 → 09/09/2026 | **10/09/2026 → 09/09/2027** |
| Tiền phòng | Trả trước **từng quý** (3 tháng), chuyển khoản | như cũ |
| Lịch nộp | Quý 1 (10/09–09/12): nộp 20–30/09 · Quý 2 (10/12–09/03): nộp 10–25/12 · Quý 3 (10/03–09/06): nộp 10–25/03 · Quý 4 (10/06–09/09): nộp 10–25/06 | Quý 1 (10/09–09/12/2026): nộp **20/09–05/10/2026** · Quý 2: nộp 10–25/12/2026 · Quý 3: nộp 10–25/03/2027 · Quý 4: nộp 10–25/06/2027 |
| Điện, nước | Bên thuê trả cho chủ nhà **hằng tháng** (điều II.1) | như cũ |
| Người ở | Tối đa **2 người/phòng** (điều VIII.2), là nhân viên công ty, công ty phối hợp khai báo tạm trú | như cũ |
| Chậm nộp | Quá 7 ngày kể từ ngày 25 tháng đầu quý → chủ nhà được đơn phương chấm dứt | như cũ |
| Giảm phòng / chấm dứt | Báo trước 1 tháng; năm đầu phạt 1 tháng cọc của phòng bỏ; từ năm sau không phạt | như cũ |

Theo lời anh Duy (22/10/2026): **nước 100.000đ/phòng/tháng** (cố định, không theo người), **điện như bình thường** (kWh × giá điện),
tiền phòng **không thu hằng tháng** mà thu theo kỳ của hợp đồng/phụ lục, người ở là người của công ty, thỉnh thoảng đổi người.

Điểm thuận lợi: kỳ quý của Horn bắt đầu **ngày 10**, trùng ngày chốt (mồng 10) của phần mềm. Tiền điện, nước vẫn chốt mồng 10 như mọi phòng.

## 2. Hiện trạng trong phần mềm

- B3.1–B3.5 đang là **5 lượt thuê riêng lẻ** (nhập ngày 19/10/2026, "Khách cũ", ngày vào 10/10/2026), mỗi phòng một người
  "Khách Trung Quốc (chưa có tên)" với số CCCD tạm, cọc ghi **1.000.000đ/phòng** ở TK cá nhân, giá 2.200.000đ.
- Nếu để nguyên, kỳ 11/2026 phần mềm sẽ lập 5 phiếu tháng: **2.200.000đ tiền phòng + điện + nước 80.000đ × số người**, sai cả 3 khoản
  so với hợp đồng (tiền phòng thu theo quý, nước 100.000đ/phòng, cọc thực là 33.000.000đ).
- Không có khái niệm công ty, hợp đồng nhiều phòng, kỳ thanh toán nhiều tháng; không lưu được phụ lục.

## 3. Phương án đề xuất: "Hợp đồng công ty" gắn lên các lượt thuê

Ý tưởng: giữ nguyên mỗi phòng một lượt thuê (để sơ đồ, số điện, người ở, trả phòng, báo cáo theo phòng vẫn chạy như cũ),
thêm một đối tượng **Hợp đồng công ty** bao các lượt thuê đó và quyết định cách tính tiền.

### 3.1. Dữ liệu mới

**CompanyContract – Hợp đồng công ty** (một dòng cho Horn)
- Công ty: tên, mã số thuế, địa chỉ, người đại diện, chức vụ, điện thoại, tài khoản ngân hàng (để in phiếu, đối chiếu chuyển khoản).
- Số hợp đồng, ngày ký, **ghi chú**, scan hợp đồng và các phụ lục (Attachment, nhiều file).
- Cách tính: **tiền phòng theo kỳ N tháng** (Horn: 3), **nước cố định/phòng/tháng** (100.000đ; 0 = tính theo người như thường),
  điện theo kWh như thường, **số người tối đa/phòng** (2), không áp dụng quy định "ở đủ 3 tháng mới trả cọc".

**ContractTerm – Thời hạn (hợp đồng gốc và từng phụ lục)**, mỗi dòng:
- Số văn bản (01/HĐTN-Horn, PL 03.2026), ngày ký, **từ ngày – đến ngày**, giá/phòng/tháng, **cọc/phòng** (6.600.000đ), danh sách phòng,
  lịch nộp: ngày bắt đầu kỳ (10/09) và **cửa sổ nộp** của từng kỳ (từ ngày – đến ngày). Phụ lục mới = thêm một dòng, dòng cũ hết hiệu lực.

**Lease** thêm `CompanyContractId` (null = khách lẻ). Khi gắn vào hợp đồng công ty: `Deposit` = cọc/phòng của thời hạn hiện hành,
`Room.MaxOccupants` = 2.

**Bill** thêm loại `ContractRent` ("Tiền phòng kỳ hợp đồng"): **mỗi phòng một phiếu cho mỗi kỳ quý**
(B3.1 kỳ 10/12/2026–09/03/2027: 3 × 2.200.000 = 6.600.000đ), thêm `ContractPeriodKey` để gom 5 phiếu của cùng kỳ.
Giữ phiếu theo phòng để **doanh thu theo phòng**, màu nợ trên sơ đồ, trang Phòng/Hợp đồng dùng lại được y nguyên.

### 3.2. Cách tính tiền cho phòng thuộc hợp đồng công ty

| Khoản | Khách lẻ (như hiện nay) | Phòng Horn |
|---|---|---|
| Tiền phòng | Tròn tháng trên phiếu tháng, trả trước 1 tháng | **0 trên phiếu tháng**; thu bằng phiếu `ContractRent` theo quý, số tiền = số tháng × giá |
| Điện | kWh × 3.500 trên phiếu tháng | **như thường** |
| Nước | số người × 80.000 | **100.000đ/phòng** (vẫn hiện số người để biết, không nhân) |
| Cọc | 1.000.000 mặc định, ở đủ 3 tháng mới hoàn | 6.600.000/phòng (33.000.000 cả HĐ), hoàn theo hợp đồng, không áp quy định 3 tháng |
| Kỳ chốt đầu (đoạn lẻ) | có | **không** (hợp đồng bắt đầu đúng mồng 10) |

- Phiếu tháng của phòng Horn chỉ còn **điện + nước**, diễn giải tiền phòng ghi "Thu theo HĐ Horn, kỳ 10/12/2026–09/03/2027".
  Trang Ghi số điện, Thu tiền tháng, sơ đồ phòng không đổi gì về thao tác.
- Phiếu `ContractRent` được **tự sinh** khi tới ngày bắt đầu kỳ (10/12/2026…) theo lịch trong ContractTerm, hạn nộp = cuối cửa sổ nộp
  (25/12/2026). Quá hạn → phòng hiện màu nợ và trang chủ nhắc "Horn chưa nộp quý 10/12–09/03".
- **Thu gộp**: một lần chuyển khoản 33.000.000đ của Horn → nút "Thu tiền hợp đồng công ty" chia tự động vào 5 phiếu của kỳ
  (5 dòng thu cùng `TransferGroup`, chung một chứng từ), vào **TK hộ kinh doanh**. Điện nước hằng tháng cũng có thể thu gộp 5 phòng
  cùng cách này. Thu lẻ từng phiếu vẫn được.
- Báo cáo không đổi cách tính (thu thực tế theo tháng thu). Tiền quý rơi vào tháng nộp; nếu muốn xem doanh thu "theo tháng ở"
  thì thêm sau, chưa cần.

### 3.3. Người ở thay đổi

- Dùng ngay **"Sửa người ở"** trên trang Hợp đồng: thêm người mới (hộ chiếu + ảnh chân dung), bỏ người cũ → phần mềm ghi ngày vào/ra
  từng người. Tiền nước không phụ thuộc số người nên đổi người không ảnh hưởng tiền.
- Số CCCD/hộ chiếu vẫn bắt buộc (cần cho **khai báo tạm trú** người nước ngoài, hợp đồng điều V.1.d). Quét bằng AI hiện nhắm CCCD;
  bổ sung mẫu nhắc cho **hộ chiếu** (số, tên Latin, quốc tịch, ngày sinh) để tự điền.
- Trang Khách thuê: thêm lọc "theo công ty" và xuất danh sách người nước ngoài đang ở (đã có `ResidentRegisterAsync`) để nộp công an.

### 3.4. Giảm phòng, hết hạn, gia hạn

- **Giảm phòng** (Horn bỏ 1 phòng): trả phòng như thường (điện kỳ cuối, kiểm đồ, ảnh); cọc dùng để quyết toán là 6.600.000 của phòng đó;
  tiền phòng quý đã nộp **không hoàn** (hợp đồng không quy định hoàn), muốn hoàn thì ghi âm ở "khoản khác" như hiện nay.
  Phạt 1 tháng cọc năm đầu: nhập ở mục phạt.
- **Hết hạn / phụ lục mới**: thêm ContractTerm mới (ngày, giá, phòng); phòng không còn trong danh sách → nhắc trả phòng;
  phòng mới → nhận phòng gắn vào hợp đồng (bước Khách cho phép nhập "Nhân viên Horn (chưa có tên)" tạm, bổ sung sau).
- **Hợp đồng đã ký** do công ty soạn song ngữ: chỉ cần tải scan, không cần sinh từ mẫu Word.

### 3.5. Màn hình

- **Hợp đồng công ty** (`/hop-dong-cong-ty`): danh sách; trang chi tiết gồm thông tin công ty, thời hạn hiện hành + lịch sử phụ lục,
  5 phòng (liên kết tới trang phòng/hợp đồng từng phòng), bảng **kỳ thanh toán** (kỳ, số tiền, hạn nộp, đã thu, còn nợ, nút Thu gộp),
  scan hợp đồng/phụ lục. Nút "Thêm phụ lục".
- Trang Phòng, Hợp đồng của B3.x: thẻ "Hợp đồng Horn · nước 100.000đ/phòng · tiền phòng theo quý", link sang hợp đồng công ty.
- Trang chủ: ô nhắc kỳ hợp đồng công ty đến hạn / quá hạn.
- Cấu hình: không thêm gì, các thông số nằm trên từng hợp đồng công ty.

### 3.6. Việc phải làm với dữ liệu thật trên máy chủ (sau khi có tính năng)

**Đã làm xong tính năng ngày 22/10/2026** (migration `CompanyContracts`, `CompanyService`, trang `/hop-dong-cong-ty`), thử trên LocalDB.
Khác bản nghiên cứu: không có nút "Lập phiếu ngay"; muốn đổi ngày của một thời hạn thì xóa và thêm lại (chỉ được khi chưa có phiếu).
Sau khi triển khai, làm trên giao diện (Thông tin → HĐ công ty), không cần lệnh máy chủ:

1. Tạo hợp đồng Horn, thời hạn 1 = hợp đồng gốc (20 phòng, đã hết hạn 09/09/2026, mọi kỳ tự ghi đã thu ngoài), thời hạn 2 = phụ lục
   (B3.1–B3.5, 10/09/2026–09/09/2027, 2.200.000, cọc 6.600.000/phòng). Kỳ 1 của phụ lục: sửa cửa sổ nộp thành 20/09–05/10/2026.
   Bấm "Gắn phòng" (tự chọn sẵn B3.1–B3.5 theo phụ lục). Tải scan hợp đồng và phụ lục ở "Thông tin công ty".
2. Mục "Tiền cọc" sẽ báo thiếu 28.000.000 (đã ghi 1.000.000/phòng lúc nhập khách cũ): bấm "Ghi cọc" để ghi phần thiếu 5.600.000/phòng,
   vào tài khoản cá nhân (anh Duy xác nhận 22/10/2026 cọc Horn ở TK cá nhân; đây là lựa chọn mặc định).
3. Kỳ quý 1 (10/09–09/12/2026, 33.000.000, hạn 05/10/2026) đã thu ngoài phần mềm → ghi "đã thu trước khi dùng phần mềm" (giống cách
   nhập khách cũ), không tính vào doanh thu trên phần mềm. Kỳ quý 2 (10/12/2026–09/03/2027) là kỳ đầu thu trên phần mềm, hạn 25/12/2026.
4. Đặt tối đa 2 người/phòng cho B3.1–B3.5; bổ sung hộ chiếu, ảnh của người đang ở.

### 3.7. Khối lượng

Một migration (2 bảng mới + 3 cột), `CompanyService` (CRUD, sinh kỳ, thu gộp), sửa `BillingService` (tiền phòng 0, nước cố định,
không đoạn lẻ), sửa `LeaseService.PreviewCheckoutAsync` (bỏ quy định 3 tháng, bỏ kỳ thiếu tiền phòng cho phòng công ty), trang mới
`/hop-dong-cong-ty`, thẻ nhắc trên trang chủ và trang phòng, lọc ở Khách thuê. Ước **2 ngày làm** kể cả thử trên LocalDB.

## 4. Phương án gọn hơn (không khuyến nghị)

Chỉ thêm lên `Lease` ba cột: tên công ty, số tháng/kỳ tiền phòng, nước cố định/phòng; tiền quý ghi vào phiếu tháng đầu quý.
Làm nhanh hơn (~nửa ngày) nhưng: không lưu được phụ lục/lịch nộp, không thu gộp một lần chuyển khoản, 5 phòng phải sửa tay từng cái
khi có phụ lục, tiền phòng lẫn với điện nước trên cùng phiếu nên khó đối chiếu với số tiền Horn chuyển (33.000.000 chẵn).
Chỉ nên chọn nếu anh Duy muốn có ngay trong tuần và chấp nhận theo dõi phụ lục bằng ghi chú.
