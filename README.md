# TicketFlow

**API de processamento assíncrono de pedidos de ingressos com controle de concorrência**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=flat-square&logo=docker&logoColor=white)

---

## Sobre o projeto

TicketFlow é uma API backend que resolve o problema de **vendas duplicadas de ingressos em cenários de alta concorrência**. Quando centenas de usuários tentam comprar o último ingresso ao mesmo tempo, o sistema garante que apenas um pedido seja confirmado — sem bloquear o banco, sem filas de espera, sem degradação de performance.

**Mecanismos utilizados:**

| Mecanismo | Onde | O que faz |
|---|---|---|
| Lock otimista (RowVersion) | `Ticket` | Detecta conflito quando dois processos tentam salvar o mesmo ingresso |
| Idempotência | `Order` | Rejeita pedidos duplicados com a mesma chave |
| Fila assíncrona | RabbitMQ | Desacopla o recebimento do pedido do seu processamento |
| Dead-letter queue | RabbitMQ | Isola mensagens que falharam após 3 retries |
| Retry com backoff | Polly | Tenta reprocessar automaticamente com espera exponencial |

---

## Pré-requisitos

Antes de configurar o projeto, certifique-se de ter instalado:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — com o WSL 2 habilitado
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) — workload **ASP.NET and web development**
- [EF Core Tools](https://learn.microsoft.com/ef/core/cli/dotnet) — `dotnet tool install --global dotnet-ef`

Para verificar se tudo está instalado corretamente:

```bash
dotnet --version        # deve retornar 8.0.x
dotnet ef --version     # deve retornar 8.x.x
docker --version        # deve retornar 25.x ou superior
docker-compose --version
```

---

## Configuração do projeto

### 1. Extrair e abrir

Extraia o arquivo `TicketFlow.zip` em uma pasta de sua preferência.

> Evite caminhos com espaços ou acentos. Prefira `C:\Projetos\TicketFlow` a `C:\Meus Documentos\TCC\TicketFlow`.

Abra o Visual Studio 2022 e clique em **Open a project or solution**. Navegue até a pasta extraída e selecione o arquivo `TicketFlow.sln`.

Aguarde o Visual Studio restaurar os pacotes NuGet automaticamente — uma barra de progresso aparece na parte inferior da tela. Isso pode levar alguns minutos na primeira vez.

---

### 2. Subir a infraestrutura com Docker

O projeto usa PostgreSQL e RabbitMQ rodando em containers Docker. Você precisa subir os containers antes de rodar a API.

Abra um terminal na **pasta raiz do projeto** (onde está o `docker-compose.yml`) e execute:

```bash
docker-compose up -d
```

Para verificar se os containers estão rodando:

```bash
docker ps
```

Você deve ver dois containers ativos:

```
NAMES                    STATUS
ticketflow_postgres      Up
ticketflow_rabbitmq      Up
```

**Painel de gerenciamento do RabbitMQ** — acesse http://localhost:15672 no navegador.
- Usuário: `guest`
- Senha: `guest`

> O Docker Desktop precisa estar **aberto e rodando** sempre que for trabalhar no projeto.

---

### 3. Criar a migration e o banco de dados

O projeto usa Entity Framework Core para gerenciar o banco. Você precisa criar as tabelas antes de rodar pela primeira vez.

No terminal, dentro da pasta `src/TicketFlow.API`:

```bash
cd src/TicketFlow.API
dotnet ef migrations add InitialCreate --project ../TicketFlow.Infrastructure
dotnet ef database update
```

Se tiver múltiplos SDKs instalados e o comando falhar, especifique o startup project explicitamente:

```bash
dotnet ef migrations add InitialCreate \
  --project ../TicketFlow.Infrastructure \
  --startup-project .

dotnet ef database update --startup-project .
```

Para verificar se as tabelas foram criadas:

```bash
docker exec -it ticketflow_postgres psql -U ticketflow -d ticketflow -c "\dt"
```

Resultado esperado — as quatro tabelas do projeto:

```
 Schema |         Name          | Type  |   Owner
--------+-----------------------+-------+-----------
 public | Events                | table | ticketflow
 public | Orders                | table | ticketflow
 public | Payments              | table | ticketflow
 public | Tickets               | table | ticketflow
```

> As migrations também são aplicadas automaticamente toda vez que a API inicia. O passo acima é para confirmar que o banco está correto antes do primeiro uso.

---

### 4. Rodar a API

**Pelo Visual Studio:**

Confirme que `TicketFlow.API` está definido como projeto de inicialização — ele deve aparecer em **negrito** no Solution Explorer. Se não estiver, clique com o botão direito em `TicketFlow.API` e selecione **Set as Startup Project**.

Pressione `F5` para rodar com debug ou `Ctrl+F5` para rodar sem debug.

**Pelo terminal:**

```bash
cd src/TicketFlow.API
dotnet run
```

A API estará disponível em `https://localhost:{porta}`. A porta aparece no terminal após a inicialização:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7001
```

**Swagger** — acesse `https://localhost:{porta}/swagger` para explorar e testar os endpoints via interface web.

---

## Testando o projeto

### Fluxo básico via Swagger

Acesse o Swagger em `https://localhost:{porta}/swagger` e siga a ordem abaixo.

**1. Criar um evento com ingressos**

`POST /api/events`

```json
{
  "name": "Show de Rock",
  "venue": "Estádio Nacional",
  "date": "2026-07-15T20:00:00",
  "totalTickets": 5,
  "ticketPrice": 150.00
}
```

Copie o `id` retornado no response.

**2. Listar os tickets do evento**

`GET /api/events/{id}/tickets`

Use o `id` do passo anterior. Copie o `id` de um dos tickets retornados.

**3. Criar um pedido**

`POST /api/orders`

```json
{
  "ticketId": "{id-do-ticket}",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "idempotencyKey": "pedido-001"
}
```

A API retorna **202 Accepted** — o pedido foi enfileirado, não processado ainda.

**4. Consultar o status do pedido**

`GET /api/orders/{id}`

Use o `id` retornado no passo anterior. Aguarde alguns segundos e consulte novamente — o status deve mudar de `Pending` para `Confirmed`.

**Testando a idempotência:** envie o `POST /api/orders` novamente com o mesmo `idempotencyKey: "pedido-001"`. A API deve retornar o mesmo pedido sem criar um novo.

---

### Rodando os testes automatizados

Os testes não precisam do Docker — usam SQLite em memória e um publisher fake no lugar do RabbitMQ.

```bash
# Todos os testes
dotnet test

# Com detalhes de cada teste
dotnet test --logger "console;verbosity=detailed"

# Só unitários
dotnet test tests/TicketFlow.UnitTests

# Só integração
dotnet test tests/TicketFlow.IntegrationTests
```

Os testes mais importantes para o TCC — os de concorrência — estão em:

- `tests/TicketFlow.UnitTests/Concurrency/ConcurrencyTests.cs`
- `tests/TicketFlow.IntegrationTests/Concurrency/ConcurrencyApiTests.cs`

O teste `ProcessAsync_QuandoMultiplosPedidosParaMesmoTicket_ApenasUmDeveSerConfirmado` é a prova automatizada da hipótese central: 5 processos simultâneos tentando reservar o mesmo ingresso — apenas 1 é confirmado.

---

### Teste de carga com k6

Instale o k6 em **https://k6.io/docs/get-started/installation/** ou via winget:

```bash
winget install k6 --source winget
```

Antes de rodar, crie um evento com 1 ingresso via Swagger e copie o `ticketId`. Edite o arquivo `k6/load-test.js` e substitua `COLOQUE-O-TICKET-ID-AQUI` pelo id copiado.

Execute o teste:

```bash
k6 run k6/load-test.js
```

O script simula 100 usuários simultâneos tentando comprar o mesmo ingresso e coleta as métricas p95, p99, taxa de erro e pedidos confirmados x falhos.

---

## Estrutura do projeto

```
TicketFlow/
├── docker-compose.yml              # PostgreSQL + RabbitMQ
├── TicketFlow.sln
├── k6/
│   └── load-test.js                # script de teste de carga
├── src/
│   ├── TicketFlow.Domain/          # entidades, interfaces, enums — sem dependências externas
│   ├── TicketFlow.Application/     # serviços, DTOs, interface do publisher
│   ├── TicketFlow.Infrastructure/  # EF Core, repositórios, RabbitMQ, consumer, processor
│   └── TicketFlow.API/             # controllers, Program.cs, configuração
└── tests/
    ├── TicketFlow.UnitTests/       # testes de domínio, services e concorrência
    └── TicketFlow.IntegrationTests/# testes de API ponta a ponta e concorrência HTTP
```

---

## Arquitetura

O projeto segue **Clean Architecture** — dependências apontam sempre para dentro:

```
API  →  Application  →  Domain
              ↑
       Infrastructure
    (implementa interfaces do Domain)
```

- **Domain** — entidades com regras de negócio, interfaces dos repositórios. Sem pacotes externos.
- **Application** — `OrderService` com lógica de idempotência e publicação na fila.
- **Infrastructure** — repositórios EF Core, `RabbitMqPublisher`, `OrderProcessor` (lock otimista), `RabbitMqConsumer` (Polly retry + dead-letter).
- **API** — controllers REST, `Program.cs` com registro de DI.

---

## Comandos úteis

```bash
# Infraestrutura
docker-compose up -d          # sobe PostgreSQL e RabbitMQ
docker-compose down           # para os containers (dados preservados)
docker-compose down -v        # para e apaga os dados do banco
docker logs ticketflow_rabbitmq -f  # logs do RabbitMQ em tempo real

# EF Core
dotnet ef migrations add NomeDaMigration \
  --project src/TicketFlow.Infrastructure \
  --startup-project src/TicketFlow.API

dotnet ef database update --startup-project src/TicketFlow.API
dotnet ef database drop   --startup-project src/TicketFlow.API

# Build e testes
dotnet build
dotnet test
dotnet test --logger "console;verbosity=detailed"

# k6
k6 run -e TICKET_ID=<id> k6/load-test.js
```

---

## Solução de problemas

**A API não conecta ao banco**
Verifique se o Docker Desktop está aberto e os containers estão rodando com `docker ps`. Se os containers não aparecerem, execute `docker-compose up -d` novamente.

**O consumer não processa os pedidos**
O status dos pedidos fica em `Pending` indefinidamente. Verifique os logs da API — se aparecer erro de conexão com o RabbitMQ, o container pode ter demorado para iniciar. Reinicie a API após confirmar que o RabbitMQ está rodando.

**`dotnet ef` não é reconhecido**
Feche e abra um novo terminal. Se persistir, verifique se `C:\Users\SeuUsuario\.dotnet\tools` está na variável de ambiente `Path`.

**Migration falha com erro de conexão**
O PostgreSQL precisa estar rodando antes de aplicar migrations. Confirme com `docker ps` e tente novamente.

**Erro `DispatchConsumersAsync` nos logs**
A `ConnectionFactory` no `Program.cs` precisa ter `DispatchConsumersAsync = true`. Verifique se essa propriedade está configurada.
