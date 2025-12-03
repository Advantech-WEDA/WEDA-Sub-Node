#!/bin/bash

# Transform Real-time Update Test Script
# Usage: ./test-transform.sh <test_number>
# Tests: 1=disable, 2=enable, 3=f2c, 4=kelvin, 5=celsius, 6=invalid

NATS_URL="${NATS_URL:-nats://172.22.160.197:4224}"
DEVICE_ID="Test789"
SUBJECT="weda.core.msg.${DEVICE_ID}.config.update"

case "$1" in
  1)
    echo "Test 1: Disable UnitConversion Transform"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 1,
      "reqSeqId": "test-001",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": false,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "celsius", "ToUnit": "fahrenheit"}
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
    echo "Expected: Temperature should show raw Celsius (~25°C)"
    ;;

  2)
    echo "Test 2: Re-enable UnitConversion Transform"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 2,
      "reqSeqId": "test-002",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": true,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "celsius", "ToUnit": "fahrenheit"}
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
    echo "Expected: Temperature should show Fahrenheit (~77°F)"
    ;;

  3)
    echo "Test 3: Change to Fahrenheit -> Celsius"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 3,
      "reqSeqId": "test-003",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": true,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "fahrenheit", "ToUnit": "celsius"}
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
    echo "Expected: Temperature (~-3.89°C if raw is 25)"
    ;;

  4)
    echo "Test 4: Change to Kelvin"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 4,
      "reqSeqId": "test-004",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": true,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "celsius", "ToUnit": "kelvin"}
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
    echo "Expected: Temperature should show Kelvin (~298K)"
    ;;

  5)
    echo "Test 5: Restore to Celsius (pass-through)"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 5,
      "reqSeqId": "test-005",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": true,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "celsius", "ToUnit": "celsius"}
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
    echo "Expected: Temperature should show raw Celsius (~25°C)"
    ;;

  6)
    echo "Test 6: Invalid - Empty FromUnit"
    nats pub --server="$NATS_URL" "$SUBJECT" '{
      "deviceId": "Test789",
      "cmd": "updateCmd",
      "seqId": 6,
      "reqSeqId": "test-006",
      "data": {
        "cfg": {
          "desired": {
            "subNodeDeviceConfig": {
              "deviceConfigs": {
                "MyFirstDeviceConfig": {
                  "deviceName": "Test789",
                  "sensors": [{
                    "name": "temperature.sensor",
                    "config": {
                      "transformPipeline": [{
                        "type": "UnitConversion",
                        "enabled": true,
                        "parameters": {"TargetResourceId": "*", "FromUnit": "", "ToUnit": "fahrenheit"}
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
    echo "Expected: Should report status='invalid' with error message"
    ;;

  *)
    echo "Transform Real-time Update Test Script"
    echo "======================================="
    echo "Usage: $0 <test_number>"
    echo ""
    echo "Tests:"
    echo "  1 - Disable UnitConversion Transform (Enabled=false)"
    echo "  2 - Re-enable UnitConversion Transform"
    echo "  3 - Change to Fahrenheit -> Celsius conversion"
    echo "  4 - Change to Celsius -> Kelvin conversion"
    echo "  5 - Restore to Celsius -> Celsius (pass-through)"
    echo "  6 - Invalid test (empty FromUnit)"
    echo ""
    echo "Environment:"
    echo "  NATS_URL=${NATS_URL}"
    ;;
esac
