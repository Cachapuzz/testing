curl -L -O https://artifacts.elastic.co/downloads/beats/elastic-agent/elastic-agent-8.15.1-darwin-aarch64.tar.gz
tar xzvf elastic-agent-8.15.1-darwin-aarch64.tar.gz
cd elastic-agent-8.15.1-darwin-aarch64
sudo ./elastic-agent install \
  --fleet-server-es=http://localhost:9200 \
  --fleet-server-service-token=AAEAAWVsYXN0aWMvZmxlZXQtc2VydmVyL3Rva2VuLTE3MjgzODU3NjAzNjQ6bzFXTFhGdlJUUm1VMEhBRXQ3eF9rQQ \
  --fleet-server-policy=fleet-server-policy \
  --fleet-server-port=8220