# Testing ELK and Prometheus with Golang

Simple setup based on [elastic search with docker-compose](https://www.elastic.co/guide/en/elasticsearch/reference/current/docker.html#docker-compose-file).

1. How to run

```bash
docker-compose up -d
```

After that access Kibana at [http://localhost:5601](http://localhost:5601).

- username: `elastic`
- password: `elastic123`

2. How to stop

```bash
docker compose down -v
```

3. Golang logs on Kibana

![Logs](./assets/logs.png)

3.1 APM test app on [http://localhost:8000](http://localhost:8000)

4. Prometheus

Access Prometheus at [http://localhost:9090](http://localhost:9090).

Access Pushgateway at [http://localhost:9091](http://localhost:9091).

5. Grafana

Access Grafana at [http://localhost:3000](http://localhost:3000).

- username: `admin`
- password: `admin`


## Portainer 

Access Portainer at [http://localhost:9000](http://localhost:9000).

- username: `admin`
- password: `superpassword`

## Install Ansible

```bash
sudo apt install ansible -y
```

## Run Ansible-Playbook

```bash
sudo ansible-playbook -i inventory.yml ansible-playbook.yml --ask-vault-pass
```

### Invetory.yml vault password

```bash
passwordmuitoforte
```

### .env vault password

```bash
passwordsupersegura
```

### Encrypt or Decrypt with Ansible-Vault

```bash
ansible-vault encrypt filename
```

```bash
ansible-vault decrypt filename
```
