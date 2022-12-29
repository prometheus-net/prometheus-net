#!/bin/bash

set -e

# Replace any env variables in the template, yielding the final configuration file.
envsubst < /app/prometheus.yml > /etc/prometheus/prometheus.yml

# We must listen on 0.0.0.0 here because otherwise the liveness/readiness probes cannot reach us.
exec /bin/prometheus --web.listen-address=0.0.0.0:$PROMETHEUS_PORT --storage.tsdb.retention.time=5m --storage.tsdb.min-block-duration=2m --config.file=/etc/prometheus/prometheus.yml --storage.tsdb.path=/prometheus --web.console.libraries=/usr/share/prometheus/console_libraries --web.console.templates=/usr/share/prometheus/consoles --enable-feature=exemplar-storage