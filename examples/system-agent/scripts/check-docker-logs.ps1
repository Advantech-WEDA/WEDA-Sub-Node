# Docker 日誌驅動檢查腳本
# 用於確認裝置上 Docker 使用的日誌驅動和配置

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Docker 日誌驅動檢查" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[檢查 Docker 全局日誌驅動]" -ForegroundColor Yellow
Write-Host "在裝置上執行: docker info --format '{{.LoggingDriver}}'" -ForegroundColor Gray
Write-Host ""

Write-Host "[檢查 system-monitor 容器日誌驅動]" -ForegroundColor Yellow
Write-Host "在裝置上執行: docker inspect system-monitor --format='{{.HostConfig.LogConfig.Type}}'" -ForegroundColor Gray
Write-Host ""

Write-Host "[檢查 Docker 日誌檔案大小]" -ForegroundColor Yellow
Write-Host "在裝置上執行: docker inspect system-monitor --format='{{.LogPath}}' | xargs ls -lh" -ForegroundColor Gray
Write-Host "或: sudo du -sh /var/lib/docker/containers/*/*.log" -ForegroundColor Gray
Write-Host ""

Write-Host "[檢查 syslog 大小]" -ForegroundColor Yellow
Write-Host "在裝置上執行: ls -lh /var/log/syslog*" -ForegroundColor Gray
Write-Host "或: sudo du -sh /var/log/" -ForegroundColor Gray
Write-Host ""

Write-Host "[檢查 journald 配置]" -ForegroundColor Yellow
Write-Host "在裝置上執行: journalctl --disk-usage" -ForegroundColor Gray
Write-Host "檢查是否有 ForwardToSyslog: cat /etc/systemd/journald.conf | grep ForwardToSyslog" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "三種驅動說明" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[json-file (預設)]" -ForegroundColor Green
Write-Host "  位置: /var/lib/docker/containers/<container-id>/<container-id>-json.log" -ForegroundColor Gray
Write-Host "  特性: Docker 自己管理，可配置 max-size 和 max-file" -ForegroundColor Gray
Write-Host "  影響: 不影響 /var/log/syslog" -ForegroundColor Gray
Write-Host "  清理: Docker 自動輪替，或 docker logs --tail 清空" -ForegroundColor Gray
Write-Host ""

Write-Host "[journald]" -ForegroundColor Yellow
Write-Host "  位置: /var/log/journal/ 或 /run/log/journal/" -ForegroundColor Gray
Write-Host "  特性: 由 systemd 管理，可能轉發到 syslog" -ForegroundColor Gray
Write-Host "  影響: 如果 ForwardToSyslog=yes，會同步到 /var/log/syslog" -ForegroundColor Gray
Write-Host "  清理: journalctl --vacuum-size=100M 或 --vacuum-time=7d" -ForegroundColor Gray
Write-Host ""

Write-Host "[syslog]" -ForegroundColor Red
Write-Host "  位置: /var/log/syslog (直接寫入)" -ForegroundColor Gray
Write-Host "  特性: 由 rsyslog 管理" -ForegroundColor Gray
Write-Host "  影響: 直接影響 /var/log/syslog 大小" -ForegroundColor Gray
Write-Host "  清理: logrotate 自動輪替 (預設每天)" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "如何確定是否是 system-monitor 造成" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[方法 1: 檢查日誌內容]" -ForegroundColor Yellow
Write-Host "sudo tail -n 100 /var/log/syslog | grep system-monitor" -ForegroundColor Gray
Write-Host "sudo tail -n 100 /var/log/syslog | grep 'LocalSystemMonitorDevice\\|SystemMetricsParser'" -ForegroundColor Gray
Write-Host ""

Write-Host "[方法 2: 監控增長速度]" -ForegroundColor Yellow
Write-Host "# 記錄當前大小" -ForegroundColor Gray
Write-Host "ls -lh /var/log/syslog" -ForegroundColor Gray
Write-Host "# 等待 1 分鐘" -ForegroundColor Gray
Write-Host "sleep 60" -ForegroundColor Gray
Write-Host "# 再次檢查" -ForegroundColor Gray
Write-Host "ls -lh /var/log/syslog" -ForegroundColor Gray
Write-Host ""

Write-Host "[方法 3: 停止容器觀察]" -ForegroundColor Yellow
Write-Host "# 停止 system-monitor" -ForegroundColor Gray
Write-Host "docker stop system-monitor" -ForegroundColor Gray
Write-Host "# 觀察 syslog 是否停止增長 (等待 5 分鐘)" -ForegroundColor Gray
Write-Host "# 重新啟動" -ForegroundColor Gray
Write-Host "docker start system-monitor" -ForegroundColor Gray
Write-Host ""
