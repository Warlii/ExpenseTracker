# ExpenseTracker.Api

API REST para controle de despesas e relatório mensal. Projeto de portfólio com estrutura simples: controllers tratam HTTP, services concentram regras de negócio, DTOs definem contratos e Entity Framework Core realiza a persistência.

## Tecnologias

- C# e ASP.NET Core Web API com .NET 10
- Entity Framework Core 10 e PostgreSQL (Npgsql)
- OpenAPI nativo e Swagger UI em desenvolvimento
- xUnit e SQLite em memória para testes dos serviços
- Docker e Docker Compose

## Execução com Docker

Requisitos: Docker com Compose.

1. Copie `.env.example` para `.env` e substitua a senha de exemplo por uma senha local. `.env` é ignorado pelo Git.
2. Execute `docker compose up --build -d`.
3. Acesse http://localhost:5080/swagger e http://localhost:5080/openapi/v1.json.

O PostgreSQL usa um volume persistente. A API aguarda o healthcheck do banco e aplica migrations na inicialização neste ambiente local. Execute `docker compose down` para parar; `docker compose down -v` também apaga os dados.

A conexão do Compose desabilita a negociação GSSAPI, pois este ambiente local usa senha e não possui Kerberos. Isso evita o aviso de biblioteca Kerberos ausente na imagem Linux do .NET, conforme a [documentação do Npgsql](https://www.npgsql.org/doc/security.html).

## Execução local

Requisitos: SDK .NET 10 e PostgreSQL, ou Docker apenas para o banco.

```powershell
Copy-Item .env.example .env
# Edite .env e configure uma senha local antes de iniciar o banco.
docker compose up -d db
dotnet restore
dotnet tool restore
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=expense_tracker;Username=expense_tracker;Password=SUA_SENHA_LOCAL"
dotnet ef database update --project ExpenseTracker.Api
dotnet run --project ExpenseTracker.Api
```

No Bash, use `export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=expense_tracker;Username=expense_tracker;Password=SUA_SENHA_LOCAL'`.

A connection string está em `ExpenseTracker.Api/appsettings.json`, sem senha. Variáveis de ambiente substituem a configuração usando `ConnectionStrings__DefaultConnection`. Alternativamente, configure a senha com Secret Manager (`dotnet user-secrets init --project ExpenseTracker.Api`, seguido de `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SUA_CONNECTION_STRING" --project ExpenseTracker.Api`). Nunca versione credenciais reais.

O perfil local inicia em Development na porta 5080. Swagger/OpenAPI ficam disponíveis apenas nesse ambiente. `Database__ApplyMigrations=true` habilita migrations na inicialização; fora do Compose, o padrão é false. Em produção, aplique migrations como etapa de implantação com permissões específicas.

## Migrations

A migration inicial e o snapshot estão versionados em `ExpenseTracker.Api/Data/Migrations`.

```sh
dotnet tool restore
dotnet ef migrations add NomeDaAlteracao --project ExpenseTracker.Api --output-dir Data/Migrations
dotnet ef database update --project ExpenseTracker.Api
dotnet ef migrations script --idempotent --project ExpenseTracker.Api
```

## Endpoints

| Método | Rota | Resultado |
|---|---|---|
| POST | `/api/expenses` | Cria despesa; 201 com DTO e Location |
| GET | `/api/expenses?from=2026-10-01&to=2026-10-31&categoryId=1` | Lista despesas; 200 |
| GET | `/api/expenses/{id}` | Consulta despesa; 200 ou 404 |
| PUT | `/api/expenses/{id}` | Substitui os campos da despesa; 204 ou 404 |
| DELETE | `/api/expenses/{id}` | Exclui despesa; 204 ou 404 |
| GET | `/api/expenses/reports/monthly?year=2026&month=10` | Total mensal e totais por categoria; 200 |
| POST | `/api/categories` | Cria categoria; 201 |
| GET | `/api/categories` | Lista categorias por nome; 200 |
| DELETE | `/api/categories/{id}` | Exclui categoria; 204, 404 ou 409 se estiver em uso |

Os filtros são opcionais e combináveis; as datas inicial e final são inclusivas. A listagem usa data e Id em ordem decrescente. Um relatório sem despesas retorna total zero e categorias vazias.

Crie uma categoria com `{"name":"Alimentação"}`. Use o Id retornado para criar a despesa:

```json
{
  "description": "Almoço",
  "amount": 32.50,
  "date": "2026-10-05",
  "categoryId": 1,
  "paymentMethod": "Pix"
}
```

Description é obrigatória (até 200 caracteres), PaymentMethod é obrigatório (até 50), Amount deve ser positivo com até duas casas decimais e caber em numeric(18,2), Date deve ser uma data ISO válida e diferente do valor padrão, e CategoryId deve existir. Name é obrigatório (até 100 caracteres). Textos são aparados antes da persistência. Datas futuras são permitidas; não há restrição de calendário além da validade da data. PaymentMethod é texto livre para manter o escopo simples.

Erros usam ProblemDetails: 400 para entrada inválida, 404 para recurso ausente, 409 para conflito de persistência ou categoria em uso e 500 para falhas inesperadas. Detalhes internos ficam nos logs; respostas inesperadas não expõem exceções ou credenciais.

## Build e testes

```sh
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Os testes exercitam serviços com SQLite em memória, sem depender de PostgreSQL externo. Cobrem valores inválidos, textos obrigatórios, data, categoria inexistente, atualização, exclusão, intervalo inclusivo e agregação mensal. SQLite não substitui a validação de migrations e consultas no PostgreSQL real; use o Compose para verificar o fluxo completo com o provedor de produção.

Para validar os endpoints HTTP com PostgreSQL real, inicie o Compose e execute o script com PowerShell 7:

```powershell
docker compose up --build -d --wait
pwsh -File scripts/Test-Api.ps1
```

O script verifica Swagger/OpenAPI, criação e listagem de categorias, CRUD de despesas, filtros inclusivos, relatório mensal e respostas 400, 404 e 409. Ele usa categorias próprias e remove os registros criados ao terminar. Para outro endereço, passe `-BaseUrl http://localhost:5080`.

## Decisões técnicas

- Controllers finos e serviços concretos registrados por DI. Sem interfaces redundantes, repository genérico ou camadas adicionais: DbContext já oferece rastreamento e unidade de trabalho.
- DTOs protegem o contrato HTTP e evitam expor entidades e propriedades de navegação. DataAnnotations e validação nos serviços preservam as regras também em chamadas diretas.
- `decimal`/`numeric(18,2)` evita erros de ponto flutuante nos valores monetários. A API considera uma única moeda; conversão monetária não faz parte do escopo.
- `DateOnly`/PostgreSQL `date` representa o dia da despesa sem ambiguidade de fuso horário.
- Chave estrangeira com `Restrict` protege categorias em uso no banco, incluindo concorrência entre validação e gravação. A verificação do serviço oferece uma mensagem clara; conflitos do banco também retornam 409.
- Consultas de leitura usam `AsNoTracking` e projeção em DTOs. O relatório é agregado no banco e soma os totais das categorias retornadas para manter consistência em uma única consulta.
- Operações de banco são assíncronas e recebem CancellationToken. Logging usa a infraestrutura padrão do ASP.NET Core e o tratamento global padroniza erros.
- Não há autenticação, frontend, paginação ou controle otimista por versão nesta etapa. Atualizações concorrentes seguem o comportamento padrão do EF; requisitos adicionais devem orientar essas evoluções.

Referências oficiais: [OpenAPI no ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0) e [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).
