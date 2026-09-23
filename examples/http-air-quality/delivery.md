# Air Quality SubNode

定時呼叫環境部的空氣品質開放 API，把回傳的整份資料當成一筆記錄回報上去。它示範的是「輪詢一個 REST API
並回報整份文件」這種情境——跟逐一對應成純量感測值的做法不同，適合結構本身就有意義的資料。

不需要任何硬體，但**裝置必須連得到外網**才能呼叫該 API。

## 正常運作時會看到什麼

每隔一段時間出現一次取得資料的紀錄：

```
[INF] Air quality data received: AirQualityMonitor.Models.AirQualityRecords...
```

如果裝置沒有對外網路，這一行不會出現，log 會改成 HTTP 連線失敗。
