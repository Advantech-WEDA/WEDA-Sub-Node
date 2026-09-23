# Custom Commands SubNode

示範如何讓 SubNode 接收雲端下達的自訂指令。範例註冊了三個指令：讀取感測值（`sensor.read`）、設定感測器
標籤（`tag.set`）、取得裝置屬性（`props.get`），每一個都包含處理器、參數驗證與回傳結果三個部分，而且會
自動出現在裝置的能力清單裡，雲端因此知道這台裝置支援哪些指令。

感測器是容器內建的模擬器，不必接任何硬體。

## 正常運作時會看到什麼

啟動後會持續看到感測值：

```
[INF] temperature_sensor: 25
[INF] temperature_sensor: 25.095226
```

從雲端下達指令後，會看到對應的執行結果：

```
[INF] Demo command 'sensor.read' succeeded: {"Status":0,"Message":"Sensor value read successfully",...}
[INF] Demo command 'tag.set' succeeded: {"Status":0,"Message":"Sensor tags applied successfully",...}
[INF] Demo command 'props.get' succeeded: {"Status":0,"Message":"Device properties retrieved successfully",...}
```

沒有人下指令時只會看到感測值，這是正常的。
