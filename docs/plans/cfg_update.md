```json
{
   "timestamp":1766714128965,
   "reqSeqId":"7f8e61d1-5106-49f1-8aaf-eaf0d6353023",
   "rspSeqId":"d0df95b7-de5a-4689-9cb2-b92c8c0c4339",
   "code":0,
   "message":"Device registration completed successfully",
   "data":{
      "deviceName":"MyWiseDevice4012Z2343",
      "registrationStatus":"accepted",
      "deviceId":"262760264720449536",
      "natsTopicAssignments":{
         "telemetryTopic":"eco1j.weda.262760264720449536.telemetry",
         "batchTelemetryTopic":"eco1j.weda.262760264720449536.telemetry-batch",
         "healthTopic":"eco1j.weda.262760264720449536.health",
         "configUpdateTopic":"eco1j.weda.262760264720449536.subnode.shadow.cfg.delta",
         "configResponseTopic":"eco1j.weda.262760264720449536.subnode.shadow.cfg.update",
         "commandTopic":"eco1j.weda.262760264720449536.subnode.cmd.req",
         "commandResponseTopic":"eco1j.weda.262760264720449536.subnode.cmd.rsp",
         "eventTopic":"eco1j.weda.262760264720449536.event",
         "shadowConfigResponseTopic":"eco1j.weda.262760264720449536.subnode.shadow.cfg.update.rsp",
         "shadowStateResponseTopic":"eco1j.weda.262760264720449536.subnode.shadow.state.update.rsp",
         "systemConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.systemcfg.delta",
         "customConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.customcfg.delta",
         "deviceConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.devicecfg.delta",
         "systemConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.systemcfg.update",
         "deviceConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.devicecfg.update",
         "customConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.customcfg.update"
      }
   }
}
```

+ key change
1. config: 
從 subscribe 
    "configUpdateTopic":"eco1j.weda.262760264720449536.subnode.shadow.cfg.delta",
   publish
    "configResponseTopic":"eco1j.weda.262760264720449536.subnode.shadow.cfg.update",
改成動態聽取
    "systemConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.systemcfg.delta",
    "customConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.customcfg.delta",
    "deviceConfigDesiredTopic":"eco1j.weda.262760264720449536.subnode.shadow.devicecfg.delta",
並回覆
    "systemConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.systemcfg.update",
    "customConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.customcfg.update"
    "deviceConfigReportedTopic":"eco1j.weda.262760264720449536.subnode.shadow.devicecfg.update",

2. cmd: 
從 subscribe
    "commandTopic":"eco1j.weda.262760264720449536.subnode.cmd.req",
    publish
    "commandResponseTopic":"eco1j.weda.262760264720449536.subnode.cmd.rsp", 
變成動態聽取
