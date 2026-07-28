[README.md](https://github.com/user-attachments/files/30440755/README.md)
# ONG.Donation.Worker

Worker service que processa eventos de pagamento de doações em uma plataforma de doações para ONGs. Escuta uma fila do Azure Service Bus, processa pagamentos (simulado), persiste resultados em SQL Server e publica o resultado em uma fila de saída.

## Stack

| Tecnologia | Versão |
|---|---|
| .NET | 10.0 |
| Azure Service Bus | 7.18.4 |
| Entity Framework Core | 10.0.9 |
| SQL Server | — |
| Serilog + Grafana Loki | 4.3.1 / 9.0.1 |
| Docker | Multi-stage |
| Kubernetes | AKS |
| CI/CD | Azure Pipelines |

## Arquitetura

```
                    ┌───────────────────────────────┐
                    │  Azure Service Bus            │
                    │  (donation.payment)           │
                    └──────────────┬────────────────┘
                                   │
                                   ▼
                    ┌───────────────────────────────┐
                    │  ONG.Donation.Worker          │
                    │  ┌────────────────────────┐   │
                    │  │ ServiceBusDonationConsumer │
                    │  │ (BackgroundService)    │   │
                    │  └────────┬───────────────┘   │
                    │           │                   │
                    │     ┌─────┴─────┐             │
                    │     │ Payment   │             │
                    │     │ Processor │             │
                    │     └─────┬─────┘             │
                    └───────────┼───────────────────┘
                                │
                    ┌───────────┴───────────────┐
                    │       SQL Server          │
                    │   (ONGDonationPayments)   │
                    └───────────────────────────┘
                                │
                    ┌───────────┴────────────────┐
                    │  Azure Service Bus         │
                    │  (donation.payment.result) │
                    └────────────────────────────┘
```

Organizado em **3 camadas** seguindo princípios de Clean Architecture:

- **Domain** (`ONG.Donation.Worker.Domain`) — Núcleo puro sem dependências externas. Contém entidades, enums, eventos de domínio e interfaces (ports).
- **Infrastructure** (`ONG.Donation.Worker.Infrastructure`) — Implementações concretas: EF Core DbContext, repositórios, publicador Service Bus e migrations.
- **Worker** (`ONG.Donation.Worker`) — Hosted service que orquestra o consumo de mensagens e coordena o fluxo.

### Padrões utilizados

- **Background Service** — `ServiceBusDonationConsumer` estende `BackgroundService`
- **Domain Events** — Eventos de domínio como records (`DonationCreatedEvent`, `DonationPaymentProcessedEvent`, `DonationPaymentFailedEvent`)
- **Event Publisher** — Abstração `IEventPublisher` implementada por `ServiceBusEventPublisher`
- **Repository** — `PaymentRepository` encapsula acesso a dados
- **Idempotent Consumer** — Verifica se o pagamento já foi processado antes de prosseguir

## Fluxo de funcionamento

1. O worker escuta a fila `donation.payment` no Azure Service Bus
2. Cada mensagem é desserializada para `DonationCreatedEvent` (DonationId, CampaignId, DonorId, Amount, OccurredAt)
3. Verifica idempotência: se já existe um `Payment` com o mesmo `DonationId`, a mensagem é ignorada
4. Cria um registro `Payment` com status `Pendente`
5. Processa o pagamento (simulado — sempre retorna sucesso)
6. Em caso de sucesso: atualiza status para `Processada` e publica `DonationPaymentProcessedEvent` na fila `donation.payment.result`
7. Em caso de falha: atualiza status para `Falhou` e publica `DonationPaymentFailedEvent` com o motivo
8. A mensagem é confirmada (`CompleteMessage`) ou abandonada (`AbandonMessage`) conforme o resultado

## Pré-requisitos

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (local ou Docker)
- Azure Service Bus (ou [Service Bus Emulator](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-emulator-overview))
- Grafana Loki (opcional, para envio de logs)

## Como executar localmente

### 1. Configurar o banco de dados

Crie o banco `ONGDonationPayments` no SQL Server local.

### 2. Configurar o Service Bus

Inicie o Service Bus Emulator ou use uma instância do Azure Service Bus. O arquivo `sbemulator-config.json` define as filas:

- `donation.payment` — fila de entrada consumida pelo worker
- `donation.payment.result` — fila onde os resultados são publicados

### 3. Configurar appsettings

Edite `src/ONG.Donation.Worker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "WorkerConnection": "Server=localhost;Database=ONGDonationPayments;User Id=sa;Password=your_password;TrustServerCertificate=True;"
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;...;UseDevelopmentEmulator=true",
    "QueueName": "donation.payment",
    "ResultQueueName": "donation.payment.result"
  },
  "Loki": {
    "Url": "http://localhost:3100"
  }
}
```

### 4. Executar

```bash
cd src/ONG.Donation.Worker
dotnet run
```

Na inicialização, o worker:
- Configura Serilog (console + Loki)
- Aplica migrations do EF Core automaticamente (cria a tabela `Payments`)
- Inicia o processor do Service Bus e começa a escutar a fila `donation.payment`

### 5. Enviar uma mensagem de teste

Publique na fila `donation.payment`:

```json
{
  "DonationId": 1,
  "CampaignId": 10,
  "DonorId": 100,
  "Amount": 50.00,
  "OccurredAt": "2026-07-27T00:00:00Z"
}
```

## Docker

```bash
docker build -f src/ONG.Donation.Worker/Dockerfile -t ong-donation-worker .
docker run --rm ong-donation-worker
```

## Kubernetes

Manifests completos em [`k8s/`](k8s/) para deploy no AKS, incluindo:

- Deployment com HPA (2–5 réplicas), PDB, NetworkPolicy
- ConfigMap com variáveis de ambiente
- SecretProviderClass para integração com Azure Key Vault via Workload Identity
- Health probes (liveness, readiness, startup)

Consulte [`k8s/README.md`](k8s/README.md) para instruções detalhadas de deploy.

## Infraestrutura (Terraform)

A infraestrutura Terraform foi centralizada no repositório `fcg-infra`. Consulte [`.terraform-info.md`](.terraform-info.md) para detalhes.

## Estrutura do projeto

```
├── .gitignore
├── .terraform-info.md
├── azure-pipelines.yml
├── ONG.Donation.Worker.slnx
├── sbemulator-config.json
├── k8s/
│   ├── README.md
│   ├── configmap.yaml
│   ├── deployment.yaml
│   ├── namespace.yaml
│   ├── secretproviderclass.yaml
│   └── worker-networkpolicy.yaml
└── src/
    ├── ONG.Donation.Worker/
    │   ├── ONG.Donation.Worker.csproj
    │   ├── Program.cs
    │   ├── Dockerfile
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   └── Consumers/
    │       └── ServiceBusDonationConsumer.cs
    ├── ONG.Donation.Worker.Domain/
    │   ├── ONG.Donation.Worker.Domain.csproj
    │   ├── Common/
    │   │   └── BaseEntity.cs
    │   ├── Entities/
    │   │   └── Payment.cs
    │   ├── Enums/
    │   │   └── DonationStatus.cs
    │   ├── Events/
    │   │   ├── DonationCreatedEvent.cs
    │   │   ├── DonationPaymentFailedEvent.cs
    │   │   └── DonationPaymentProcessedEvent.cs
    │   └── Interfaces/
    │       ├── IDomainEvent.cs
    │       └── IEventPublisher.cs
    └── ONG.Donation.Worker.Infrastructure/
        ├── ONG.Donation.Worker.Infrastructure.csproj
        ├── DependencyInjection/
        │   └── DependencyInjection.cs
        ├── Migrations/
        │   ├── 20260703012857_InitialCreate.cs
        │   ├── 20260703012857_InitialCreate.Designer.cs
        │   └── WorkerDbContextModelSnapshot.cs
        ├── Persistence/
        │   ├── Configurations/
        │   │   └── PaymentConfiguration.cs
        │   ├── Context/
        │   │   └── WorkerDbContext.cs
        │   └── Repositories/
        │       └── PaymentRepository.cs
        └── ServiceBus/
            ├── ServiceBusEventPublisher.cs
            └── ServiceBusOptions.cs
```

## CI/CD

O pipeline do Azure Pipelines (`azure-pipelines.yml`) executa:

1. **Build** — `dotnet restore` e `dotnet build`
2. **Testes** — `dotnet test`
3. **Docker** — Build e push da imagem para ACR (`ongdonationacr.azurecr.io/ong-donation-worker`)
4. **Deploy** — Aplica manifests Kubernetes no AKS (namespace, ConfigMap, SecretProviderClass, NetworkPolicy, Deployment)
5. **Rollout status** — Monitora o status do deployment com diagnósticos em caso de falha

## Observações

- **Processamento de pagamento** — `ProcessPaymentAsync()` é um stub que sempre retorna `true`. A integração com gateway real não foi implementada.
- **Health check** — Não há endpoint HTTP configurado. As probes do k8s referenciam `/health`, mas o endpoint não está implementado no código.
- **Testes** — Não há projetos de teste no repositório. O pipeline executa `dotnet test` sem assemblies de teste.
- **Application Insights** — Referenciado nos manifests k8s, mas sem SDK/configuração no código.
