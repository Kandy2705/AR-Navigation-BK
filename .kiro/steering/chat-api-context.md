---
inclusion: manual
---

# Context — Gắn API Chat & Refactor UI (Unity AR Project)

File này dùng để Kiro tự khôi phục ngữ cảnh khi mở phiên mới. Người dùng giao tiếp bằng tiếng Việt.

## Mục tiêu chung
Gắn API backend CO4029_BE vào UI Toolkit (UIHistory/UIChat/UIProfile/UIMainSetting…). Đồng thời refactor UI (nav bar shared, fix scroll bug, etc.).

## Backend (Swagger live, KHÔNG dùng `API-FLOWS.md` — file đó outdated)

Base URL: `AppConst.BASE_API` = `https://arnavbk-...azurewebsites.net/api/v1`

### User / Auth
- `POST /users/login` — body `{ email, password }` → `{ success, data: { accessToken, refreshToken, ... } }` (có wrapper)
- `POST /users/create-customer` — body register
- `GET /users/me` — header Bearer → `{ success, data: RegisterRes }` (có wrapper)
- `POST /users/request-password-change` — body `{ email }`
- `POST /users/change-password` — body `{ email, otpCode, oldPassword, newPassword }`

### Chat (đã verify từ Swagger)
- `GET /chat/histories` — list session, response `{ success, data: [{ id, header, create_date }], message }`
- `POST /chat/histories` — body `{ header }` tạo session mới
- `GET /chat/chatboxes/history/{historyId}` — list message của 1 phiên, response `[{ Id, Content, Contact_person }]` (có thể PascalCase, cũng có thể lowercase tuỳ endpoint)
- `POST /chat/chatboxes` — body `{ Content, History_id }` → response `{ success, data: [user_msg, assistant_msg], message }`. Field message: lowercase `id, content, contact_time, contact_person`.
- `DELETE /chat/chatboxes/{chatboxId}` — Swagger ghi vậy nhưng có khả năng đây là xoá MESSAGE chứ không phải xoá HISTORY. Người dùng vẫn chưa xác nhận đúng endpoint xoá session.

## Stack hiện tại
- Unity 6000.0.44f1, UI Toolkit (`.uxml`/`.uss`).
- HTTP client: `Proyecto26.RestClient` + RSG.Promise (đã có sẵn trong `Assets/RestClient/`).
- Token cache: `PlayerPrefs("ACCESS_TOKEN")` (do `LoginPageController` lưu).
- Hệ điều hướng: `NavigationManager` (MonoBehaviour) + `IPageController` + `PageFactory` ở `Assets/UI/Scripts/`. Có hệ cũ `UIRouter`/`UILogin`/`UISignUp` cho onboarding/welcome.

## Đã làm xong (file đã sửa/tạo)

### Service / Model
- `Service/ChatService.cs` — wrapper 3 endpoint: `GetHistory`, `CreateHistory(header)`, `GetChatboxMessages(historyId)`, `SendMessage(content, historyId)`, `DeleteHistory(chatboxId)`. Tự gắn Authorization Bearer từ `PlayerPrefs("ACCESS_TOKEN")`.
- `Model/ChatModels.cs` — `ChatMessage` chứa cả 2 schema (PascalCase + lowercase) với helper `GetContent()`/`GetSender()`. `ChatHistoryItem { id, header, create_date, messages }`. Wrappers `ChatHistoryListResponse`, `ChatHistoryMessagesResponse`, `ChatHistoryResponse`, `SendMessageResponse`. `SendMessageReq { Content, History_id }`. `static ChatSession { HistoryList, Current }`.
- `Model/LoginModels.cs` — thêm `LoginResponseWrapper { success, data: LoginRes }`.
- `Service/ProfileService.cs` — `GetUserProfile(token)` parse cả wrapper `{success,data}` lẫn flat. URL dùng `AppConst.BASE_API + "/users/me"`.

### Controller
- `Controller/HistoryPageController.cs` — gọi `GetHistory`, render qua `HistoryManager`. Có nút `BtnNewChat` → `CreateHistory`. Click thẻ → `GetChatboxMessages` → set `ChatSession.Current` → `Navigate(Chatbox)`. Có loading overlay (spinner xoay, label "Đang tải..."). Modal "Xóa lịch sử" → callback `OnDeleteHistoryItem` → `DeleteHistory(item.id)`.
- `Controller/ChatboxController.cs` — render messages từ `ChatSession.Current.messages`, bind BtnSend (+ Enter), gọi `SendMessage(text, historyId)`, parse `SendMessageResponse` (List<ChatMessage>) lấy reply assistant. Disable nút Send khi đang chờ.
- `Controller/LoginPageController.cs` — bỏ shortcut `Navigate(MainSettings); return;`. Thêm `ParseLoginResponse` parse cả wrapper lẫn flat.
- `Controller/MainSettingController.cs` — bỏ bind nút `BtnHistory`/`btn-ar` vì giờ shared nav xử lý.
- `Controller/HistoryPageController.cs` — bỏ bind nút `BtnSettings`/`BtnChatbox`/`btn-ar`, vẫn bind `BtnBack`.
- `Controller/ProfileController.cs` — fix bug `JsonUtility.FromJson("Không có dữ liệu")` gây crash khi chưa có cache.

### UI / UXML / USS
- `UI Main.uxml` — thêm `<SharedBottomNav>` ngoài RootContainer (3 nút btn-ar, BtnHistory, BtnSettings). Style import từ `UI History.uss`.
- `UI History.uxml`, `UI Main Setting.uxml` — bỏ `bottom-nav` nội bộ.
- `UI History.uxml` — thêm nút FAB `BtnNewChat` (+) ở góc phải-dưới. ScrollView `body-list` thêm `touch-scroll-type="Clamped" elasticity="0"`.
- `UI History.uss` — `.btn-delete-hidden` thêm `background-image: url(...trash.png)`. `.body-list` `flex-grow:1`. `.bottom-nav` bỏ `position:absolute`. Thêm `.nav-active .icon-action { tint: #6C5DD3 }` và `.nav-item .icon-action { tint: rgb(142,142,147) }` (xám khi không active).
- `UI Chat.uxml` + `UI Chat.uss` — input chat dùng `flex-grow:1; min-width:0; overflow:hidden` để không bị giãn theo text dài. Nút send fix size 50x50.
- `UI User Info.uxml` — bỏ ScrollView (đổi thành VisualElement), bỏ min/max-height cứng, bỏ `position:absolute` cho `.bottom-actions`. Header label bỏ `width:164px` cứng.
- `UI Main Setting.uxml` — bỏ ScrollView (đổi thành VisualElement).
- Các UXML khác (Email Changing, Password Changing, Support Center) — ScrollView thêm `touch-scroll-type="Clamped" elasticity="0"`, đổi `min-height:400px` cứng → `flex-grow:1`.
- `Assets/UI/Resources/InputCaretStyle.uss` — caret trắng cho mọi TextField.
- `Manager/CaretStyleApplier.cs` — runtime apply caret style + `ClampScrollViews` (set `touchScrollBehavior=Clamped, elasticity=0`).
- `Manager/SharedNavBar.cs` — quản lý shared nav: bind 1 lần, `SetActive(pageId)` đổi class `nav-active`, `SetVisible(bool)` ẩn/hiện. Tab order: AR → History → Settings. `_navVisiblePages = {HistoryPage, MainSettings}` (AR là scene riêng nên không hiện nav). `GoToTab` tự tính isBack theo index trong `_tabOrder`.
- `Manager/NavigationManager.cs` — bind shared nav trong `OnEnable`. Trong `Navigate()` apply caret + nav state cho page mới. **`OnEnable` thêm `rootContainer.Clear(); currentPageElement = null;`** để reset state khi quay từ AR. `ConsumeReturnPageFromAR` pop target ở đỉnh trước khi return để tránh duplicate stack.

### Base URL refactor
- Xoá `APIModel/ApiConfig.cs` (trùng với `AppConst.BASE_API`).
- `LoginController.cs`, `RegisterController.cs` (legacy MonoBehaviour) — chuyển hardcode URL sang `AppConst.BASE_API + "/users/..."`.

## **REGRESSION CHƯA FIX (ưu tiên xử lý sáng mai)**

User đã báo: **việc thêm shared nav bar gây 2 regression**:

1. **Quay lại từ AR scene → UI bị "chết đứng" / loạn**
   - Trước fix shared nav: bạn ấy đã làm flow AR ↔ UI hoạt động.
   - Sau khi mình thêm shared nav + sửa `OnEnable` reset rootContainer, flow indoor AR → quay về vẫn bị stuck, không bấm được.
   - Mình **chưa đọc scene `HybridGPSMap.unity`** vì không tìm thấy file (search trả `No files found`). User dùng Plastic SCM, có thể scene nằm dưới path khác.
   - Trước khi tiếp tục cần: hỏi user path scene chính xác hoặc dùng `list_directory` Assets/Scene và Assets/Scenes.

2. **Shared nav xuất hiện ở các trang KHÔNG nên có nó** (Profile, Chatbox, Email Change, Support Center, Contact, Password Change…)
   - Logic hiện tại trong `NavigationManager.Navigate`:
     ```csharp
     bool isTabPage = _navBar.IsTabPage(pageID);
     _navBar.SetVisible(isTabPage);
     ```
   - `SharedNavBar._navVisiblePages = { HistoryPage, MainSettings }` — đáng lẽ chỉ 2 page này mới hiện nav.
   - Nhưng user vẫn thấy nav ở trang khác → có thể `_navBar.SetVisible(false)` không thực sự ẩn (style display vẫn flex), hoặc trang đó không pass qua `Navigate()` này.
   - Cần verify: trang Profile/Chatbox đang được navigate qua đâu? `MainSettingController` bind `BtnProfile → Navigate(PageID.Profile)` thì phải đi qua `Navigate()` này. Cần check log.

User đã yêu cầu **dừng** sau khi mình loay hoay tìm scene. Phương án 2 hướng cho session mới:
- **Hướng A — Revert shared nav**: đưa nav bar về lại từng UXML page như cũ. Chấp nhận hiệu ứng nháy nhẹ khi chuyển tab. An toàn hơn, không động flow AR.
- **Hướng B — Giữ shared nav, debug**: cần đọc scene chính xác, verify `gameObject.SetActive` flow của `NavigationManager` khi quay từ AR, kiểm tra event listener bị lưu trên `SharedNavBar` qua các session.

Hỏi user hướng nào trước khi sửa.

## Vấn đề khác chưa fix
- **DELETE history endpoint chưa chắc đúng**: `DELETE /chat/chatboxes/{id}` có thể là xoá tin nhắn chứ không phải xoá history. User test xoá session vẫn không mất hẳn (reload lại thấy lại). Cần Swagger xác minh có endpoint riêng `DELETE /chat/histories/{id}` hay không.
- **Trang Contact / Support Center** chưa có API thật. User nói chưa có UI admin nên có thể dùng các endpoint GET (read-only) cho user thường thấy FAQ. Cần Swagger ContactManagementFacade GET endpoints sample response.

## Quy ước phong cách (đã được user xác nhận)
- Trả lời bằng **tiếng Việt**, ngắn gọn, không lan man.
- Khi đụng vào hệ điều hướng mới, ưu tiên `IPageController` + `NavigationManager`.
- DTO mới đặt ở `Assets/UI/Scripts/Model/`. Service mới ở `Assets/UI/Scripts/Service/`. Controller mới ở `Assets/UI/Scripts/Controller/`. Tất cả flat namespace.
- Khi sửa file `.uxml` thì giữ nguyên các style inline đã có; chỉ thêm element mới hoặc append.
- **KHÔNG đoán endpoint API**. Phải có Swagger live screenshot hoặc raw response từ user trước khi viết DTO.

## Cách kích hoạt steering này
Trong chat: `#chat-api-context` (Kiro sẽ load file này).

Câu mở đầu gợi ý cho session mới:
> "#chat-api-context tiếp tục từ chỗ regression shared nav bar gây lỗi quay về từ AR. Hãy hỏi tôi muốn revert hay debug tiếp."
