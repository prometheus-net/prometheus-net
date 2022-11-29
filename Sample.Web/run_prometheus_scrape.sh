#!/bin/bash

# Runs a prometheus scrape on an endpoint with exemplar storage enabled. localhost:9090 will take you to the prometheus 
# ui for verification.

cat <<EOF > prometheus.yml
scrape_configs:
  - job_name: 'prometheus-net'
    scrape_interval: 5s
    static_configs:
      - targets: ['$1']
EOF

docker run --rm -it --name prometheus -p 9090:9090 \
    -v $PWD/prometheus.yml:/etc/prometheus/prometheus.yml \
    prom/prometheus \
    --config.file=/etc/prometheus/prometheus.yml \
    --log.level=debug \
    --enable-feature=exemplar-storage