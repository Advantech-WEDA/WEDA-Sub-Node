# OPC UA SubNode

透過 OPC UA 讀取設備資料。範例內建一個 OPC UA 模擬伺服器，提供溫度、濕度、氣壓三個節點，SubNode 以
OPC UA 用戶端的身分訂閱它們並回報上去。要接真實的 OPC UA 伺服器時，改設定檔裡的位址即可。

不必接任何硬體。

## 正常運作時會看到什麼

三個感測值會定時出現：

```
[INF] temperature_sensor: 25
[INF] humidity_sensor: 55
[INF] pressure_sensor: 1013
[INF] temperature_sensor: 24.839359283447266
[INF] humidity_sensor: 55.17400360107422
```

第一輪是模擬器的初始值，之後會開始飄動。
