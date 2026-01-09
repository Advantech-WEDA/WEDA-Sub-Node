#!/bin/bash
# Run this script ON THE DEVICE (172.22.160.197)
# Usage: bash monitor-advantech.sh

echo "===== Memory Test WITH ADVANTECH SENSORS ====="
echo "Device: SystemMonitor-Advantech-ARM64"
echo "Start Time: $(date)"
echo ""
echo "Sample | Timestamp           | Memory(MB) | CPU%  | Uptime"
echo "-------|---------------------|------------|-------|--------"

for i in $(seq 1 288); do
  timestamp=$(date +"%Y-%m-%d %H:%M:%S")
  stats=$(docker stats system-monitor --no-stream --format "{{.MemUsage}}|||{{.CPUPerc}}")
  mem=$(echo "$stats" | cut -d'|' -f1 | xargs)
  cpu=$(echo "$stats" | cut -d'|' -f4 | xargs)
  uptime_sec=$(docker inspect system-monitor --format='{{.State.StartedAt}}' | xargs -I {} date -d {} +'%s')
  now=$(date +'%s')
  uptime_mins=$(( (now - uptime_sec) / 60 ))
  
  printf "%6d | %s | %10s | %5s | %dm\n" $i "$timestamp" "$mem" "$cpu" $uptime_mins
  
  # Every hour (12 samples), log to file
  if [ $((i % 12)) -eq 0 ]; then
    echo "[$timestamp] Sample $i: Memory=$mem, CPU=$cpu, Uptime=${uptime_mins}m" >> /tmp/memory-test-advantech.log
  fi
  
  sleep 300  # 5 minutes
done

echo ""
echo "===== Test Complete: $(date) ====="
