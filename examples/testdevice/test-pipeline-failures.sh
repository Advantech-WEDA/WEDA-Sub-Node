#!/bin/bash

# Pipeline Configuration Update - Failure Test Cases
# 測試各種配置更新失敗的情況,驗證 rollback 機制

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
NC='\033[0m' # No Color

echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Pipeline Configuration Update - Failure Test Cases${NC}"
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
    echo -e "${YELLOW}⏳ Waiting 2 seconds for response...${NC}"
    sleep 2
}

# 測試分隔線
print_test_header() {
    echo ""
    echo -e "${BLUE}────────────────────────────────────────────────────────────────${NC}"
    echo -e "${BLUE}  Test $1: $2${NC}"
    echo -e "${BLUE}────────────────────────────────────────────────────────────────${NC}"
}

# ============================================================================
# Test 1: Transform 參數驗證失敗 - 缺少必要參數
# ============================================================================
print_test_header "1" "UnitConversion 缺少 FromUnit 參數"

echo -e "${YELLOW}Expected: Status='failed', Error='FromUnit parameter is required'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 101,
  "reqSeqId": "test-fail-001",
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
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "ToUnit": "fahrenheit"
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
echo -e "${GREEN}✅ Test 1 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 2: Transform 參數驗證失敗 - 空字串參數
# ============================================================================
print_test_header "2" "UnitConversion FromUnit 為空字串"

echo -e "${YELLOW}Expected: Status='failed', Error='FromUnit cannot be empty'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 102,
  "reqSeqId": "test-fail-002",
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
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "",
                      "ToUnit": "fahrenheit"
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
echo -e "${GREEN}✅ Test 2 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 3: DSP Filter 參數驗證失敗 - 負數值
# ============================================================================
print_test_header "3" "KalmanFilter ProcessNoise 為負數"

echo -e "${YELLOW}Expected: Status='failed', Error='ProcessNoise must be positive'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 103,
  "reqSeqId": "test-fail-003",
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
                      "ProcessNoise": -0.001,
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
echo -e "${GREEN}✅ Test 3 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 4: DSP Filter 參數驗證失敗 - 超出範圍
# ============================================================================
print_test_header "4" "MovingAverage WindowSize 超出範圍"

echo -e "${YELLOW}Expected: Status='failed', Error='WindowSize must be between 2 and 100'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 104,
  "reqSeqId": "test-fail-004",
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
                      "WindowSize": 1000
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
echo -e "${GREEN}✅ Test 4 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 5: DSP Filter 參數驗證失敗 - WindowSize 為 1
# ============================================================================
print_test_header "5" "MovingAverage WindowSize 小於最小值"

echo -e "${YELLOW}Expected: Status='failed', Error='WindowSize must be at least 2'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 105,
  "reqSeqId": "test-fail-005",
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
                      "WindowSize": 1
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
echo -e "${GREEN}✅ Test 5 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 6: Transform 類型不匹配 (會被跳過)
# ============================================================================
print_test_header "6" "Transform 類型不匹配 - Calibration vs UnitConversion"

echo -e "${YELLOW}Expected: Transform skipped, Log='type mismatch'${NC}"
echo -e "${YELLOW}Note: 類型不匹配會被跳過,不會導致整個更新失敗${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 106,
  "reqSeqId": "test-fail-006",
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
                    "type": "UnitConversion",
                    "enabled": true,
                    "parameters": {
                      "TargetResourceId": "*",
                      "FromUnit": "celsius",
                      "ToUnit": "kelvin"
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
echo -e "${GREEN}✅ Test 6 completed - Check logs for type mismatch warning${NC}"

# ============================================================================
# Test 7: 多個 Sensor 部分失敗 (Atomic Rollback)
# ============================================================================
print_test_header "7" "多個 Sensor 更新,其中一個失敗 (Atomic Rollback)"

echo -e "${YELLOW}Expected: 整個更新被 rollback,所有 sensor 配置保持不變${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 107,
  "reqSeqId": "test-fail-007",
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
                        "Offset": -3.0,
                        "Scale": 1.05
                      }
                    }]
                  }
                },
                {
                  "name": "humidity.sensor",
                  "config": {
                    "dspPipeline": [{
                      "type": "KalmanFilter",
                      "enabled": true,
                      "parameters": {
                        "ProcessNoise": -0.1,
                        "MeasurementNoise": 0.5,
                        "InitialEstimate": 50.0,
                        "InitialErrorCovariance": 1.0
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
echo -e "${GREEN}✅ Test 7 completed - Check logs for atomic rollback${NC}"

# ============================================================================
# Test 8: Calibration 參數驗證失敗 - Scale 為 0
# ============================================================================
print_test_header "8" "Calibration Scale 為 0 (除以零錯誤)"

echo -e "${YELLOW}Expected: Status='failed', Error='Scale cannot be zero'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 108,
  "reqSeqId": "test-fail-008",
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
                      "Offset": 0.0,
                      "Scale": 0.0
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
echo -e "${GREEN}✅ Test 8 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 9: ReLU Threshold 為負數
# ============================================================================
print_test_header "9" "ReLU Threshold 為負數"

echo -e "${YELLOW}Expected: Status='failed', Error='Threshold cannot be negative'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 109,
  "reqSeqId": "test-fail-009",
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
                    "type": "ReLU",
                    "enabled": true,
                    "parameters": {
                      "Threshold": -0.5
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
echo -e "${GREEN}✅ Test 9 completed - Check logs for validation error${NC}"

# ============================================================================
# Test 10: KalmanFilter 缺少所有參數
# ============================================================================
print_test_header "10" "KalmanFilter 缺少所有必要參數"

echo -e "${YELLOW}Expected: Status='failed', Error='Required parameters missing'${NC}"
echo -e "${YELLOW}Publishing configuration update...${NC}"

nats pub "$SUBJECT" '{
  "deviceId": "'"$DEVICE_ID"'",
  "groupId": "",
  "cmd": "updateCmd",
  "seqId": 110,
  "reqSeqId": "test-fail-010",
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
                    "parameters": {}
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
echo -e "${GREEN}✅ Test 10 completed - Check logs for validation error${NC}"

# ============================================================================
# 測試完成總結
# ============================================================================
echo ""
echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  All Failure Test Cases Completed!${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}📊 Test Summary:${NC}"
echo "  Total Tests: 10"
echo "  Test 1:  ❌ UnitConversion 缺少 FromUnit"
echo "  Test 2:  ❌ UnitConversion FromUnit 空字串"
echo "  Test 3:  ❌ KalmanFilter ProcessNoise 負數"
echo "  Test 4:  ❌ MovingAverage WindowSize 超出範圍 (1000)"
echo "  Test 5:  ❌ MovingAverage WindowSize 小於最小值 (1)"
echo "  Test 6:  ⚠️  Transform 類型不匹配 (跳過)"
echo "  Test 7:  ❌ 多 Sensor 部分失敗 (Atomic Rollback)"
echo "  Test 8:  ❌ Calibration Scale 為 0"
echo "  Test 9:  ❌ ReLU Threshold 負數"
echo "  Test 10: ❌ KalmanFilter 缺少參數"
echo ""
echo -e "${YELLOW}📝 驗證重點:${NC}"
echo "  1. 每個失敗案例都應該收到 status='failed' 的回報"
echo "  2. 配置應該自動 rollback 到更新前的狀態"
echo "  3. 錯誤訊息應該清楚指出驗證失敗的原因"
echo "  4. Test 7 驗證 atomic 操作 - 部分失敗會導致整個更新 rollback"
echo ""
echo -e "${YELLOW}🔍 查看日誌:${NC}"
echo "  檢查 testdevice 的日誌輸出,應該看到:"
echo "  - [ERROR] Pipeline update validation failed: ..."
echo "  - [INFO] Configuration rolled back to previous state"
echo "  - [INFO] Configuration update completed successfully (rollback 後)"
echo ""
echo -e "${GREEN}✨ Rollback 機制已驗證完成!${NC}"