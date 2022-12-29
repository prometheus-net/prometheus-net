FROM mcr.microsoft.com/cbl-mariner/base/core:2.0

# gettext is used by run.sh to do env var replacement
# ca-certificates-microsoft lets us access github.com for download
RUN tdnf repolist --refresh \
	&& tdnf install -y gettext ca-certificates-microsoft

# Install dependencies 
RUN tdnf install -y \
	wget \
	tar \
	gzip

################### Based on https://github.com/prometheus/prometheus/blob/main/Dockerfile

ARG PROMETHEUS_PACKAGE_NAME=prometheus-2.41.0.linux-amd64
RUN wget --no-verbose -O prometheus.tar.gz https://github.com/prometheus/prometheus/releases/download/v2.41.0/$PROMETHEUS_PACKAGE_NAME.tar.gz
RUN tar xvfz prometheus.tar.gz
RUN cp /$PROMETHEUS_PACKAGE_NAME/prometheus /bin/
RUN cp /$PROMETHEUS_PACKAGE_NAME/promtool /bin/
RUN mkdir /usr/share/prometheus
RUN cp -r /$PROMETHEUS_PACKAGE_NAME/console_libraries/ /usr/share/prometheus/console_libraries/
RUN cp -r /$PROMETHEUS_PACKAGE_NAME/consoles/ /usr/share/prometheus/consoles/

RUN mkdir -p /prometheus && mkdir -p /etc/prometheus

################### Custom logic start

WORKDIR /app

COPY run.sh .
RUN chmod +x /app/run.sh

# This is the config template. run.sh will inject env variables into it.
COPY prometheus.yml .

ENTRYPOINT ["/app/run.sh"]