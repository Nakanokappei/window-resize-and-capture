# Window Resize for Windows — Hướng dẫn sử dụng

## Mục lục

1. [Bắt đầu](#bắt-đầu)
2. [Thay đổi kích thước cửa sổ](#thay-đổi-kích-thước-cửa-sổ)
3. [Cài đặt](#cài-đặt)
4. [Xử lý sự cố](#xử-lý-sự-cố)

---

## Bắt đầu

1. Chạy **WindowsResizeCapture.exe**. Màn hình khởi động sẽ hiển thị trong giây lát.
2. Biểu tượng ứng dụng xuất hiện trong **khay hệ thống** (vùng thông báo ở góc dưới bên phải thanh tác vụ).
3. Nhấp vào biểu tượng khay để mở menu.

> **Lưu ý:** Không cần quyền đặc biệt. Ứng dụng hoạt động ngay sau khi khởi chạy.

---

## Thay đổi kích thước cửa sổ

### Các bước thực hiện

1. Nhấp vào **biểu tượng Window Resize** trong khay hệ thống.
2. Di chuột qua **"Thay đổi kích thước"** để mở danh sách cửa sổ.
3. Tất cả cửa sổ đang mở sẽ hiển thị với **biểu tượng ứng dụng** và tên theo định dạng **[Tên ứng dụng] Tiêu đề cửa sổ**. Tiêu đề dài sẽ tự động được cắt ngắn.
4. Di chuột qua một cửa sổ để xem các kích thước đặt trước có sẵn.
5. Nhấp vào một kích thước để thay đổi kích thước cửa sổ ngay lập tức.

### Định dạng hiển thị kích thước

Mỗi mục kích thước trong menu hiển thị như sau:

```
1920 x 1080          Full HD
```

- **Bên trái:** Rộng x Cao (pixel)
- **Bên phải:** Nhãn (tên tiêu chuẩn), hiển thị màu xám

### Kích thước đặt trước vượt quá màn hình

Nếu kích thước đặt trước lớn hơn độ phân giải của màn hình nơi cửa sổ đang hiển thị, kích thước đó sẽ **chuyển sang màu xám và không thể chọn**. Điều này ngăn việc thay đổi kích thước cửa sổ vượt quá ranh giới màn hình.

---

## Cài đặt

Nhấp vào biểu tượng Window Resize trong khay, sau đó chọn **"Cài đặt..."** để mở cửa sổ cài đặt.

### Kích thước tích hợp

Ứng dụng bao gồm 12 kích thước đặt trước tích hợp:

| Kích thước | Nhãn |
|------------|------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1200 | WUXGA |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1600 x 900 | HD+ |
| 1440 x 900 | WXGA+ |
| 1366 x 768 | WXGA |
| 1280 x 1024 | SXGA |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

Kích thước tích hợp không thể xóa hoặc chỉnh sửa.

### Kích thước tùy chỉnh

Bạn có thể thêm kích thước của riêng mình:

1. Nhập **Chiều rộng** và **Chiều cao** bằng pixel.
2. Nhấp **"Thêm"**.
3. Kích thước mới sẽ được thêm vào danh sách và có sẵn ngay trong menu thay đổi kích thước.

Để xóa kích thước tùy chỉnh, nhấp nút **"Xóa"** bên cạnh.

### Khởi động khi đăng nhập

Bật **"Khởi động khi đăng nhập"** để Window Resize tự động khởi động khi bạn đăng nhập vào Windows.

### Ảnh chụp màn hình

Bật **"Chụp ảnh màn hình sau khi thay đổi kích thước"** để tự động chụp cửa sổ sau khi thay đổi kích thước.

Khi bật, các tùy chọn sau khả dụng:

- **Lưu vào tệp** — Lưu ảnh chụp màn hình dưới dạng tệp PNG vào thư mục bạn chọn.
  > **Định dạng tên tệp:** `MMddHHmmss_TênỨngDụng_TiêuĐềCửaSổ.png` (ví dụ: `0227193012_chrome_Google.png`). Ký hiệu sẽ bị loại bỏ; chỉ sử dụng chữ cái, chữ số và dấu gạch dưới.
- **Sao chép vào bộ nhớ tạm** — Sao chép ảnh chụp màn hình vào bộ nhớ tạm để dán vào ứng dụng khác.

Cả hai tùy chọn có thể được bật độc lập.

---

## Xử lý sự cố

### Thay đổi kích thước thất bại

Nếu hiển thị thông báo "Thay đổi kích thước thất bại", nguyên nhân có thể bao gồm:

- Cửa sổ mục tiêu không hỗ trợ thay đổi kích thước từ bên ngoài.
- Cửa sổ đang ở **chế độ toàn màn hình** (nhấn **F11** hoặc **Esc** để thoát trước).

### Cửa sổ không xuất hiện trong danh sách

Menu thay đổi kích thước chỉ hiển thị các cửa sổ:

- Hiện đang hiển thị trên màn hình
- Có thanh tiêu đề
- Không phải cửa sổ của ứng dụng Window Resize

Cửa sổ đã thu nhỏ sẽ không xuất hiện trong danh sách.

### Ảnh chụp màn hình không hoạt động

Nếu ảnh chụp màn hình không được chụp:

- Đảm bảo đã bật ít nhất một trong hai tùy chọn **"Lưu vào tệp"** hoặc **"Sao chép vào bộ nhớ tạm"** trong Cài đặt.
- Nếu lưu vào tệp, hãy xác nhận thư mục lưu tồn tại và có thể ghi được.
