
# Summary

## 
```
   Application (你的 ROS 2 程式碼)
        ↓
   rcl / rclcpp / rclpy   ← Node、Pub/Sub、Service correlation、QoS preset、namespace mangling 在這
        ↓
   rmw                    ← middleware 抽象層
        ↓
   DDS impl (FastDDS / CycloneDDS)   ← RTPS wire、CDR、QoS 真正執行在這
        ↓
   網路
```

|選項|	插哪層|	帶來的東西|	失去的東西|
|--|--|--|--|
|A. rclcpp + C shim + P/Invoke|	rcl 層|	我們在 ros2 node list 看得到；service correlation (Ch2 講的 client_guid + sequence_number 配對) rcl 幫你做；QoS preset (sensor_data / default) rcl 幫你套；namespace mangling rcl 幫你做|	部署要帶整套 ROS 2 runtime（500 MB +）；綁特定 distro；客戶 IT 部門要點頭|
|B. ros2_dotnet（社群 .NET 綁定）|	rcl 層（透過社群 wrapper）|	同 A，但 .NET 程式碼最少|	社群維護、Jazzy 支援度未驗證、threading model 固定、出 bug 自己修|
|C. FastDDS 或 CycloneDDS 直連|	DDS 層|	單一 binary（30 MB）；distro-agnostic；DDS-XTypes 自然就在|	不會出現在 ros2 node list；service correlation 要自己寫；QoS preset 要自己對照；namespace mangling（rt/、rq/、rr/）要自己做|
|D. RTI Connext .NET|	DDS 層（商業 .NET 綁定）|	同 C 但有官方 .NET 支援、enterprise 維護|	商業授權 $$$；違背 PRD「Free SDK」定位