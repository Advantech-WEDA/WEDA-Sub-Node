# Aggregation SubNode

示範如何從多個來源算出一個新的感測值。範例內建電流與電壓兩個模擬感測器，另外定義一個功率感測器，其值
由前兩者相乘得出——功率本身沒有對應的實體感測器，是算出來的。

不必接任何硬體。

## 正常運作時會看到什麼

兩個來源感測器各自回報，接著衍生的功率感測器跟著更新：

```
[INF] [CurrentSensor] DataReceived: ResourceId=502ac822-..., Value=5.00, Timestamp=...
[INF] [VoltageSensor] DataReceived: ResourceId=7c8b5f5f-..., Value=230.00, Timestamp=...
[INF] [PowerSensor]   DataReceived: ResourceId=0b4c6033-..., Value=1143.28, Timestamp=...
```

看到 `PowerSensor` 那一行，就表示兩個來源都有資料進來而且運算有執行。數值會隨模擬器飄動。
