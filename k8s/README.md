# ONG Donation Worker - Kubernetes Manifests

## Overview

Manifests Kubernetes para o deployment do ONG.Donation.Worker (Background Worker) no Azure Kubernetes Service (AKS).

O Worker é responsável por processar mensagens de doações via RabbitMQ/Service Bus e atualizar o status dos pagamentos.

## Architecture

```
┌──────────────────────────────────────────────────┐
│      RabbitMQ / Azure Service Bus                │
│   (Shared with ONG.Donation WebAPI)             │
└────────────┬─────────────────────────────────────┘
             │
             ▼
┌──────────────────────────────────────────────────┐
│   ONG Donation Worker Deployment (HPA)           │
│   - Replicas: 2-5 (Auto-scaled)                 │
│   - Message consuming                           │
│   - Payment processing                          │
│   - Dead letter queue handling                  │
└────────────┬─────────────────────────────────────┘
             │
        ┌────┴────┬─────────┬─────────┐
        ▼         ▼         ▼         ▼
    [Pod 1]  [Pod 2]  [Pod N]   [Database]
    ├─Logs
    ├─Secrets (Key Vault CSI)
    ├─Config (ConfigMap)
    └─Health Checks (RabbitMQ, DB)
            │
            ▼
    ┌──────────────────┐
    │   SQL Database   │
    │  (Payments DB)   │
    └──────────────────┘
```

## Files

### 1. namespace.yaml
Define um namespace isolado para o Worker.

**Recursos:**
- `Namespace`: ong-donation-worker

### 2. configmap.yaml
Armazena configurações de aplicação em variáveis de ambiente.

**Recursos:**
- `ConfigMap`: ong-donation-worker-config (todas as configurações não-sensíveis)
- `ConfigMap`: ong-donation-worker-logging-config (configuração de logging)
- `ConfigMap`: ong-donation-worker-scripts (scripts auxiliares)

**Variáveis incluídas:**
- DOTNET_ENVIRONMENT
- Database configuration
- RabbitMQ configuration
- Worker thread and batch configuration
- Payment processor configuration
- Loki configuration
- Application Insights configuration

### 3. secretproviderclass.yaml
Integra com Azure Key Vault via CSI Driver (Secrets Store).

**Recursos:**
- `ServiceAccount`: ong-donation-worker
- `AzureIdentity`: Workload Identity para autenticação sem credenciais
- `AzureIdentityBinding`: Liga o Workload Identity ao Pod
- `SecretProviderClass`: Define quais secrets serão injetados do Key Vault

**Secrets injetados:**
- db-connection-string (Payments database)
- service-bus-connection-string (RabbitMQ)
- appinsights-connection-string

### 4. deployment.yaml
Deploy do Worker com configurações avançadas.

**Recursos:**
- `Deployment`: ong-donation-worker
- `PodDisruptionBudget`: Garante mínimo de replicas disponíveis
- `HorizontalPodAutoscaler`: Auto-scaling baseado em CPU/Memory

**Características:**
- 2 replicas iniciais (escala até 5)
- Rolling updates (maxSurge: 1, maxUnavailable: 0)
- Security context (non-root, read-only filesystem)
- Resource limits (256Mi-512Mi memory, 200m-500m CPU)
- Liveness, Readiness, Startup probes
- Pod anti-affinity (spread entre nodes)
- Graceful shutdown (10s sleep antes de kill)
- Environment variables dinâmicas
- NetworkPolicy (egress apenas para RabbitMQ, Database, Loki)

**Probes:**
- Liveness: Verifica se o worker está vivo (HTTP health check)
- Readiness: Verifica se o worker está pronto para processar mensagens
- Startup: Aguarda startup (até 20 tentativas de 5s = 100s)

## Deployment Steps

### Prerequisites

```bash
# 1. Instalar cert-manager (para Ingress, se necessário)
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# 2. Instalar Secrets Store CSI Driver
kubectl apply -f https://raw.githubusercontent.com/Azure/secrets-store-csi-driver-provider-azure/master/deployment/provider-azure-installer.yaml

# 3. Instalar AAD Pod Identity
helm install aad-pod-identity aad-pod-identity/aad-pod-identity -n aad-pod-identity --create-namespace

# 4. Provisionar Terraform (ONG.Donation primeiro)
cd ONG.Donation/terraform
terraform init
terraform apply -var-file=terraform.tfvars

# 5. Provisionar Terraform (ONG.Donation.Worker)
cd ONG.Donation.Worker/terraform
terraform init
terraform apply -var-file=terraform.tfvars
```

### Deploy Worker

```bash
# 1. Create namespace
kubectl apply -f namespace.yaml

# 2. Create ConfigMap
kubectl apply -f configmap.yaml

# 3. Setup Workload Identity and Secrets
# - Atualizar os placeholders em secretproviderclass.yaml:
#   - {SUBSCRIPTION_ID}
#   - {RESOURCE_GROUP}
#   - {WORKER_CLIENT_ID} (do Terraform output worker_identity_client_id)
#   - {TENANT_ID}
kubectl apply -f secretproviderclass.yaml

# 4. Deploy application
kubectl apply -f deployment.yaml
```

## Monitoramento

### Health Checks
```bash
# Verificar status dos pods
kubectl get pods -n ong-donation-worker -w

# Verificar logs em tempo real
kubectl logs -f deployment/ong-donation-worker -n ong-donation-worker

# Ver eventos
kubectl describe pod <pod-name> -n ong-donation-worker
```

### Métricas
```bash
# CPU e Memory usage
kubectl top pods -n ong-donation-worker

# HPA status
kubectl get hpa ong-donation-worker-hpa -n ong-donation-worker -w

# Detalhes do deployment
kubectl describe deployment ong-donation-worker -n ong-donation-worker
```

### Message Processing
```bash
# Conectar ao pod e verificar logs
kubectl exec -it <pod-name> -n ong-donation-worker -- /bin/bash

# Verificar se o worker está conectado ao RabbitMQ
kubectl logs <pod-name> -n ong-donation-worker | grep -i "connected\|connecting"

# Monitorar processamento de mensagens
kubectl logs -f <pod-name> -n ong-donation-worker | grep -i "processing\|completed\|error"
```

## Troubleshooting

### Pod não está iniciando
```bash
# Verificar logs
kubectl logs deployment/ong-donation-worker -n ong-donation-worker

# Verificar eventos
kubectl describe pod <pod-name> -n ong-donation-worker

# Verificar secrets
kubectl get secret -n ong-donation-worker
kubectl describe secret <secret-name> -n ong-donation-worker
```

### Worker não está processando mensagens
```bash
# Verificar conectividade com RabbitMQ
kubectl run -it --rm debug --image=mcr.microsoft.com/powershell --restart=Never -n ong-donation-worker -- pwsh
# Dentro do pod: Test-NetConnection -ComputerName rabbitmq-host -Port 5672

# Verificar logs detalhados
kubectl logs deployment/ong-donation-worker -n ong-donation-worker --all-containers --tail=100

# Verificar se há backlog de mensagens
# (Conectar ao console de gerenciamento do RabbitMQ)
```

### Falha na autenticação com Key Vault
```bash
# Verificar Workload Identity
kubectl get aadidentity -n ong-donation-worker
kubectl get aadidentitybinding -n ong-donation-worker

# Verificar logs do NMI (Node Managed Identity)
kubectl logs -n aad-pod-identity deployment/aad-pod-identity-nmi | grep -i "ong-donation-worker"

# Verificar se o Secret Provider Class está montado
kubectl exec <pod-name> -n ong-donation-worker -- ls -la /mnt/secrets-store
```

### Database connection fails
```bash
# Testar conectividade
kubectl run -it --rm debug --image=mcr.microsoft.com/mssql-tools --restart=Never -n ong-donation-worker -- bash
# Dentro do pod: sqlcmd -S <server-name>.database.windows.net -U <user> -P <password> -Q "SELECT 1"

# Verificar NSG rules
az network nsg rule list -g <resource-group> -n <nsg-name>

# Verificar firewall rules do SQL Server
az sql server firewall-rule list -g <resource-group> -s <server-name>
```

## Performance Tuning

### Aumentar throughput de processamento
```yaml
# No deployment.yaml, aumentar:
WORKER_THREAD_COUNT: "8"           # Aumentar de 4
WORKER_BATCH_SIZE: "20"             # Aumentar de 10
WORKER_MAX_CONCURRENT_MESSAGES: "20" # Aumentar de 10

# Aumentar replicas
minReplicas: 3
maxReplicas: 10
```

### Otimizar resource allocation
```yaml
# Se CPU está limitando:
resources:
  requests:
    cpu: "250m"
  limits:
    cpu: "750m"

# Se memory está limitando:
resources:
  requests:
    memory: "512Mi"
  limits:
    memory: "1Gi"
```

## Logging e Observabilidade

### Estrutura de logs
O Worker emite logs estruturados (JSON) com as seguintes informações:
- Timestamp
- Log level (Information, Warning, Error)
- Service name (ONG.Donation.Worker)
- Message ID (para rastreamento)
- Processing status
- Performance metrics (duration, throughput)

### Integração com Loki
Os logs são enviados automaticamente para Loki, podendo ser consultados via:
```
{job="ong-donation-worker"} | json
{job="ong-donation-worker"} | json | error_level="Error"
```

## Variables a configurar

No arquivo `secretproviderclass.yaml`, substituir:
- `{SUBSCRIPTION_ID}`: Azure Subscription ID
- `{RESOURCE_GROUP}`: Nome do Resource Group
- `{WORKER_CLIENT_ID}`: Client ID da User Managed Identity criada pelo Terraform
- `{TENANT_ID}`: Azure Tenant ID

No arquivo `deployment.yaml`:
- `{ACR_LOGIN_SERVER}`: Login server do Azure Container Registry

## Best Practices aplicadas

1. **Message Processing**
   - Prefetch count controlado
   - Batch processing com timeout
   - Dead letter queue habilitado
   - Retry logic com exponential backoff

2. **Reliability**
   - Multiple replicas para alta disponibilidade
   - Pod Disruption Budgets
   - Graceful shutdown (aguarda processamento de mensagens)
   - Health checks integrados

3. **Security**
   - Non-root user
   - Read-only filesystem
   - Network policies restritivas
   - Secret management via Key Vault
   - Workload Identity (sem credenciais)

4. **Performance**
   - Resource limits apropriados para background worker
   - Connection pooling
   - Batch processing
   - HPA com métricas customizadas

5. **Observability**
   - Structured logging
   - Application Insights
   - Prometheus metrics
   - Distributed tracing support
