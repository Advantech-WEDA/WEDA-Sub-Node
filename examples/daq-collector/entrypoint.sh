#!/bin/bash
set -e

# Copy Advantech DAQNavi libraries to system lib directory
if [ -d "/opt/advantech/libs" ]; then
    echo "Copying Advantech DAQNavi libraries from /opt/advantech/libs to /usr/lib..."
    cp -v /opt/advantech/libs/* /usr/lib/ 2>/dev/null || true
    echo "Library copy completed"
fi

# Execute the main application
exec /app/daq-data-collector "$@"
