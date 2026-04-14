# AttendanceAgentSystem

Windows Agent đọc dữ liệu máy chấm công ZKTeco/Ronald Jack (ZKEMKeeper) và đẩy lên Django server qua API.

Repo gồm 3 project chính:

- `AttendanceAgent.Core`: core logic (device service, orchestrator, API client, local store)
- `AttendanceAgent.Console`: chạy dạng console để test/dev
- `AttendanceAgent.Service`: chạy dạng Windows Service (production)

## 1. Thiết bị hỗ trợ

- ZKTeco / Ronald Jack (các model phổ biến: K40, A4, …)
- Kết nối LAN TCP/IP, thường port `4370`
- SDK: `zkemkeeper` (COM)

## 2. SDK / COM interop (ZKEMKeeper)

Agent sử dụng COM `zkemkeeper` để:

- Connect: `Connect_Net(ip, port)`
- Read logs: `ReadTimeGLogData` (nếu support) hoặc fallback `ReadGeneralLogData`
- Lặp dữ liệu: `SSR_GetGeneralLogData(...)`

### 2.1. Yêu cầu x64 + đăng ký COM

- Build/Run `x64`
- Cần `Dependencies/64bit/*.dll` (zkemkeeper.dll, zkemsdk.dll, …) đi kèm output
- Trên máy chạy agent cần đăng ký COM:
  - chạy `Register_SDK_x64.bat` bằng **Run as Administrator**
  - hoặc cơ chế auto `regsvr32` (nếu project có implement)

> Lưu ý: `dotnet publish` bằng CLI có thể lỗi với COM reference (ResolveComReference). Khuyến nghị publish qua **Visual Studio / MSBuild Full**.

## 3. Django server tương thích (intranet)

Server Django nằm ở repo: `haixxx/intranet`.

Các endpoint liên quan (theo app `attendance_devices`):

- `POST /api/attendance/agents/register`
- `GET  /api/attendance/agents/<agent_id>/devices`
- `POST /api/attendance/agents/<agent_id>/heartbeat`
- `POST /api/attendance/raw-events/batch`  (**ingest logs**)

Server quản lý tập trung:
- danh sách device
- `last_cursor_json` cho từng device
- dedupe theo `device_event_id` và `dedup_hash`

## 4. Cursor (server is source of truth)

Agent chạy theo chu kỳ (ví dụ 5 phút/lần). Mỗi cycle:

1. Agent gọi `GET /api/attendance/agents/<agent_id>/devices` để lấy device list + `last_cursor_json`
2. Agent đọc incremental logs theo cursor
3. Agent đẩy logs theo batch qua `POST /api/attendance/raw-events/batch`
4. Cursor được **commit** thông qua ingest (server lưu `AttendanceDevice.last_cursor_json`)

### 4.1. Cursor format khuyến nghị

```json
{ "last_device_time": "yyyy-MM-dd HH:mm:ss" }
```

Lần sau: fromTime = last_device_time + 1 giây.

## 5. Batch ingest

Thiết bị có thể có dữ liệu rất lớn lần đầu (12k+ dòng). Vì vậy cần chunk:

- Chunk size <= 1000 events/batch (theo giới hạn server)
- Mỗi batch có `batch_id` unique
- Retry an toàn: server dedupe nên gửi lại không gây nhân bản dữ liệu

Payload ingest (tối thiểu):

```json
{
  "device_id": "4",
  "batch_id": "....",
  "cursor": { "last_device_time": "2026-03-17 00:39:38" },
  "events": [
    {
      "device_user_id": "879",
      "event_time_local": "2025-12-05T16:53:10+07:00",
      "event_time_utc": "2025-12-05T09:53:10Z",
      "method": "FACE",
      "direction": "UNKNOWN",
      "device_event_id": "ZK-4-20251205165310-879",
      "meta": { "verify_mode": 15, "inout_mode": 255, "workcode": 0 }
    }
  ]
}
```

## 6. Auth plan (the current plan)

Mục tiêu thiết kế (đã thống nhất trong trao đổi):
- Dùng **Bearer token hoàn toàn** cho đơn giản
- Ingest chỉ cho phép agent **được assign** mới được push dữ liệu vào device đó
- **Không dùng** endpoint cursor_update trong agent (cursor commit chỉ qua ingest)

Server side:
- `api_views_agents.py` đã có Bearer auth (`DeviceAPIKey`)
- `agent_devices` trả `last_cursor_json`
- `ingest_raw_events_batch` cần dùng Bearer + check `device.assigned_agent == agent`

Agent side:
- Bỏ `HmacAuthHandler` ra khỏi HttpClient pipeline
- HttpClient giữ Bearer `api_key` để gọi mọi endpoint

## 7. Cấu hình agent (gợi ý)

Các config tối thiểu:

- `ServerUrl`
- `AgentId` (sau khi register)
- `ApiKey` (Bearer token)
- `PollIntervalSeconds`

## 8. Chạy project

### 8.1. Console (dev)
- Build `x64`
- Run `AttendanceAgent.Console`
- đảm bảo DLL SDK được copy ra output

### 8.2. Windows Service (prod)
- Build/publish `AttendanceAgent.Service` (x64)
- Copy output sang máy chạy service
- Run admin `Register_SDK_x64.bat`
- Cài service theo hướng dẫn nội bộ (sc.exe / PowerShell New-Service)
- Logs: xem theo cấu hình Serilog (mặc định ghi file trong `C:\ProgramData\AttendanceAgent\Logs\...`)

## 9. Notes / Troubleshooting

- Lỗi connect: kiểm tra ping, port 4370, CommKey, firewall/VLAN
- Nếu connect OK nhưng đọc log fail: thử ranged read `ReadTimeGLogData` fallback `ReadGeneralLogData`
- Nếu chạy exe nháy tắt: kiểm tra OutputType (Exe/WinExe), chạy từ PowerShell để xem exception
- Publish bằng CLI có thể fail do COM => dùng VS Publish hoặc MSBuild Full