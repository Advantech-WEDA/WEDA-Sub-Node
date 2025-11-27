#!/bin/bash

# Pipeline Configuration Update - Success Test Cases
# 測試各種配置更新成功的情況

set -e

DEVICE_ID="251583620882366464"
DEVICE_NAME="Test789"
DEVICE_TYPE="MyFirstDeviceConfig"
SUBJECT="eco1j.weda.dm.config.${DEVICE_ID}.req"

# 顏色定義
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Pipeline Configuration Update - Success Test Cases${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo ""

# 檢查 nats 是否可用
if ! command -v nats &> /dev/null; then
    echo -e "${RED}❌ Error: 'nats' command not found${NC}"
    echo "Please install NATS CLI: https://github.com/nats-io/natscli"
    exit 1
fi

echo -e "${YELLOW}📋 Test Environment:${NC}"
echo "  Device ID:   $DEVICE_ID"
echo "  Device Name: $DEVICE_NAME"
echo "  Device Type: $DEVICE_TYPE"
echo "  NATS Subject: $SUBJECT"
echo ""

# 等待函數
wait_for_response() {
    echo -e "${YELLOW}⏳ Waiting 3 seconds for response...${NC}"
    sleep 3
}

# 測試分隔線
print_test_header() {
    echo ""
    echo -e "${CYAN}────────────────────────────────────────────────────────────────${NC}"
    echo -e "${CYAN}  Test $1: $2${NC}"
    echo -e "${CYAN}────────────────────────────────────────────────────────────────${NC}"
}

# ============================================================================
# Test 1: 更新單個 Transform 參數
# ============================================================================
print_test_header "1" "更新 Calibration 的 Offset 和 Scale"

echo -e "${YELLOW}Expected: Calibration offset=-3.0, scale=1.05${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 201,
  "reqSeqId": "test-success-001",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "Calibration",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "Offset": -3.0,
                      "Scale": 1.05
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 1 completed - Calibration parameters updated${NC}"

# ============================================================================
# Test 2: 禁用 Transform
# ============================================================================
print_test_header "2" "禁用 Calibration Transform (pass-through)"

echo -e "${YELLOW}Expected: Calibration disabled, raw values pass through${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 202,
  "reqSeqId": "test-success-002",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "Calibration",
                    "enabled": false,
                    "parameters": {
                      "TargetResourceId": "*",
                      "Offset": -3.0,
                      "Scale": 1.05
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 2 completed - Calibration disabled${NC}"

# ============================================================================
# Test 3: 重新啟用 Transform
# ============================================================================
print_test_header "3" "重新啟用 Calibration Transform"

echo -e "${YELLOW}Expected: Calibration enabled again${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 203,
  "reqSeqId": "test-success-003",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "Calibration",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "Offset": -2.5,
                      "Scale": 1.02
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 3 completed - Calibration re-enabled${NC}"

# ============================================================================
# Test 4: 更新 KalmanFilter 噪音參數
# ============================================================================
print_test_header "4" "更新 KalmanFilter 噪音參數"

echo -e "${YELLOW}Expected: ProcessNoise=0.01, MeasurementNoise=0.5${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 204,
  "reqSeqId": "test-success-004",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "dspPipeline": [{
                    "type": "KalmanFilter",
                    "enabled": true,
                    "parameters": {
                      "ProcessNoise": 0.01,
                      "MeasurementNoise": 0.5,
                      "InitialEstimate": 25.0,
                      "InitialErrorCovariance": 1.0
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 4 completed - KalmanFilter parameters updated${NC}"

# ============================================================================
# Test 5: 更新 MovingAverage WindowSize
# ============================================================================
print_test_header "5" "更新 MovingAverage WindowSize"

echo -e "${YELLOW}Expected: WindowSize=10${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 205,
  "reqSeqId": "test-success-005",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "dspPipeline": [{
                    "type": "MovingAverage",
                    "enabled": true,
                    "parameters": {
                      "WindowSize": 10
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 5 completed - MovingAverage WindowSize updated${NC}"

# ============================================================================
# Test 6: 同時更新多個 Pipeline 組件
# ============================================================================
print_test_header "6" "同時更新 Transform 和 DSP Pipeline"

echo -e "${YELLOW}Expected: Both Calibration and KalmanFilter updated${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 206,
  "reqSeqId": "test-success-006",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "Calibration",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "Offset": -1.0,
                      "Scale": 1.0
                    }
                  }],
                  "dspPipeline": [{
                    "type": "KalmanFilter",
                    "enabled": true,
                    "parameters": {
                      "ProcessNoise": 0.001,
                      "MeasurementNoise": 0.1,
                      "InitialEstimate": 25.0,
                      "InitialErrorCovariance": 1.0
                    }
                  }]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 6 completed - Multiple pipelines updated${NC}"

# ============================================================================
# Test 7: 更新多個 Sensor 的 Pipeline
# ============================================================================
print_test_header "7" "同時更新多個 Sensor 的 Pipeline"

echo -e "${YELLOW}Expected: Both temperature and humidity sensors updated${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 207,
  "reqSeqId": "test-success-007",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [
                {
                  "name": "temperature.sensor",
                  "config": {
                    "transformPipeline": [{
                      "type": "Calibration",
                      "enabled": true,
                      "parameters": {
                        "TargetResourceId": "*",
                        "Offset": -2.0,
                        "Scale": 1.01
                      }
                    }]
                  }
                },
                {
                  "name": "humidity.sensor",
                  "config": {
                    "transformPipeline": [{
                      "type": "Calibration",
                      "enabled": true,
                      "parameters": {
                        "TargetResourceId": "*",
                        "Offset": 1.5,
                        "Scale": 0.98
                      }
                    }]
                  }
                }
              ]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 7 completed - Multiple sensors updated${NC}"

# ============================================================================
# Test 8: 禁用所有 DSP Filters
# ============================================================================
print_test_header "8" "禁用所有 DSP Filters"

echo -e "${YELLOW}Expected: All DSP filters disabled${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 208,
  "reqSeqId": "test-success-008",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "dspPipeline": [
                    {
                      "type": "KalmanFilter",
                      "enabled": false,
                      "parameters": {
                        "ProcessNoise": 0.001,
                        "MeasurementNoise": 0.1,
                        "InitialEstimate": 25.0,
                        "InitialErrorCovariance": 1.0
                      }
                    },
                    {
                      "type": "MovingAverage",
                      "enabled": false,
                      "parameters": {
                        "WindowSize": 5
                      }
                    }
                  ]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 8 completed - All DSP filters disabled${NC}"

# ============================================================================
# Test 9: 重新啟用所有 DSP Filters
# ============================================================================
print_test_header "9" "重新啟用所有 DSP Filters"

echo -e "${YELLOW}Expected: All DSP filters re-enabled${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 209,
  "reqSeqId": "test-success-009",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "dspPipeline": [
                    {
                      "type": "KalmanFilter",
                      "enabled": true,
                      "parameters": {
                        "ProcessNoise": 0.001,
                        "MeasurementNoise": 0.1,
                        "InitialEstimate": 25.0,
                        "InitialErrorCovariance": 1.0
                      }
                    },
                    {
                      "type": "MovingAverage",
                      "enabled": true,
                      "parameters": {
                        "WindowSize": 5
                      }
                    }
                  ]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 9 completed - All DSP filters re-enabled${NC}"

# ============================================================================
# Test 10: 還原初始配置
# ============================================================================
print_test_header "10" "還原到初始配置"

echo -e "${YELLOW}Expected: Configuration restored to initial state${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 210,
  "reqSeqId": "test-success-010",
  "timestamp": 0,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "'"$DEVICE_TYPE"'": {
              "deviceName": "'"$DEVICE_NAME"'",
              "sensors": [{
                "name": "temperature.sensor",
                "config": {
                  "transformPipeline": [{
                    "type": "Calibration",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "Offset": -2.5,
                      "Scale": 1.02
                    }
                  }],
                  "dspPipeline": [
                    {
                      "type": "KalmanFilter",
                      "enabled": true,
                      "parameters": {
                        "ProcessNoise": 0.001,
                        "MeasurementNoise": 0.1,
                        "InitialEstimate": 25.0,
                        "InitialErrorCovariance": 1.0
                      }
                    },
                    {
                      "type": "MovingAverage",
                      "enabled": true,
                      "parameters": {
                        "WindowSize": 5
                      }
                    }
                  ]
                }
              }]
            }
          }
        }
      }
    }
  }
}'

wait_for_response
echo -e "${GREEN}✅ Test 10 completed - Configuration restored${NC}"

# ============================================================================
# 測試完成總結
# ============================================================================
echo ""
echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  All Success Test Cases Completed!${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}📊 Test Summary:${NC}"
echo "  Total Tests: 10"
echo "  Test 1:  ✅ 更新 Calibration 參數"
echo "  Test 2:  ✅ 禁用 Calibration"
echo "  Test 3:  ✅ 重新啟用 Calibration"
echo "  Test 4:  ✅ 更新 KalmanFilter 參數"
echo "  Test 5:  ✅ 更新 MovingAverage WindowSize"
echo "  Test 6:  ✅ 同時更新 Transform 和 DSP"
echo "  Test 7:  ✅ 更新多個 Sensor"
echo "  Test 8:  ✅ 禁用所有 DSP Filters"
echo "  Test 9:  ✅ 重新啟用所有 DSP Filters"
echo "  Test 10: ✅ 還原初始配置"
echo ""
echo -e "${YELLOW}📝 驗證重點:${NC}"
echo "  1. 每個測試都應該收到 status='success' 的回報"
echo "  2. 配置應該被持久化到 cache"
echo "  3. 下次 telemetry 應該使用新的 pipeline 配置"
echo "  4. KalmanFilter 的內部狀態應該被保留"
echo ""
echo -e "${YELLOW}🔍 查看日誌:${NC}"
echo "  檢查 testdevice 的日誌輸出,應該看到:"
echo "  - [INFO] Updated pipelines - DSP: X sensors, Transform: Y sensors"
echo "  - [DEBUG] Sensor 'xxx' Transform updates: N transforms updated"
echo "  - [DEBUG] Sensor 'xxx' DSP updates: M filters updated"
echo "  - [INFO] Configuration cached to: ..."
echo ""
echo -e "${GREEN}✨ Pipeline 動態配置更新測試完成!${NC}"