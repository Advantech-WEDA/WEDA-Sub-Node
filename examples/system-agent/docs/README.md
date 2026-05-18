# System Agent 文件導覽

本目錄包含 System Agent 的所有技術文件。以下說明各文件的用途與閱讀順序。

---

## 給人讀的文件（快速上手）

| 順序 | 文件 | 說明 |
|------|------|------|
| 1 | [01_QUICK_START.md](01_QUICK_START.md) | 部署與啟動 |
| 2 | [Metrics/00_DEVICECFG.md](Metrics/00_DEVICECFG.md) | `devicecfg.json` 結構總覽（一頁式） |
| 3 | [v1.0/Sensor-Configuration-and-Usage-Guide.md](v1.0/Sensor-Configuration-and-Usage-Guide.md) | Sensor 配置完整參考（v1.0） |
| 4 | [v1.1/Sensor-Configuration-and-Usage-Guide.md](v1.1/Sensor-Configuration-and-Usage-Guide.md) | v1.1 差異：Auto-detect / Explicit list / Bound mode |
| 5 | [03_DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) | Docker 部署指南 |
| 6 | [04_TESTING_GUIDE.md](04_TESTING_GUIDE.md) | 測試指南 |

> **英文版**：每份中文文件在同目錄下有對應的 `_en.md` 版本。

---

## 給 AI 讀的文件（串接/開發用）

如果你要讓 AI 協助新增 Sensor、修改配置或開發串接，請餵以下文件：

```
# 欄位定義（含 Auto-detect / Explicit list / Bound mode 完整規格）
docs/Metrics/01_CPU_NETWORK_FIELDS.md
docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU_FIELDS.md
docs/Metrics/03_HARDWARE_INFO_FIELDS.md
docs/Metrics/04_ONBOARD_SENSOR_FIELDS.md
docs/Metrics/05_HARDWARE_FEATURE_FIELDS.md

# devicecfg.json 實際配置
examples/system-agent/devicecfg.json
examples/system-agent/devicecfg-explicit-list.json
```

> **為什麼不需要另外餵 `v1.0/` 或 `v1.1/` 文件？**
> `Metrics/*_FIELDS.md` 每個 MetricType 段落已包含 v1.0/v1.1 差異、三種模式（Bound / Explicit list / Auto-detect）的優先順序與範例。

---

## 文件結構

```
docs/
├── README.md                          ← 你在這裡
├── 01_QUICK_START.md                  — 快速部署
├── 03_DOCKER_DEPLOY.md                — Docker 詳細部署
├── 04_TESTING_GUIDE.md                — 測試方法
│
├── v1.0/                              — v1.0 基線文件
│   ├── Sensor-Configuration-and-Usage-Guide.md     — Sensor 配置完整參考
│   ├── Sensor-Configuration-and-Usage-Guide_en.md
│   ├── METRIC-TYPES.md                             — MetricType 欄位詳解
│   └── METRIC-TYPES_en.md
│
├── v1.1/                              — v1.1 差異文件
│   ├── Sensor-Configuration-and-Usage-Guide.md     — 欄位修改規則 + 三種解析模式
│   ├── Sensor-Configuration-and-Usage-Guide_en.md
│   ├── METRIC-TYPES.md                             — 解析模式詳細行為
│   └── METRIC-TYPES_en.md
│
└── Metrics/                           — 按 MetricType 分組的詳細技術文件
    ├── 00_DEVICECFG.md                — devicecfg.json 結構總覽
    ├── 01_CPU_NETWORK_FIELDS.md       — cpu + network 欄位定義
    ├── 01_CPU_NETWORK.dtdl.json       — DTDL Interface
    ├── 01_CPU_NETWORK_USAGE.md        — 使用情境與範例
    ├── 02_MEMORY_DISK_SYSTEM_GPU_*    — memory + disk + system + gpu
    ├── 03_HARDWARE_INFO_*             — hwinfo
    ├── 04_ONBOARD_SENSOR_*            — temperature + voltage + fanspeed
    ├── 05_HARDWARE_FEATURE_*          — gpio + watchdog + thermalprotection
    ├── HOWTO_EVALUATE_DTDL.md         — DTDL 驗證方法
    ├── _template/                     — 新增 MetricType 的範本
    └── samples/                       — DTDL 驗證用 sample 資料
```

---

## DTDL 驗證工具

位於 `examples/system-agent/dtdl-validate/`，用於驗證 DTDL Interface 定義與 sample 資料的正確性。

| 路徑 | 說明 |
|------|------|
| `dtdl-validate/docker-compose.yml` | 一鍵啟動驗證（包含 .NET 和 Node.js 兩套驗證器） |
| `dtdl-validate/scripts-dotnet/` | .NET 版驗證器（使用 Microsoft DTDLParser） |
| `dtdl-validate/scripts/` | Node.js 版驗證器（輕量替代方案） |

使用方式：

```bash
cd examples/system-agent/dtdl-validate
docker compose up --build
```

驗證內容：
- DTDL JSON 語法與結構正確性
- Telemetry schema 是否與 `devicecfg.json` 的 `SensorInfo.Schema` 一致
- Sample 資料是否符合 DTDL 定義的型別

詳細說明請參閱 [HOWTO_EVALUATE_DTDL.md](Metrics/HOWTO_EVALUATE_DTDL.md)。

---

## 配置檔案說明

| 檔案 | 用途 |
|------|------|
| `devicecfg.json` | 預設推薦配置（使用 `[]` 自動偵測模式） |
| `devicecfg-explicit-list.json` | 進階範例：明確列表模式 + 單一綁定模式 |

---

## 其他參考

| 文件 | 說明 |
|------|------|
| [CODE_REVIEW_REPORT.md](CODE_REVIEW_REPORT.md) | 程式碼審查報告 |
| [FAIL_SAFE_IMPLEMENTATION_REPORT.md](FAIL_SAFE_IMPLEMENTATION_REPORT.md) | Fail-safe 實作報告 |
| [SIGSEGV.md](SIGSEGV.md) | SIGSEGV 問題排查記錄 |
