# Stock Quotes SubNode

定時呼叫台灣證券交易所的行情 API，把回傳欄位一一對應成感測值回報上去。它示範的是「輪詢 REST API 並把
欄位映射成純量感測器」這種情境——開盤價、最高價、最低價、成交量各自成為一個獨立的感測值。

不需要任何硬體，但**裝置必須連得到外網**才能呼叫該 API。

## 正常運作時會看到什麼

每次輪詢會印出所有對應到的欄位：

```
[INF]   Advantech Open Price (stock_2395_open): 647 TWD
[INF]   Advantech High Price (stock_2395_high): 658 TWD
[INF]   Advantech Volume (stock_2395_volume): 100 shares
[INF]   TSMC Current Price (stock_2330_current): 2445 TWD
```

數值每次都不一樣，因為那是當下的真實行情。非交易時段取得的會是最後一筆收盤資料。
