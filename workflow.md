# Workflow — TimeBomb Windows (WPF .NET Framework 4.7.2)

Bộ quy tắc BẮT BUỘC khi viết, chỉnh sửa và phân tích mã nguồn trong dự án TimeBomb Windows.

---

## 1. Nguyên tắc Giao tiếp & Kỹ thuật cốt lõi

- **Ngôn ngữ phản hồi**: Luôn luôn trả lời bằng **tiếng Việt**.
- **Mã nguồn**: Tên biến, hàm, class, comment trong code viết bằng tiếng Anh chuẩn.
- **Tuân thủ Skill**: Luôn áp dụng tư duy tối giản (YAGNI), đúng trọng tâm từ skill `teamwork-preview-protocol` và tối ưu cấu trúc từ `prompt-optimizer`.
- **Độc lập nền tảng**: Ứng dụng là **WPF thuần**, không dùng AutoHotkey, không phụ thuộc vào bất kỳ script ngoài nào.

---

## 2. Nền tảng & Công nghệ (Tech Stack)

- **Platform**: WPF trên .NET Framework `4.7.2`.
- **C# Language Version**: `7.3`.
- **Compiler**: Visual Studio MSBuild 18 (`MSBuild.exe`).
- **Mục tiêu đầu ra**: Executable standalone (`TimeBomb.exe`) tại `bin\Release\`.
- **Quy chuẩn chất lượng**: Build sạch **0 Error, 0 Warning**.

---

## 3. Kiến trúc & Phân Lane Dự án

```
timebomb windows/
├── App.xaml / App.xaml.cs          # Bootstrap, Single-instance Mutex, System Tray Icon
├── MainWindow.xaml / .cs           # Floating HUD Overlay Window (Topmost, NoActivate, ToolWindow)
├── AlarmWindow.xaml / .cs          # Alarm Popup Window khi Timer về 00:00
├── Core/
│   ├── Win32Api.cs                 # P/Invoke Win32 API (Hooks, Window Styles, Positioning)
│   ├── LowLevelKeyboardHook.cs     # Bắt Win+Keys toàn cục, Freezing feature, Suppress Start Menu
│   ├── TimeBombManager.cs          # Quản lý Timer, Stopwatch, Clock logic & gia tốc phím
│   ├── SoundManager.cs             # Phát âm thanh WAV bất đồng bộ (System.Media.SoundPlayer)
│   └── SettingsManager.cs          # Đọc/ghi cấu hình INI (vị trí X, Y, chế độ, thời gian lưu)
├── Resources/
│   ├── Fonts/                      # Font số điện tử DS-Digital
│   ├── Sounds/                     # 8 file WAV âm thanh gốc
│   └── Icons/                      # timebomb.ico
└── build.bat                       # Script build tự động bằng MSBuild Release
```

---

## 4. Tiêu chuẩn Giao diện (UI Standards)

- **Phong cách Thiết kế**: Dark Cyberpunk / Modern HUD Glassmorphic cao cấp.
- **Màu sắc chủ đạo**:
  - Nền overlay: Dark semi-transparent (`#CC252528` hoặc `#D81E1E22`), viền bo tròn tinh tế (`CornerRadius="8"`), viền phát sáng nhẹ (`#35353A`).
  - Màu hiển thị bình thường: Xanh neon Digital `#23FF23` và xanh phụ `#A0FFA0`.
  - Màu cảnh báo (Timer ≤ 10s hoặc hết giờ): Đỏ neon `#FF2323`.
- **Hiệu ứng & Trải nghiệm (UX)**:
  - Font số điện tử `DS-Digital` hiển thị thời gian sắc nét.
  - Nhấp nháy số (Blinking) khi Pause (chu kỳ 500ms) và khi đếm ngược dưới 10 giây.
  - Cửa sổ nổi: Hỗ trợ kéo thả (`DragMove`) mượt mà, tự động khóa trong màn hình (`Clamp to Screen Bounds`), không cướp focus của game hay ứng dụng fullscreen (`WS_EX_NOACTIVATE`), không hiện trong Alt+Tab (`WS_EX_TOOLWINDOW`).

---

## 5. Hệ thống Phím tắt & Tính năng Freezing

Tất cả phím tắt sử dụng tổ hợp phím **Win (Super/Meta)**:
- `Win + ` ` (Grave / Tilde): Bật / Tắt hiển thị overlay (Toggle Show/Hide).
- `Win + Enter`: Tạm dừng / Tiếp tục (Pause / Resume).
- `Win + Backspace`: Reset về mốc mặc định (hoặc tắt báo thức và reset nếu đang kêu).
- `Win + S`: Lưu thời gian đếm ngược hiện tại làm mốc reset mặc định mới.
- `Win + Esc`: Đổi chế độ xoay vòng: `Timer` ➔ `Stopwatch` ➔ `Clock`.
- `Win + Up`: Tăng timer thêm 1 phút (tự động tăng tốc độ khi giữ phím).
- `Win + Down`: Giảm timer đi 1 phút (tối thiểu 1 phút, tự động tăng tốc độ khi giữ phím).

**Đặc tả Freezing Feature**:
- Khi nhấn bất kỳ phím tắt nào với phím Win, nếu người dùng **tiếp tục giữ phím Win**, thời gian sẽ đóng băng tạm thời (Freeze). Khi nhả phím Win ra, đồng hồ mới tiếp tục đếm.
- Low-Level Keyboard Hook theo dõi trạng thái Up/Down của phím Win và chặn việc mở Start Menu Windows khi sử dụng phím tắt TimeBomb.

---

## 6. Quy trình Build & Git Commit (BẮT BUỘC)

1. Sau khi chỉnh sửa code, chạy `build.bat` hoặc MSBuild Release.
2. Tự động kiểm tra và sửa toàn bộ warning/error cho đến khi đạt **0 Error / 0 Warning**.
3. **Chỉ commit local** (`git commit`), **TUYỆT ĐỐI KHÔNG push GitHub** trừ khi có yêu cầu rõ ràng từ người dùng.
4. Thông báo kết quả theo format bắt buộc:
   ```
   commit local: *mã short hash*
   commit github: "không"
   path exe: <đường dẫn đầy đủ tới TimeBomb.exe>
   ```
