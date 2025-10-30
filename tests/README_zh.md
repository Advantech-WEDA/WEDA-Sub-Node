# Weda SubNode 測試基礎設施

此目錄包含 Weda SubNode 專案的測試基礎設施。

## 專案結構

```
tests/
├── Weda.SubNode.TestBase/            # 共用測試工具和建構器
│   ├── Builders/                       # 測試資料建構器
│   │   └── DeviceConfigurationBuilder.cs
│   └── Fixtures/                       # 測試固件（未來）
├── Weda.SubNode.Core.Tests/          # Core 層的單元測試
│   └── InfrastructureTests.cs          # 測試基礎設施的冒煙測試
├── Weda.SubNode.Abstractions.Tests/  # Abstractions 的單元測試（未來）
├── Weda.SubNode.Devices.Tests/       # Devices 的單元測試（未來）
├── Directory.Build.props               # 共用測試專案配置
├── coverlet.runsettings                # 程式碼覆蓋率配置
├── README.md                           # 英文文件
└── README_zh.md                        # 本檔案（繁體中文文件）
```

## 測試技術堆疊

- **xUnit 2.9.2** - 測試框架
- **NSubstitute 5.3.0** - 模擬框架
- **Shouldly 4.2.1** - 斷言函式庫
- **Coverlet** - 程式碼覆蓋率工具
- **Testcontainers 3.10.0** - 使用 Docker 進行整合測試（未來）

## 執行測試

### 執行所有測試
```bash
dotnet test
```

### 執行特定專案的測試
```bash
dotnet test tests/Weda.SubNode.Core.Tests
```

### 執行測試並產生覆蓋率報告
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults --settings tests/coverlet.runsettings
```

### 執行測試並顯示詳細輸出
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 程式碼覆蓋率

### 覆蓋率配置

覆蓋率設定定義於 `coverlet.runsettings`：

- **格式**：Cobertura XML + JSON
- **排除**：測試專案、Program.cs、Startup.cs
- **排除的屬性**：Obsolete、GeneratedCode、CompilerGenerated
- **跳過自動屬性**：是

### 目前覆蓋率目標

- **行覆蓋率**：60%（初始目標）
- **分支覆蓋率**：50%（初始目標）

這些目標將隨著測試套件的成熟而提高：

- 第二階段：70% 行覆蓋率、60% 分支覆蓋率
- 第三階段：80% 行覆蓋率、70% 分支覆蓋率

### 查看覆蓋率報告

執行測試並產生覆蓋率後，可查看報告：

```bash
# 尋找覆蓋率檔案
find TestResults -name "coverage.cobertura.xml"

# 安裝報表產生器（一次性）
dotnet tool install -g dotnet-reportgenerator-globaltool

# 產生 HTML 報告
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html

# 開啟報告
open TestResults/CoverageReport/index.html
```

## 測試資料建構器

本專案使用 **測試資料建構器（Test Data Builder）** 模式來建立測試物件：

```csharp
// 範例：建立測試用的 DeviceConfiguration
var config = DeviceConfigurationBuilder.Default()
    .WithDeviceId("test-device-001")
    .WithDeviceName("Test Device")
    .AddSensor(new Sensor { ResourceId = "sensor-001", Name = "temperature" })
    .Build();
```

## 撰寫測試

### 測試類別結構
```csharp
using Xunit;
using Shouldly;
using NSubstitute;
using Weda.SubNode.TestBase.Builders;

namespace Weda.SubNode.Core.Tests;

public class MyFeatureTests
{
    [Fact]
    public void MyMethod_WhenCondition_ShouldBehavior()
    {
        // Arrange - 準備
        var config = DeviceConfigurationBuilder.Default().Build();
        var mockService = Substitute.For<IMyService>();
        mockService.SomeMethod().Returns(expectedValue);

        // Act - 執行
        var result = MyMethod(config, mockService);

        // Assert - 斷言
        result.ShouldNotBeNull();
        result.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    public void MyCalculation_WithInputs_ShouldReturnExpected(int a, int b, int expected)
    {
        // Arrange & Act - 準備與執行
        var result = Calculator.Add(a, b);

        // Assert - 斷言
        result.ShouldBe(expected);
    }
}
```

### 測試命名慣例

遵循模式：`方法名稱_情境_預期行為`

**良好範例：**
- `Initialize_WhenValidConfiguration_ShouldReturnTrue`
- `Connect_WhenNetworkError_ShouldRetryThreeTimes`
- `ProcessData_WithEmptyList_ShouldThrowArgumentException`

**不良範例：**
- `TestInitialize`（太模糊）
- `Test1`、`Test2`（無意義）
- `Initialize_Test`（未描述情境）

## CI/CD 整合

測試會在 Azure DevOps Pipeline 中自動執行：

- **觸發條件**：每次 PR 和提交到 main/develop 分支
- **環境**：Ubuntu Latest、.NET 9.0
- **覆蓋率檢查**：若覆蓋率 < 60% 則 Pipeline 失敗
- **測試結果**：發佈為建置成品

完整配置請參見 `azure-pipelines.yml`。

## 未來增強功能

- [ ] 使用 Testcontainers 的整合測試（NATS、PostgreSQL）
- [ ] 高吞吐量場景的效能測試
- [ ] Modbus 協定的契約測試
- [ ] 使用 Stryker.NET 的變異測試
- [ ] 配置驗證的快照測試

## 測試函式庫使用方式

### NSubstitute - 模擬框架

**建立模擬物件：**
```csharp
var mockService = Substitute.For<IMyService>();
```

**設定回傳值：**
```csharp
mockService.GetValue().Returns(42);
mockService.GetAsync(Arg.Any<string>()).Returns(Task.FromResult("result"));
```

**驗證方法呼叫：**
```csharp
await mockService.Received(1).ProcessAsync(Arg.Any<string>());
mockService.DidNotReceive().DeleteAll();
```

**參數比對：**
```csharp
mockService.Process(Arg.Any<int>());              // 任何值
mockService.Process(Arg.Is<int>(x => x > 5));     // 條件式
mockService.Process(42);                           // 精確比對
```

### Shouldly - 斷言函式庫

**基本斷言：**
```csharp
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);
result.ShouldBeNull();
result.ShouldNotBeNull();
```

**數值斷言：**
```csharp
value.ShouldBeGreaterThan(10);
value.ShouldBeLessThan(100);
value.ShouldBeInRange(10, 100);
```

**集合斷言：**
```csharp
list.ShouldNotBeEmpty();
list.ShouldContain(item);
list.ShouldAllBe(x => x > 0);
```

**字串斷言：**
```csharp
text.ShouldStartWith("prefix");
text.ShouldEndWith("suffix");
text.ShouldContain("substring");
text.ShouldMatch(@"\d{3}-\d{4}");
```

**例外斷言：**
```csharp
Should.Throw<ArgumentException>(() => method());
await Should.ThrowAsync<InvalidOperationException>(() => methodAsync());
```

## 疑難排解

### 測試失敗顯示「找不到類型」
確保您在測試檔案頂端加入 `using Xunit;`。

### NSubstitute 回傳 null 而非模擬值
檢查您是否正確使用 `Returns()` 且方法簽章完全相符。

### 未收集覆蓋率
驗證 `coverlet.runsettings` 位於正確位置，且測試命令中的路徑正確。

### 測試在本機通過但在 CI 中失敗
檢查硬編碼路徑、環境變數或依賴時區的程式碼。

### Shouldly 斷言訊息不清楚
Shouldly 預設提供詳細的錯誤訊息。確保您針對情境使用正確的斷言方法。

## 參考資料

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [Shouldly Documentation](https://docs.shouldly.org/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Test Data Builder Pattern](https://www.industriallogic.com/xp/refactoring/testDataBuilder.html)
